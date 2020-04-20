#if ASYNC_REDIS

using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
        : IAsyncRedisNativeClient
    {
        Task<byte[][]> IAsyncRedisNativeClient.TimeAsync(CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.Time);
    }
}
#endif