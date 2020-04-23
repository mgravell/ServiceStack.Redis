#if ASYNC_REDIS

using ServiceStack.Redis.Pipeline;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
        : IRedisNativeClientAsync
    {
        internal IRedisPipelineSharedAsync PipelineAsync
            => (IRedisPipelineSharedAsync)pipeline;
        ValueTask<byte[][]> IRedisNativeClientAsync.TimeAsync(CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.Time);

        ValueTask<long> IRedisNativeClientAsync.IncrAsync(string key, CancellationToken cancellationToken)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectLongAsync(cancellationToken, Commands.Incr, key.ToUtf8Bytes());
        }
    }
}
#endif