using ServiceStack.Caching;
using System;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisClientManagerCacheClient : ICacheClientAsync, IRemoveByPatternAsync, ICacheClientExtendedAsync
    {
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}