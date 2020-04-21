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
        : IRedisClientsManagerAsync
    {
        ValueTask<ICacheClientAsync> IRedisClientsManagerAsync.GetCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<ICacheClientAsync>(new RedisClientManagerCacheClient(this));

        ValueTask<IRedisClientAsync> IRedisClientsManagerAsync.GetClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IRedisClientAsync>(GetClient(true));

        ValueTask<ICacheClientAsync> IRedisClientsManagerAsync.GetReadOnlyCacheClientAsync(CancellationToken cancellationToken)
            => new ValueTask<ICacheClientAsync>(new RedisClientManagerCacheClient(this) { ReadOnly = true });

        ValueTask<IRedisClientAsync> IRedisClientsManagerAsync.GetReadOnlyClientAsync(CancellationToken cancellationToken)
            => new ValueTask<IRedisClientAsync>(GetClient(true));
    }
}
#endif