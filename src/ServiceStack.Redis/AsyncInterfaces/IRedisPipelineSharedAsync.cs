#if ASYNC_REDIS
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Pipeline
{
    /// <summary>
    /// Pipeline interface shared by typed and non-typed pipelines
    /// </summary>
    public interface IRedisPipelineSharedAsync : IDisposable, IRedisQueueCompletableOperation
    {
        Task FlushAsync(CancellationToken cancellationToken = default);
        Task<bool> ReplayAsync(CancellationToken cancellationToken = default);
    }
}
#endif