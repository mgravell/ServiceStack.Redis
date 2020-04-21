#if ASYNC_REDIS

using ServiceStack.Caching;

namespace ServiceStack.Redis
{
    partial class RedisClientManagerCacheClient : ICacheClientAsync, IRemoveByPatternAsync, ICacheClientExtendedAsync
    {

    }
}
#endif