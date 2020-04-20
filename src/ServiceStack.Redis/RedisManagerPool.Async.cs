//Copyright (c) Service Stack LLC. All Rights Reserved.
//License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt

#if ASYNC_REDIS

using ServiceStack.Caching;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    public partial class RedisManagerPool
        : IAsyncRedisClientsManager
    {
        ValueTask<IAsyncCacheClient> IAsyncRedisClientsManager.GetCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncCacheClient>(new RedisClientManagerCacheClient(this));

        ValueTask<IAsyncRedisClient> IAsyncRedisClientsManager.GetClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncRedisClient>(GetClient(true));

        ValueTask<IAsyncCacheClient> IAsyncRedisClientsManager.GetReadOnlyCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncCacheClient>(new RedisClientManagerCacheClient(this) { ReadOnly = true });

        ValueTask<IAsyncRedisClient> IAsyncRedisClientsManager.GetReadOnlyClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncRedisClient>(GetClient(true));
    }
}
#endif