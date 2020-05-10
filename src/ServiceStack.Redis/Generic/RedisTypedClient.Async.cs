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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Generic
{
    partial class RedisTypedClient<T>
        : IRedisTypedClientAsync<T>
    {
        IRedisClientAsync AsyncClient => client;
        async ValueTask<T> IRedisTypedClientAsync<T>.GetValueAsync(string key, CancellationToken cancellationToken)
            => DeserializeValue(await AsyncClient.GetBytesAsync(key, cancellationToken).ConfigureAwait(false));

        async ValueTask IRedisTypedClientAsync<T>.SetValueAsync(string key, T entity, CancellationToken cancellationToken)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            await client.SetAsync(key, SerializeValue(entity), cancellationToken).ConfigureAwait(false);
            client.RegisterTypeId(entity);
        }
    }
}