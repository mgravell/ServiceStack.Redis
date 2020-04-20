//
// https://github.com/ServiceStack/ServiceStack.Redis
// ServiceStack.Redis: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Demis Bellot (demis.bellot@gmail.com)
//
// Copyright 2013 Service Stack LLC. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//

#if ASYNC_REDIS

using ServiceStack.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    public partial class PooledRedisClientManager
        : IAsyncRedisClientsManager
    {
        ValueTask<IAsyncCacheClient> IAsyncRedisClientsManager.GetCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncCacheClient>(new RedisClientManagerCacheClient(this));

        ValueTask<IAsyncRedisClient> IAsyncRedisClientsManager.GetClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncRedisClient>(GetClient(true));

        ValueTask<IAsyncCacheClient> IAsyncRedisClientsManager.GetReadOnlyCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncCacheClient>(new RedisClientManagerCacheClient(this) { ReadOnly = true });

        ValueTask<IAsyncRedisClient> IAsyncRedisClientsManager.GetReadOnlyClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IAsyncRedisClient>(GetReadOnlyClient(true));
    }

}
#endif