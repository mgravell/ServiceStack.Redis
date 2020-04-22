#if ASYNC_REDIS
using ServiceStack.Redis.Pipeline;
using ServiceStack.Text;
using ServiceStack.Text.Pools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
    {
        private async Task<byte[][]> SendExpectMultiDataAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return (await SendReceiveAsync(cmdWithBinaryArgs, ReadMultiData, cancellationToken, Pipeline != null ? Pipeline.CompleteMultiBytesQueuedCommand : (Action<Func<byte[][]>>)null).ConfigureAwait(false))
            ?? TypeConstants.EmptyByteArrayArray;
        }

        private async Task<T> SendReceiveAsync<T>(byte[][] cmdWithBinaryArgs,
            Func<T> fn,
            CancellationToken cancellationToken,
            Action<Func<T>> completePipelineFn = null,
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
                        result = fn();

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

        private ValueTask FlushSendBufferAsync(CancellationToken cancellationToken)
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
    }
}
#endif