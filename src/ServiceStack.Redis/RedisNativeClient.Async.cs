#if ASYNC_REDIS
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
        : IAsyncRedisNativeClient
    {
        Task<byte[][]> IAsyncRedisNativeClient.TimeAsync(CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.Time);

        protected Task<byte[][]> SendExpectMultiDataAsync(CancellationToken cancellationToken, params byte[][] cmdWithBinaryArgs)
        {
            throw new NotImplementedException();
            //return SendReceive(cmdWithBinaryArgs, ReadMultiData, Pipeline != null ? Pipeline.CompleteMultiBytesQueuedCommand : (Action<Func<byte[][]>>)null)
            //    ?? TypeConstants.EmptyByteArrayArray;
        }
    }
}
#endif