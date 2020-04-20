#if ASYNC_REDIS
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
    {
        private async Task<byte[][]> SendExpectMultiDataAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            return (await SendReceiveAsync(cmdWithBinaryArgs, ReadMultiDataAsync, cancellationToken, Pipeline != null ? Pipeline.CompleteMultiBytesQueuedCommand : (Action<Func<byte[][]>>)null).ConfigureAwait(false))
            ?? TypeConstants.EmptyByteArrayArray;
        }

        private Task<T> SendReceiveAsync<T>(byte[][] cmdWithBinaryArgs,
            Func<Task<T>> fn,
            CancellationToken cancellationToken,
            Action<Func<T>> completePipelineFn = null,
            bool sendWithoutRead = false)
        {
            throw new NotImplementedException();
            //if (TrackThread != null)
            //{
            //    if (TrackThread.Value.ThreadId != Thread.CurrentThread.ManagedThreadId)
            //        throw new InvalidAccessException(TrackThread.Value.ThreadId, TrackThread.Value.StackTrace);
            //}

            //var i = 0;
            //var didWriteToBuffer = false;
            //Exception originalEx = null;

            //var firstAttempt = DateTime.UtcNow;

            //while (true)
            //{
            //    try
            //    {
            //        if (TryConnectIfNeeded())
            //            didWriteToBuffer = false;

            //        if (socket == null)
            //            throw new RedisRetryableException("Socket is not connected");

            //        if (!didWriteToBuffer) //only write to buffer once
            //        {
            //            WriteCommandToSendBuffer(cmdWithBinaryArgs);
            //            didWriteToBuffer = true;
            //        }

            //        if (Pipeline == null) //pipeline will handle flush if in pipeline
            //        {
            //            FlushSendBuffer();
            //        }
            //        else if (!sendWithoutRead)
            //        {
            //            if (completePipelineFn == null)
            //                throw new NotSupportedException("Pipeline is not supported.");

            //            completePipelineFn(fn);
            //            return default(T);
            //        }

            //        var result = default(T);
            //        if (fn != null)
            //            result = fn();

            //        if (Pipeline == null)
            //            ResetSendBuffer();

            //        if (i > 0)
            //            Interlocked.Increment(ref RedisState.TotalRetrySuccess);

            //        Interlocked.Increment(ref RedisState.TotalCommandsSent);

            //        return result;
            //    }
            //    catch (Exception outerEx)
            //    {
            //        if (log.IsDebugEnabled)
            //            logDebug("SendReceive Exception: " + outerEx.Message);

            //        var retryableEx = outerEx as RedisRetryableException;
            //        if (retryableEx == null && outerEx is RedisException
            //            || outerEx is LicenseException)
            //        {
            //            ResetSendBuffer();
            //            throw;
            //        }

            //        var ex = retryableEx ?? GetRetryableException(outerEx);
            //        if (ex == null)
            //            throw CreateConnectionError(originalEx ?? outerEx);

            //        if (originalEx == null)
            //            originalEx = ex;

            //        var retry = DateTime.UtcNow - firstAttempt < retryTimeout;
            //        if (!retry)
            //        {
            //            if (Pipeline == null)
            //                ResetSendBuffer();

            //            Interlocked.Increment(ref RedisState.TotalRetryTimedout);
            //            throw CreateRetryTimeoutException(retryTimeout, originalEx);
            //        }

            //        Interlocked.Increment(ref RedisState.TotalRetryCount);
            //        TaskUtils.Sleep(GetBackOffMultiplier(++i));
            //    }
            //}
        }

        private Task<byte[][]> ReadMultiDataAsync()
        {
            throw new NotImplementedException();
            //int c = SafeReadByte(nameof(ReadMultiData));
            //if (c == -1)
            //    throw CreateNoMoreDataError();

            //var s = ReadLine();
            //if (log.IsDebugEnabled)
            //    Log("R: {0}", s);

            //switch (c)
            //{
            //    // Some commands like BRPOPLPUSH may return Bulk Reply instead of Multi-bulk
            //    case '$':
            //        var t = new byte[2][];
            //        t[1] = ParseSingleLine(string.Concat(char.ToString((char)c), s));
            //        return t;

            //    case '-':
            //        throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            //    case '*':
            //        if (int.TryParse(s, out var count))
            //        {
            //            if (count == -1)
            //            {
            //                //redis is in an invalid state
            //                return TypeConstants.EmptyByteArrayArray;
            //            }

            //            var result = new byte[count][];

            //            for (int i = 0; i < count; i++)
            //                result[i] = ReadData();

            //            return result;
            //        }
            //        break;
            //}

            //throw CreateResponseError("Unknown reply on multi-request: " + c + s);
        }
    }
}
#endif