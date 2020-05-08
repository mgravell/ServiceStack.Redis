﻿using ServiceStack.Redis.Pipeline;
using ServiceStack.Text;
using ServiceStack.Text.Pools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
    {
        private async ValueTask<byte[][]> SendExpectMultiDataAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return (await SendReceiveAsync(cmdWithBinaryArgs, ReadMultiDataAsync, cancellationToken,
                PipelineAsync != null ? PipelineAsync.CompleteMultiBytesQueuedCommandAsync : (Action<Func<CancellationToken, ValueTask<byte[][]>>>)null).ConfigureAwait(false))
            ?? TypeConstants.EmptyByteArrayArray;
        }

        private ValueTask<long> SendExpectLongAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return SendReceiveAsync(cmdWithBinaryArgs, ReadLongAsync, cancellationToken,
                PipelineAsync != null ? PipelineAsync.CompleteLongQueuedCommandAsync : (Action<Func<CancellationToken, ValueTask<long>>>)null);
        }

        private ValueTask SendExpectSuccessAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            //Turn Action into Func Hack
            Action<Func<CancellationToken, ValueTask<long>>> completePipelineFn = null;
            if (Pipeline != null) completePipelineFn = f => { PipelineAsync.CompleteVoidQueuedCommandAsync(async ct => await f(ct).ConfigureAwait(false)); };

            return DiscardResult(SendReceiveAsync(cmdWithBinaryArgs, ExpectSuccessFnAsync, cancellationToken, completePipelineFn));
        }

        internal static ValueTask DiscardResult<T>(ValueTask<T> pending)
        {
            return pending.IsCompletedSuccessfully ? default : Awaited(pending);
            async static ValueTask Awaited(ValueTask<T> pending) => await pending.ConfigureAwait(false);
        }

        private ValueTask<byte[]> SendExpectDataAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return SendReceiveAsync(cmdWithBinaryArgs, ReadDataAsync, cancellationToken, Pipeline != null ? PipelineAsync.CompleteBytesQueuedCommandAsync : (Action<Func<CancellationToken, ValueTask<byte[]>>>)null);
        }

        private ValueTask<string> SendExpectCodeAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return SendReceiveAsync(cmdWithBinaryArgs, ExpectCodeAsync, cancellationToken, Pipeline != null ? PipelineAsync.CompleteStringQueuedCommandAsync : (Action<Func<CancellationToken, ValueTask<string>>>)null);
        }

        private async ValueTask<ScanResult> SendExpectScanResultAsync(CancellationToken cancellationToken, byte[] cmd, params byte[][] args)
        {
            var cmdWithArgs = MergeCommandWithArgs(cmd, args);
            var multiData = await SendExpectDeeplyNestedMultiDataAsync(cancellationToken, cmdWithArgs).ConfigureAwait(false);
            var counterBytes = (byte[])multiData[0];

            var ret = new ScanResult
            {
                Cursor = ulong.Parse(counterBytes.FromUtf8Bytes()),
                Results = new List<byte[]>()
            };
            var keysBytes = (object[])multiData[1];

            foreach (var keyBytes in keysBytes)
            {
                ret.Results.Add((byte[])keyBytes);
            }

            return ret;
        }

        private ValueTask<object[]> SendExpectDeeplyNestedMultiDataAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return SendReceiveAsync(cmdWithBinaryArgs, ReadDeeplyNestedMultiDataAsync, cancellationToken);
        }

        private async ValueTask<object[]> ReadDeeplyNestedMultiDataAsync(CancellationToken cancellationToken)
        {
            var result = await ReadDeeplyNestedMultiDataItemAsync(cancellationToken).ConfigureAwait(false);
            return (object[])result;
        }

        private async ValueTask<object> ReadDeeplyNestedMultiDataItemAsync(CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
                throw CreateNoMoreDataError();

            var s = ReadLine();
            if (log.IsDebugEnabled)
                Log("R: {0}", s);

            switch (c)
            {
                case '$':
                    return await ParseSingleLineAsync(string.Concat(char.ToString((char)c), s), cancellationToken).ConfigureAwait(false);

                case '-':
                    throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

                case '*':
                    if (int.TryParse(s, out var count))
                    {
                        var array = new object[count];
                        for (int i = 0; i < count; i++)
                        {
                            array[i] = await ReadDeeplyNestedMultiDataItemAsync(cancellationToken).ConfigureAwait(false);
                        }

                        return array;
                    }
                    break;

                default:
                    return s;
            }

            throw CreateResponseError("Unknown reply on multi-request: " + c + s);
        }

        private async ValueTask<T> SendReceiveAsync<T>(byte[][] cmdWithBinaryArgs,
            Func<CancellationToken, ValueTask<T>> fn,
            CancellationToken cancellationToken,
            Action<Func<CancellationToken, ValueTask<T>>> completePipelineFn = null,
            bool sendWithoutRead = false)
        {
            //if (TrackThread != null)
            //{
            //    if (TrackThread.Value.ThreadId != Thread.CurrentThread.ManagedThreadId)
            //        throw new InvalidAccessException(TrackThread.Value.ThreadId, TrackThread.Value.StackTrace);
            //}

            var i = 0;
            var didWriteToBuffer = false;
            Exception originalEx = null;

            var firstAttempt = DateTime.UtcNow;

            while (true)
            {
                try
                {
                    if (TryConnectIfNeeded()) // TODO: asyncify
                        didWriteToBuffer = false;

                    if (socket == null)
                        throw new RedisRetryableException("Socket is not connected");

                    if (!didWriteToBuffer) //only write to buffer once
                    {
                        WriteCommandToSendBuffer(cmdWithBinaryArgs);
                        didWriteToBuffer = true;
                    }

                    if (Pipeline == null) //pipeline will handle flush if in pipeline
                    {
                        await FlushSendBufferAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else if (!sendWithoutRead)
                    {
                        if (completePipelineFn == null)
                            throw new NotSupportedException("Pipeline is not supported.");

                        completePipelineFn(fn);
                        return default(T);
                    }

                    var result = default(T);
                    if (fn != null)
                        result = await fn(cancellationToken).ConfigureAwait(false);

                    if (Pipeline == null)
                        ResetSendBuffer();

                    if (i > 0)
                        Interlocked.Increment(ref RedisState.TotalRetrySuccess);

                    Interlocked.Increment(ref RedisState.TotalCommandsSent);

                    return result;
                }
                catch (Exception outerEx)
                {
                    if (log.IsDebugEnabled)
                        logDebug("SendReceive Exception: " + outerEx.Message);

                    var retryableEx = outerEx as RedisRetryableException;
                    if (retryableEx == null && outerEx is RedisException
                        || outerEx is LicenseException)
                    {
                        ResetSendBuffer();
                        throw;
                    }

                    var ex = retryableEx ?? GetRetryableException(outerEx);
                    if (ex == null)
                        throw CreateConnectionError(originalEx ?? outerEx);

                    if (originalEx == null)
                        originalEx = ex;

                    var retry = DateTime.UtcNow - firstAttempt < retryTimeout;
                    if (!retry)
                    {
                        if (Pipeline == null)
                            ResetSendBuffer();

                        Interlocked.Increment(ref RedisState.TotalRetryTimedout);
                        throw CreateRetryTimeoutException(retryTimeout, originalEx);
                    }

                    Interlocked.Increment(ref RedisState.TotalRetryCount);
                    await Task.Delay(GetBackOffMultiplier(++i)).ConfigureAwait(false);
                }
            }
        }

        internal ValueTask FlushSendBufferAsync(CancellationToken cancellationToken)
        {
            if (currentBufferIndex > 0)
                PushCurrentBuffer();

            if (cmdBuffer.Count > 0)
            {
                if (OnBeforeFlush != null)
                    OnBeforeFlush();

                if (!Env.IsMono && sslStream == null)
                {
                    if (log.IsDebugEnabled && RedisConfig.EnableVerboseLogging)
                    {
                        var sb = StringBuilderCache.Allocate();
                        foreach (var cmd in cmdBuffer)
                        {
                            if (sb.Length > 50)
                                break;

                            sb.Append(Encoding.UTF8.GetString(cmd.Array, cmd.Offset, cmd.Count));
                        }
                        logDebug("socket.Send: " + StringBuilderCache.ReturnAndFree(sb.Replace("\r\n", " ")).SafeSubstring(0, 50));
                    }

                    return new ValueTask(socket.SendAsync(cmdBuffer, SocketFlags.None));
                }
                else
                {
                    //Sending IList<ArraySegment> Throws 'Message to Large' SocketException in Mono
                    if (sslStream == null)
                    {
                        foreach (var segment in cmdBuffer)
                        {   // TODO: what is modern Mono behavior here?
                            socket.Send(segment.Array, segment.Offset, segment.Count, SocketFlags.None);
                        }
                    }
                    else
                    {
                        return WriteAsync(sslStream, cmdBuffer, cancellationToken);
                    }
                }
            }

            return default;

            static async ValueTask WriteAsync(Stream destination, List<ArraySegment<byte>> buffer, CancellationToken cancellationToken)
            {
                foreach (var segment in buffer)
                {
#if ASYNC_MEMORY
                    await destination.WriteAsync(new ReadOnlyMemory<byte>(segment.Array, segment.Offset, segment.Count), cancellationToken).ConfigureAwait(false);
#else
                    await destination.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
#endif
                }
            }
        }


        private ValueTask<int> SafeReadByteAsync(in CancellationToken cancellationToken, [CallerMemberName]string name = null)
        {
            AssertNotDisposed();

            if (log.IsDebugEnabled && RedisConfig.EnableVerboseLogging)
                logDebug(name + "()");

            return bufferedReader.ReadByteAsync(cancellationToken);
        }

        private async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            AssertNotDisposed();

            var sb = StringBuilderCache.Allocate();

            int c;
            while ((c = await bufferedReader.ReadByteAsync(cancellationToken).ConfigureAwait(false)) != -1)
            {
                if (c == '\r')
                    continue;
                if (c == '\n')
                    break;
                sb.Append((char)c);
            }
            return StringBuilderCache.ReturnAndFree(sb);
        }

        private async ValueTask<byte[]> ParseSingleLineAsync(string r, CancellationToken cancellationToken)
        {
            if (log.IsDebugEnabled)
                Log("R: {0}", r);
            if (r.Length == 0)
                throw CreateResponseError("Zero length response");

            char c = r[0];
            if (c == '-')
                throw CreateResponseError(r.StartsWith("-ERR") ? r.Substring(5) : r.Substring(1));

            if (c == '$')
            {
                if (r == "$-1")
                    return null;

                if (int.TryParse(r.Substring(1), out var count))
                {
                    var retbuf = new byte[count];

                    var offset = 0;
                    while (count > 0)
                    {
                        var readCount = await bufferedReader.ReadAsync(retbuf, offset, count, cancellationToken).ConfigureAwait(false);
                        if (readCount <= 0)
                            throw CreateResponseError("Unexpected end of Stream");

                        offset += readCount;
                        count -= readCount;
                    }

                    if (await bufferedReader.ReadByteAsync(cancellationToken).ConfigureAwait(false) != '\r'
                        || await bufferedReader.ReadByteAsync(cancellationToken).ConfigureAwait(false) != '\n')
                        throw CreateResponseError("Invalid termination");

                    return retbuf;
                }
                throw CreateResponseError("Invalid length");
            }

            if (c == ':' || c == '+')
            {
                //match the return value
                return r.Substring(1).ToUtf8Bytes();
            }
            throw CreateResponseError("Unexpected reply: " + r);
        }

        private ValueTask<byte[]> ReadDataAsync(CancellationToken cancellationToken)
        {
            var pending = ReadLineAsync(cancellationToken);
            return pending.IsCompletedSuccessfully
                ? ParseSingleLineAsync(pending.Result, cancellationToken)
                : Awaited(this, pending, cancellationToken);

            static async ValueTask<byte[]> Awaited(RedisNativeClient @this, ValueTask<string> pending, CancellationToken cancellationToken)
            {
                var r = await pending.ConfigureAwait(false);
                return await @this.ParseSingleLineAsync(r, cancellationToken).ConfigureAwait(false);
            }
        }

        private async ValueTask<string> ExpectCodeAsync(CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
                throw CreateNoMoreDataError();

            var s = await ReadLineAsync(cancellationToken);

            if (log.IsDebugEnabled)
                Log((char)c + s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            return s;
        }

        private async ValueTask<byte[][]> ReadMultiDataAsync(CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
                throw CreateNoMoreDataError();

            var s = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (log.IsDebugEnabled)
                Log("R: {0}", s);

            switch (c)
            {
                // Some commands like BRPOPLPUSH may return Bulk Reply instead of Multi-bulk
                case '$':
                    var t = new byte[2][];
                    t[1] = await ParseSingleLineAsync(string.Concat(char.ToString((char)c), s), cancellationToken).ConfigureAwait(false);
                    return t;

                case '-':
                    throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

                case '*':
                    if (int.TryParse(s, out var count))
                    {
                        if (count == -1)
                        {
                            //redis is in an invalid state
                            return TypeConstants.EmptyByteArrayArray;
                        }

                        var result = new byte[count][];

                        for (int i = 0; i < count; i++)
                            result[i] = await ReadDataAsync(cancellationToken).ConfigureAwait(false);

                        return result;
                    }
                    break;
            }

            throw CreateResponseError("Unknown reply on multi-request: " + c + s);
        }

        public async ValueTask<long> ReadLongAsync(CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
                throw CreateNoMoreDataError();

            var s = await ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (log.IsDebugEnabled)
                Log("R: {0}", s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            if (c == ':' || c == '$')//really strange why ZRANK needs the '$' here
            {
                long i;
                if (long.TryParse(s, out i))
                    return i;
            }
            throw CreateResponseError("Unknown reply on integer response: " + c + s);
        }

        internal ValueTask ExpectOkAsync(CancellationToken cancellationToken)
            => ExpectWordAsync(OK, cancellationToken);

        internal ValueTask ExpectQueuedAsync(CancellationToken cancellationToken)
            => ExpectWordAsync(QUEUED, cancellationToken);

        internal ValueTask<long> ExpectSuccessFnAsync(CancellationToken cancellationToken)
        {
            var pending = ExpectSuccessAsync(cancellationToken);
            return pending.IsCompletedSuccessfully ? default : Awaited(pending);

            static async ValueTask<long> Awaited(ValueTask pending)
            {
                await pending.ConfigureAwait(false);
                return 0;
            }
        }

        internal async ValueTask ExpectSuccessAsync(CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
                throw CreateNoMoreDataError();

            var s = await ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (log.IsDebugEnabled)
                Log((char)c + s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") && s.Length >= 4 ? s.Substring(4) : s);
        }


        private async ValueTask ExpectWordAsync(string word, CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
                throw CreateNoMoreDataError();

            var s = await ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (log.IsDebugEnabled)
                Log((char)c + s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            if (s != word)
                throw CreateResponseError($"Expected '{word}' got '{s}'");
        }

        internal async ValueTask<int> ReadMultiDataResultCountAsync(CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
                throw CreateNoMoreDataError();

            var s = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (log.IsDebugEnabled)
                Log("R: {0}", s);
            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);
            if (c == '*')
            {
                if (int.TryParse(s, out var count))
                {
                    return count;
                }
            }
            throw CreateResponseError("Unknown reply on multi-request: " + c + s);
        }
    }
}