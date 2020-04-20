#if ASYNC_REDIS

using ServiceStack.Caching;

namespace ServiceStack.Redis
{
    partial class RedisClientManagerCacheClient : IAsyncCacheClient, IAsyncRemoveByPattern, IAsyncCacheClientExtended
    {

    }
}
#endif