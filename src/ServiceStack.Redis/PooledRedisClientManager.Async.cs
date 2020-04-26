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

using ServiceStack.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    public partial class PooledRedisClientManager
        : IRedisClientsManagerAsync
    {
        ValueTask<ICacheClientAsync> IRedisClientsManagerAsync.GetCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<ICacheClientAsync>(new RedisClientManagerCacheClient(this));

        ValueTask<IRedisClientAsync> IRedisClientsManagerAsync.GetClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IRedisClientAsync>(GetClient(true));

        ValueTask<ICacheClientAsync> IRedisClientsManagerAsync.GetReadOnlyCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<ICacheClientAsync>(new RedisClientManagerCacheClient(this) { ReadOnly = true });

        ValueTask<IRedisClientAsync> IRedisClientsManagerAsync.GetReadOnlyClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IRedisClientAsync>(GetReadOnlyClient(true));
    }

}