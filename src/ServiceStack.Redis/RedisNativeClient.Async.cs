#if ASYNC_REDIS

using ServiceStack.Redis.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
        : IRedisNativeClientAsync
    {
        internal IRedisPipelineSharedAsync PipelineAsync
            => (IRedisPipelineSharedAsync)pipeline;
        Task<byte[][]> IRedisNativeClientAsync.TimeAsync(CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.Time);
    }
}
#endif