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

using ServiceStack.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Generic
{
    partial class RedisTypedClient<T>
        : IRedisTypedClientAsync<T>
    {
        public IRedisTypedClientAsync<T> AsAsync() => this;

        IRedisClientAsync AsyncClient => client;
        IRedisNativeClientAsync AsyncNative => client;

        async ValueTask<T> IRedisTypedClientAsync<T>.GetValueAsync(string key, CancellationToken cancellationToken)
            => DeserializeValue(await AsyncNative.GetAsync(key, cancellationToken).ConfigureAwait(false));

        async ValueTask IRedisTypedClientAsync<T>.SetValueAsync(string key, T entity, CancellationToken cancellationToken)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            await AsyncClient.SetAsync(key, SerializeValue(entity), cancellationToken).ConfigureAwait(false);
            client.RegisterTypeId(entity);
        }

        ValueTask<T> IEntityStoreAsync<T>.GetByIdAsync(object id, CancellationToken cancellationToken)
        {
            var key = client.UrnKey<T>(id);
            return AsAsync().GetValueAsync(key, cancellationToken);
        }

        async ValueTask<IList<T>> IEntityStoreAsync<T>.GetByIdsAsync(IEnumerable ids, CancellationToken cancellationToken)
        {
            if (ids != null)
            {
                var urnKeys = ids.Map(x => client.UrnKey<T>(x));
                if (urnKeys.Count != 0)
                    return await AsAsync().GetValuesAsync(urnKeys, cancellationToken).ConfigureAwait(false);
            }

            return new List<T>();
        }

        async ValueTask<IList<T>> IEntityStoreAsync<T>.GetAllAsync(CancellationToken cancellationToken)
        {
            var allKeys = await AsyncClient.GetAllItemsFromSetAsync(this.TypeIdsSetKey, cancellationToken).ConfigureAwait(false);
            return await AsAsync().GetByIdsAsync(allKeys.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<T> IEntityStoreAsync<T>.StoreAsync(T entity, CancellationToken cancellationToken)
        {
            var urnKey = client.UrnKey(entity);
            await AsAsync().SetValueAsync(urnKey, entity, cancellationToken).ConfigureAwait(false);
            return entity;
        }

        async ValueTask IEntityStoreAsync<T>.StoreAllAsync(IEnumerable<T> entities, CancellationToken cancellationToken)
        {
            if (PrepareStoreAll(entities, out var keys, out var values, out var entitiesList))
            {
                await AsyncNative.MSetAsync(keys, values, cancellationToken).ConfigureAwait(false);
                await client.RegisterTypeIdsAsync(entitiesList, cancellationToken).ConfigureAwait(false);
            }
        }

        async ValueTask IEntityStoreAsync<T>.DeleteAsync(T entity, CancellationToken cancellationToken)
        {
            var urnKey = client.UrnKey(entity);
            await AsyncClient.RemoveEntryAsync(new[] { urnKey }, cancellationToken).ConfigureAwait(false);
            await client.RemoveTypeIdsAsync(new[] { entity },  cancellationToken);
        }

        async ValueTask IEntityStoreAsync<T>.DeleteByIdAsync(object id, CancellationToken cancellationToken)
        {
            var urnKey = client.UrnKey<T>(id);

            await AsyncClient.RemoveEntryAsync(new[] { urnKey }, cancellationToken).ConfigureAwait(false);
            await client.RemoveTypeIdsAsync<T>(new[] { id.ToString() }, cancellationToken);
        }

        async ValueTask IEntityStoreAsync<T>.DeleteByIdsAsync(IEnumerable ids, CancellationToken cancellationToken)
        {
            if (ids == null) return;

            var urnKeys = ids.Map(t => client.UrnKey<T>(t));
            if (urnKeys.Count > 0)
            {
                await AsyncClient.RemoveEntryAsync(urnKeys.ToArray(), cancellationToken).ConfigureAwait(false);
                await client.RemoveTypeIdsAsync<T>(ids.Map(x => x.ToString()).ToArray(), cancellationToken).ConfigureAwait(false);
            }
        }

        async ValueTask IEntityStoreAsync<T>.DeleteAllAsync(CancellationToken cancellationToken)
        {
            var ids = await AsyncClient.GetAllItemsFromSetAsync(this.TypeIdsSetKey, cancellationToken).ConfigureAwait(false);
            var urnKeys = ids.Map(t => client.UrnKey<T>(t));
            if (urnKeys.Count > 0)
            {
                await AsyncClient.RemoveEntryAsync(urnKeys.ToArray(), cancellationToken).ConfigureAwait(false);
                await AsyncClient.RemoveEntryAsync(new[] { this.TypeIdsSetKey }, cancellationToken).ConfigureAwait(false);
            }
        }

        async ValueTask<List<T>> IRedisTypedClientAsync<T>.GetValuesAsync(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys.IsNullOrEmpty()) return new List<T>();

            var resultBytesArray = await AsyncNative.MGetAsync(keys.ToArray(), cancellationToken).ConfigureAwait(false);
            return ProcessGetValues(resultBytesArray);
        }
    }
}