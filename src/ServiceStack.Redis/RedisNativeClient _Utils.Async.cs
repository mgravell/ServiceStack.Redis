#if ASYNC_REDIS
using ServiceStack.Redis.Pipeline;
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
        private async Task<byte[][]> SendExpectMultiDataAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return (await SendReceiveAsync(cmdWithBinaryArgs, ReadMultiDataAsync, cancellationToken,
                PipelineAsync != null ? PipelineAsync.CompleteMultiBytesQueuedCommandAsync : (Action<Func<CancellationToken, ValueTask<byte[][]>>>)null).ConfigureAwait(false))
            ?? TypeConstants.EmptyByteArrayArray;
        }

        private async Task<T> SendReceiveAsync<T>(byte[][] cmdWithBinaryArgs,
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


        private ValueTask<int> SafeReadByteAsync([CallerMemberName]string name = null)
        {
            AssertNotDisposed();

            if (log.IsDebugEnabled && RedisConfig.EnableVerboseLogging)
                logDebug(name + "()");

            return bufferedReader.ReadByteAsync();
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

        private ValueTask<byte[]> ReadDataAsync(in CancellationToken cancellationToken)
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

        private async ValueTask<byte[][]> ReadMultiDataAsync(CancellationToken cancellationToken)
        {
            int c = await SafeReadByteAsync().ConfigureAwait(false);
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
    }
}
#endif