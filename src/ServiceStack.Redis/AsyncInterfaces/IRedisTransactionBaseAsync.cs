#if ASYNC_REDIS
using ServiceStack.Redis.Pipeline;

namespace ServiceStack.Redis
{
    /// <summary>
    /// Base transaction interface, shared by typed and non-typed transactions
    /// </summary>
    public interface IRedisTransactionBaseAsync : IRedisPipelineSharedAsync
    {
    }
}
#endif