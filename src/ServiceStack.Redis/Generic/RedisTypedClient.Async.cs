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
using ServiceStack.Model;
using ServiceStack.Redis.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
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

        IRedisClientAsync IRedisTypedClientAsync<T>.RedisClient => client;

        ValueTask<T> IRedisTypedClientAsync<T>.GetValueAsync(string key, CancellationToken cancellationToken)
            => DeserializeValue(AsyncNative.GetAsync(key, cancellationToken));

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
            await client.RemoveTypeIdsAsync(new[] { entity },  cancellationToken).ConfigureAwait(false);
        }

        async ValueTask IEntityStoreAsync<T>.DeleteByIdAsync(object id, CancellationToken cancellationToken)
        {
            var urnKey = client.UrnKey<T>(id);

            await AsyncClient.RemoveEntryAsync(new[] { urnKey }, cancellationToken).ConfigureAwait(false);
            await client.RemoveTypeIdsAsync<T>(new[] { id.ToString() }, cancellationToken).ConfigureAwait(false);
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

        IRedisTypedTransaction<T> IRedisTypedClientAsync<T>.CreateTransactionAsync()
            => new RedisTypedTransaction<T>(this);

        IRedisTypedPipeline<T> IRedisTypedClientAsync<T>.CreatePipelineAsync()
            => new RedisTypedPipeline<T>(this);


        ValueTask<IAsyncDisposable> IRedisTypedClientAsync<T>.AcquireLockAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
            => AsyncClient.AcquireLockAsync(this.TypeLockKey, timeOut, cancellationToken);

        long IRedisTypedClientAsync<T>.Db => AsyncClient.Db;

        ValueTask IRedisTypedClientAsync<T>.ChangeDbAsync(long db, CancellationToken cancellationToken)
            => AsyncClient.ChangeDbAsync(db, cancellationToken);

        ValueTask<List<string>> IRedisTypedClientAsync<T>.GetAllKeysAsync(CancellationToken cancellationToken)
            => AsyncClient.GetAllKeysAsync(cancellationToken);

        ValueTask IRedisTypedClientAsync<T>.SetSequenceAsync(int value, CancellationToken cancellationToken)
            => AsyncNative.GetSetAsync(SequenceKey, Encoding.UTF8.GetBytes(value.ToString()), cancellationToken).Await();

        ValueTask<long> IRedisTypedClientAsync<T>.GetNextSequenceAsync(CancellationToken cancellationToken)
            => AsAsync().IncrementValueAsync(SequenceKey, cancellationToken);

        ValueTask<long> IRedisTypedClientAsync<T>.GetNextSequenceAsync(int incrBy, CancellationToken cancellationToken)
            => AsAsync().IncrementValueByAsync(SequenceKey, incrBy, cancellationToken);

        ValueTask<RedisKeyType> IRedisTypedClientAsync<T>.GetEntryTypeAsync(string key, CancellationToken cancellationToken)
            => AsyncClient.GetEntryTypeAsync(key, cancellationToken);

        ValueTask<string> IRedisTypedClientAsync<T>.GetRandomKeyAsync(CancellationToken cancellationToken)
            => AsyncClient.GetRandomKeyAsync(cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AssertNotNull(object obj, string name = "key")
        {
            if (obj is null) Throw(name);
            static void Throw(string name) => throw new ArgumentNullException(name);
        }

        async ValueTask IRedisTypedClientAsync<T>.SetValueAsync(string key, T entity, TimeSpan expireIn, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            await AsyncClient.SetAsync(key, SerializeValue(entity)).ConfigureAwait(false);
            await client.RegisterTypeIdAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<bool> IRedisTypedClientAsync<T>.SetValueIfNotExistsAsync(string key, T entity, CancellationToken cancellationToken)
        {
            var success = await AsyncNative.SetNXAsync(key, SerializeValue(entity)).IsSuccess().ConfigureAwait(false);
            if (success) await client.RegisterTypeIdAsync(entity, cancellationToken);
            return success;
        }

        async ValueTask<bool> IRedisTypedClientAsync<T>.SetValueIfExistsAsync(string key, T entity, CancellationToken cancellationToken)
        {
            var success = await AsyncNative.SetAsync(key, SerializeValue(entity), exists: true).ConfigureAwait(false);
            if (success) await client.RegisterTypeIdAsync(entity, cancellationToken).ConfigureAwait(false);
            return success;
        }

        async ValueTask<T> IRedisTypedClientAsync<T>.StoreAsync(T entity, TimeSpan expireIn, CancellationToken cancellationToken)
        {
            var urnKey = client.UrnKey(entity);
            await AsAsync().SetValueAsync(urnKey, entity, cancellationToken).ConfigureAwait(false);
            return entity;
        }

        ValueTask<T> IRedisTypedClientAsync<T>.GetAndSetValueAsync(string key, T value, CancellationToken cancellationToken)
            => DeserializeValue(AsyncNative.GetSetAsync(key, SerializeValue(value), cancellationToken));

        ValueTask<bool> IRedisTypedClientAsync<T>.ContainsKeyAsync(string key, CancellationToken cancellationToken)
            => AsyncNative.ExistsAsync(key, cancellationToken).IsSuccess();

        ValueTask<bool> IRedisTypedClientAsync<T>.RemoveEntryAsync(string key, CancellationToken cancellationToken)
            => AsyncNative.DelAsync(key, cancellationToken).IsSuccess();

        ValueTask<bool> IRedisTypedClientAsync<T>.RemoveEntryAsync(string[] keys, CancellationToken cancellationToken)
            => AsyncNative.DelAsync(keys, cancellationToken).IsSuccess();

        async ValueTask<bool> IRedisTypedClientAsync<T>.RemoveEntryAsync(IHasStringId[] entities, CancellationToken cancellationToken)
        {
            var ids = entities.Map(x => x.Id);
            var success = await AsyncNative.DelAsync(ids.ToArray(), cancellationToken).IsSuccess().ConfigureAwait(false);
            if (success) await client.RemoveTypeIdsAsync(ids.ToArray(), cancellationToken).ConfigureAwait(false);
            return success;
        }

        ValueTask<long> IRedisTypedClientAsync<T>.IncrementValueAsync(string key, CancellationToken cancellationToken)
            => AsyncNative.IncrAsync(key, cancellationToken);

        ValueTask<long> IRedisTypedClientAsync<T>.IncrementValueByAsync(string key, int count, CancellationToken cancellationToken)
            => AsyncNative.IncrByAsync(key, count, cancellationToken);

        ValueTask<long> IRedisTypedClientAsync<T>.DecrementValueAsync(string key, CancellationToken cancellationToken)
            => AsyncNative.DecrAsync(key, cancellationToken);

        ValueTask<long> IRedisTypedClientAsync<T>.DecrementValueByAsync(string key, int count, CancellationToken cancellationToken)
            => AsyncNative.DecrByAsync(key, count, cancellationToken);

        ValueTask<bool> IRedisTypedClientAsync<T>.ExpireInAsync(object id, TimeSpan expiresIn, CancellationToken cancellationToken)
        {
            var key = client.UrnKey<T>(id);
            return AsyncClient.ExpireEntryInAsync(key, expiresIn, cancellationToken);
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.ExpireAtAsync(object id, DateTime expireAt, CancellationToken cancellationToken)
        {
            var key = client.UrnKey<T>(id);
            return AsyncClient.ExpireEntryAtAsync(key, expireAt, cancellationToken);
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.ExpireEntryInAsync(string key, TimeSpan expireIn, CancellationToken cancellationToken)
            => AsyncClient.ExpireEntryInAsync(key, expireIn, cancellationToken);

        ValueTask<bool> IRedisTypedClientAsync<T>.ExpireEntryAtAsync(string key, DateTime expireAt, CancellationToken cancellationToken)
            => AsyncClient.ExpireEntryAtAsync(key, expireAt, cancellationToken);

        async ValueTask<TimeSpan> IRedisTypedClientAsync<T>.GetTimeToLiveAsync(string key, CancellationToken cancellationToken)
            => TimeSpan.FromSeconds(await AsyncNative.TtlAsync(key, cancellationToken).ConfigureAwait(false));

        ValueTask IRedisTypedClientAsync<T>.ForegroundSaveAsync(CancellationToken cancellationToken)
            => AsyncClient.ForegroundSaveAsync(cancellationToken);

        ValueTask IRedisTypedClientAsync<T>.BackgroundSaveAsync(CancellationToken cancellationToken)
            => AsyncClient.BackgroundSaveAsync(cancellationToken);

        ValueTask IRedisTypedClientAsync<T>.FlushDbAsync(CancellationToken cancellationToken)
            => AsyncClient.FlushDbAsync(cancellationToken);

        ValueTask IRedisTypedClientAsync<T>.FlushAllAsync(CancellationToken cancellationToken)
            => AsyncClient.FlushAllAsync(cancellationToken);

        async ValueTask<T[]> IRedisTypedClientAsync<T>.SearchKeysAsync(string pattern, CancellationToken cancellationToken)
        {
            var strKeys = await AsyncClient.SearchKeysAsync(pattern, cancellationToken).ConfigureAwait(false);
            return SearchKeysParse(strKeys);
        }

        private ValueTask<List<T>> CreateList(ValueTask<byte[][]> pending)
        {
            return pending.IsCompletedSuccessfully ? CreateList(pending.Result).AsValueTask() : Awaited(this, pending);
            static async ValueTask<List<T>> Awaited(RedisTypedClient<T> obj, ValueTask<byte[][]> pending)
                => obj.CreateList(await pending.ConfigureAwait(false));
        }
        private ValueTask<T> DeserializeValue(ValueTask<byte[]> pending)
        {
            return pending.IsCompletedSuccessfully ? DeserializeValue(pending.Result).AsValueTask() : Awaited(this, pending);
            static async ValueTask<T> Awaited(RedisTypedClient<T> obj, ValueTask<byte[]> pending)
                => obj.DeserializeValue(await pending.ConfigureAwait(false));
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetSortedEntryValuesAsync(IRedisSet<T> fromSet, int startingFrom, int endingAt, CancellationToken cancellationToken)
        {
            var sortOptions = new SortOptions { Skip = startingFrom, Take = endingAt, };
            var multiDataList = AsyncNative.SortAsync(fromSet.Id, sortOptions, cancellationToken);
            return CreateList(multiDataList);
        }

        ValueTask IRedisTypedClientAsync<T>.StoreAsHashAsync(T entity, CancellationToken cancellationToken)
            => AsyncClient.StoreAsHashAsync(entity, cancellationToken);

        ValueTask<T> IRedisTypedClientAsync<T>.GetFromHashAsync(object id, CancellationToken cancellationToken)
            => AsyncClient.GetFromHashAsync<T>(id, cancellationToken);

        async ValueTask<HashSet<T>> IRedisTypedClientAsync<T>.GetAllItemsFromSetAsync(IRedisSet<T> fromSet, CancellationToken cancellationToken)
        {
            var multiDataList = await AsyncNative.SMembersAsync(fromSet.Id, cancellationToken).ConfigureAwait(false);
            return CreateHashSet(multiDataList);
        }

        ValueTask IRedisTypedClientAsync<T>.AddItemToSetAsync(IRedisSet<T> toSet, T item, CancellationToken cancellationToken)
            => AsyncNative.SAddAsync(toSet.Id, SerializeValue(item), cancellationToken).Await();

        ValueTask IRedisTypedClientAsync<T>.RemoveItemFromSetAsync(IRedisSet<T> fromSet, T item, CancellationToken cancellationToken)
            => AsyncNative.SRemAsync(fromSet.Id, SerializeValue(item), cancellationToken).Await();

        ValueTask<T> IRedisTypedClientAsync<T>.PopItemFromSetAsync(IRedisSet<T> fromSet, CancellationToken cancellationToken)
            => DeserializeValue(AsyncNative.SPopAsync(fromSet.Id, cancellationToken));

        ValueTask IRedisTypedClientAsync<T>.MoveBetweenSetsAsync(IRedisSet<T> fromSet, IRedisSet<T> toSet, T item, CancellationToken cancellationToken)
            => AsyncNative.SMoveAsync(fromSet.Id, toSet.Id, SerializeValue(item), cancellationToken);

        ValueTask<long> IRedisTypedClientAsync<T>.GetSetCountAsync(IRedisSet<T> set, CancellationToken cancellationToken)
            => AsyncNative.SCardAsync(set.Id, cancellationToken);

        ValueTask<bool> IRedisTypedClientAsync<T>.SetContainsItemAsync(IRedisSet<T> set, T item, CancellationToken cancellationToken)
            => AsyncNative.SIsMemberAsync(set.Id, SerializeValue(item)).IsSuccess();

        async ValueTask<HashSet<T>> IRedisTypedClientAsync<T>.GetIntersectFromSetsAsync(IRedisSet<T>[] sets, CancellationToken cancellationToken)
        {
            var multiDataList = await AsyncNative.SInterAsync(sets.Map(x => x.Id).ToArray(), cancellationToken).ConfigureAwait(false);
            return CreateHashSet(multiDataList);
        }

        ValueTask IRedisTypedClientAsync<T>.StoreIntersectFromSetsAsync(IRedisSet<T> intoSet, IRedisSet<T>[] sets, CancellationToken cancellationToken)
            => AsyncNative.SInterStoreAsync(intoSet.Id, sets.Map(x => x.Id).ToArray(), cancellationToken);

        async ValueTask<HashSet<T>> IRedisTypedClientAsync<T>.GetUnionFromSetsAsync(IRedisSet<T>[] sets, CancellationToken cancellationToken)
        {
            var multiDataList = await AsyncNative.SUnionAsync(sets.Map(x => x.Id).ToArray(), cancellationToken).ConfigureAwait(false);
            return CreateHashSet(multiDataList);
        }

        ValueTask IRedisTypedClientAsync<T>.StoreUnionFromSetsAsync(IRedisSet<T> intoSet, IRedisSet<T>[] sets, CancellationToken cancellationToken)
            => AsyncNative.SUnionStoreAsync(intoSet.Id, sets.Map(x => x.Id).ToArray(), cancellationToken);

        async ValueTask<HashSet<T>> IRedisTypedClientAsync<T>.GetDifferencesFromSetAsync(IRedisSet<T> fromSet, IRedisSet<T>[] withSets, CancellationToken cancellationToken)
        {
            var multiDataList = await AsyncNative.SDiffAsync(fromSet.Id, withSets.Map(x => x.Id).ToArray(), cancellationToken).ConfigureAwait(false);
            return CreateHashSet(multiDataList);
        }

        ValueTask IRedisTypedClientAsync<T>.StoreDifferencesFromSetAsync(IRedisSet<T> intoSet, IRedisSet<T> fromSet, IRedisSet<T>[] withSets, CancellationToken cancellationToken)
            => AsyncNative.SDiffStoreAsync(intoSet.Id, fromSet.Id, withSets.Map(x => x.Id).ToArray(), cancellationToken);

        ValueTask<T> IRedisTypedClientAsync<T>.GetRandomItemFromSetAsync(IRedisSet<T> fromSet, CancellationToken cancellationToken)
            => DeserializeValue(AsyncNative.SRandMemberAsync(fromSet.Id, cancellationToken));

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetAllItemsFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken)
        {
            var multiDataList = AsyncNative.LRangeAsync(fromList.Id, FirstElement, LastElement, cancellationToken);
            return CreateList(multiDataList);
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromListAsync(IRedisList<T> fromList, int startingFrom, int endingAt, CancellationToken cancellationToken)
        {
            var multiDataList = AsyncNative.LRangeAsync(fromList.Id, startingFrom, endingAt, cancellationToken);
            return CreateList(multiDataList);
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.SortListAsync(IRedisList<T> fromList, int startingFrom, int endingAt, CancellationToken cancellationToken)
        {
            var sortOptions = new SortOptions { Skip = startingFrom, Take = endingAt, };
            var multiDataList = AsyncNative.SortAsync(fromList.Id, sortOptions, cancellationToken);
            return CreateList(multiDataList);
        }

        ValueTask IRedisTypedClientAsync<T>.AddItemToListAsync(IRedisList<T> fromList, T value, CancellationToken cancellationToken)
            => AsyncNative.RPushAsync(fromList.Id, SerializeValue(value), cancellationToken).Await();

        ValueTask IRedisTypedClientAsync<T>.PrependItemToListAsync(IRedisList<T> fromList, T value, CancellationToken cancellationToken)
            => AsyncNative.LPushAsync(fromList.Id, SerializeValue(value), cancellationToken).Await();

        ValueTask<T> IRedisTypedClientAsync<T>.RemoveStartFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken)
            => DeserializeValue(AsyncNative.LPopAsync(fromList.Id, cancellationToken));

        async ValueTask<T> IRedisTypedClientAsync<T>.BlockingRemoveStartFromListAsync(IRedisList<T> fromList, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            var unblockingKeyAndValue = await AsyncNative.BLPopAsync(fromList.Id, (int)timeOut.GetValueOrDefault().TotalSeconds, cancellationToken).ConfigureAwait(false);
            return unblockingKeyAndValue.Length == 0
                ? default(T)
                : DeserializeValue(unblockingKeyAndValue[1]);
        }

        ValueTask<T> IRedisTypedClientAsync<T>.RemoveEndFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken)
            => DeserializeValue(AsyncNative.RPopAsync(fromList.Id, cancellationToken));

        ValueTask IRedisTypedClientAsync<T>.RemoveAllFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken)
            => AsyncNative.LTrimAsync(fromList.Id, int.MaxValue, FirstElement, cancellationToken);

        ValueTask IRedisTypedClientAsync<T>.TrimListAsync(IRedisList<T> fromList, int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken)
            => AsyncNative.LTrimAsync(fromList.Id, keepStartingFrom, keepEndingAt, cancellationToken);

        ValueTask<long> IRedisTypedClientAsync<T>.RemoveItemFromListAsync(IRedisList<T> fromList, T value, CancellationToken cancellationToken)
        {
            const int removeAll = 0;
            return AsyncNative.LRemAsync(fromList.Id, removeAll, SerializeValue(value), cancellationToken);
        }

        ValueTask<long> IRedisTypedClientAsync<T>.RemoveItemFromListAsync(IRedisList<T> fromList, T value, int noOfMatches, CancellationToken cancellationToken)
            => AsyncNative.LRemAsync(fromList.Id, noOfMatches, SerializeValue(value), cancellationToken);

        ValueTask<long> IRedisTypedClientAsync<T>.GetListCountAsync(IRedisList<T> fromList, CancellationToken cancellationToken)
            => AsyncNative.LLenAsync(fromList.Id, cancellationToken);

        ValueTask<T> IRedisTypedClientAsync<T>.GetItemFromListAsync(IRedisList<T> fromList, int listIndex, CancellationToken cancellationToken)
            => DeserializeValue(AsyncNative.LIndexAsync(fromList.Id, listIndex, cancellationToken));

        ValueTask IRedisTypedClientAsync<T>.SetItemInListAsync(IRedisList<T> toList, int listIndex, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.InsertBeforeItemInListAsync(IRedisList<T> toList, T pivot, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.InsertAfterItemInListAsync(IRedisList<T> toList, T pivot, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.EnqueueItemOnListAsync(IRedisList<T> fromList, T item, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.DequeueItemFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.BlockingDequeueItemFromListAsync(IRedisList<T> fromList, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.PushItemToListAsync(IRedisList<T> fromList, T item, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.PopItemFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.BlockingPopItemFromListAsync(IRedisList<T> fromList, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.PopAndPushItemBetweenListsAsync(IRedisList<T> fromList, IRedisList<T> toList, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.BlockingPopAndPushItemBetweenListsAsync(IRedisList<T> fromList, IRedisList<T> toList, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.AddItemToSortedSetAsync(IRedisSortedSet<T> toSet, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.AddItemToSortedSetAsync(IRedisSortedSet<T> toSet, T value, double score, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.RemoveItemFromSortedSetAsync(IRedisSortedSet<T> fromSet, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.PopItemWithLowestScoreFromSortedSetAsync(IRedisSortedSet<T> fromSet, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.PopItemWithHighestScoreFromSortedSetAsync(IRedisSortedSet<T> fromSet, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.SortedSetContainsItemAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisTypedClientAsync<T>.IncrementItemInSortedSetAsync(IRedisSortedSet<T> set, T value, double incrementBy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.GetItemIndexInSortedSetAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.GetItemIndexInSortedSetDescAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetAllItemsFromSortedSetAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetAllItemsFromSortedSetDescAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetDescAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetAllWithScoresFromSortedSetAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetDescAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<T, double>> IRedisTypedClientAsync<T>.GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.RemoveRangeFromSortedSetAsync(IRedisSortedSet<T> set, int minRank, int maxRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.RemoveRangeFromSortedSetByScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.GetSortedSetCountAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisTypedClientAsync<T>.GetItemScoreInSortedSetAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.StoreIntersectFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.StoreIntersectFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.StoreUnionFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.StoreUnionFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.HashContainsEntryAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.SetEntryInHashAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.SetEntryInHashIfNotExistsAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.SetRangeInHashAsync<TKey>(IRedisHash<TKey, T> hash, IEnumerable<KeyValuePair<TKey, T>> keyValuePairs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisTypedClientAsync<T>.GetValueFromHashAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisTypedClientAsync<T>.RemoveEntryFromHashAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.GetHashCountAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<TKey>> IRedisTypedClientAsync<T>.GetHashKeysAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetHashValuesAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<Dictionary<TKey, T>> IRedisTypedClientAsync<T>.GetAllEntriesFromHashAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.StoreRelatedEntitiesAsync<TChild>(object parentId, List<TChild> children, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.StoreRelatedEntitiesAsync<TChild>(object parentId, TChild[] children, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.DeleteRelatedEntitiesAsync<TChild>(object parentId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.DeleteRelatedEntityAsync<TChild>(object parentId, object childId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<TChild>> IRedisTypedClientAsync<T>.GetRelatedEntitiesAsync<TChild>(object parentId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisTypedClientAsync<T>.GetRelatedEntitiesCountAsync<TChild>(object parentId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisTypedClientAsync<T>.AddToRecentsListAsync(T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetLatestFromRecentsListAsync(int skip, int take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<T>> IRedisTypedClientAsync<T>.GetEarliestFromRecentsListAsync(int skip, int take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}