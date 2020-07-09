//
// https://github.com/ServiceStack/ServiceStack.Redis
// ServiceStack.Redis: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Demis Bellot (demis.bellot@gmail.com)
//
// Copyright 2017 ServiceStack, Inc. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Data;
using ServiceStack.Model;

namespace ServiceStack.Redis.Generic
{
    public interface IRedisTypedClientAsync<T> : IEntityStoreAsync<T>
    {
        //IHasNamed<IRedisList<T>> Lists { get; set; }
        //IHasNamed<IRedisSet<T>> Sets { get; set; }
        //IHasNamed<IRedisSortedSet<T>> SortedSets { get; set; }
        //IRedisHash<TKey, T> GetHashAsync<TKey>(string hashId, CancellationToken cancellationToken = default);
        //IRedisSet TypeIdsSet { get; }

        // not provided: use GetValueAsync/SetValueAsync instead
        // T this[string key] { get; set; }

        IRedisTypedTransaction<T> CreateTransactionAsync();
        IRedisTypedPipeline<T> CreatePipelineAsync();

        IRedisClientAsync RedisClient { get; }

        ValueTask<IAsyncDisposable> AcquireLockAsync(TimeSpan? timeOut = default, CancellationToken cancellationToken = default);

        long Db { get; }
        ValueTask ChangeDbAsync(long db, CancellationToken cancellationToken = default);

        ValueTask<List<string>> GetAllKeysAsync(CancellationToken cancellationToken = default);

        string UrnKey(T value);

        string SequenceKey { get; set; }
        ValueTask SetSequenceAsync(int value, CancellationToken cancellationToken = default);
        ValueTask<long> GetNextSequenceAsync(CancellationToken cancellationToken = default);
        ValueTask<long> GetNextSequenceAsync(int incrBy, CancellationToken cancellationToken = default);
        ValueTask<RedisKeyType> GetEntryTypeAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<string> GetRandomKeyAsync(CancellationToken cancellationToken = default);

        ValueTask SetValueAsync(string key, T entity, CancellationToken cancellationToken = default);
        ValueTask SetValueAsync(string key, T entity, TimeSpan expireIn, CancellationToken cancellationToken = default);
        ValueTask<bool> SetValueIfNotExistsAsync(string key, T entity, CancellationToken cancellationToken = default);
        ValueTask<bool> SetValueIfExistsAsync(string key, T entity, CancellationToken cancellationToken = default);

        ValueTask<T> StoreAsync(T entity, TimeSpan expireIn, CancellationToken cancellationToken = default);

        ValueTask<T> GetValueAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<T> GetAndSetValueAsync(string key, T value, CancellationToken cancellationToken = default);
        ValueTask<bool> ContainsKeyAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<bool> RemoveEntryAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<bool> RemoveEntryAsync(string[] args, CancellationToken cancellationToken = default);
        ValueTask<bool> RemoveEntryAsync(IHasStringId[] entities, CancellationToken cancellationToken = default);
        ValueTask<long> IncrementValueAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<long> IncrementValueByAsync(string key, int count, CancellationToken cancellationToken = default);
        ValueTask<long> DecrementValueAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<long> DecrementValueByAsync(string key, int count, CancellationToken cancellationToken = default);

        ValueTask<bool> ExpireInAsync(object id, TimeSpan expiresAt, CancellationToken cancellationToken = default);
        ValueTask<bool> ExpireAtAsync(object id, DateTime dateTime, CancellationToken cancellationToken = default);
        ValueTask<bool> ExpireEntryInAsync(string key, TimeSpan expiresAt, CancellationToken cancellationToken = default);
        ValueTask<bool> ExpireEntryAtAsync(string key, DateTime dateTime, CancellationToken cancellationToken = default);

        ValueTask<TimeSpan> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default);
        ValueTask ForegroundSaveAsync(CancellationToken cancellationToken = default);
        ValueTask BackgroundSaveAsync(CancellationToken cancellationToken = default);
        ValueTask FlushDbAsync(CancellationToken cancellationToken = default);
        ValueTask FlushAllAsync(CancellationToken cancellationToken = default);
        ValueTask<T[]> SearchKeysAsync(string pattern, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetValuesAsync(List<string> keys, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetSortedEntryValuesAsync(IRedisSet<T> fromSet, int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        ValueTask StoreAsHashAsync(T entity, CancellationToken cancellationToken = default);
        ValueTask<T> GetFromHashAsync(object id, CancellationToken cancellationToken = default);

        //Set operations
        ValueTask<HashSet<T>> GetAllItemsFromSetAsync(IRedisSet<T> fromSet, CancellationToken cancellationToken = default);
        ValueTask AddItemToSetAsync(IRedisSet<T> toSet, T item, CancellationToken cancellationToken = default);
        ValueTask RemoveItemFromSetAsync(IRedisSet<T> fromSet, T item, CancellationToken cancellationToken = default);
        ValueTask<T> PopItemFromSetAsync(IRedisSet<T> fromSet, CancellationToken cancellationToken = default);
        ValueTask MoveBetweenSetsAsync(IRedisSet<T> fromSet, IRedisSet<T> toSet, T item, CancellationToken cancellationToken = default);
        ValueTask<long> GetSetCountAsync(IRedisSet<T> set, CancellationToken cancellationToken = default);
        ValueTask<bool> SetContainsItemAsync(IRedisSet<T> set, T item, CancellationToken cancellationToken = default);
        ValueTask<HashSet<T>> GetIntersectFromSetsAsync(IRedisSet<T>[] sets, CancellationToken cancellationToken = default);
        ValueTask StoreIntersectFromSetsAsync(IRedisSet<T> intoSet, IRedisSet<T>[] sets, CancellationToken cancellationToken = default);
        ValueTask<HashSet<T>> GetUnionFromSetsAsync(IRedisSet<T>[] sets, CancellationToken cancellationToken = default);
        ValueTask StoreUnionFromSetsAsync(IRedisSet<T> intoSet, IRedisSet<T>[] sets, CancellationToken cancellationToken = default);
        ValueTask<HashSet<T>> GetDifferencesFromSetAsync(IRedisSet<T> fromSet, IRedisSet<T>[] withSets, CancellationToken cancellationToken = default);
        ValueTask StoreDifferencesFromSetAsync(IRedisSet<T> intoSet, IRedisSet<T> fromSet, IRedisSet<T>[] withSets, CancellationToken cancellationToken = default);
        ValueTask<T> GetRandomItemFromSetAsync(IRedisSet<T> fromSet, CancellationToken cancellationToken = default);

        //List operations
        ValueTask<List<T>> GetAllItemsFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromListAsync(IRedisList<T> fromList, int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        ValueTask<List<T>> SortListAsync(IRedisList<T> fromList, int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        ValueTask AddItemToListAsync(IRedisList<T> fromList, T value, CancellationToken cancellationToken = default);
        ValueTask PrependItemToListAsync(IRedisList<T> fromList, T value, CancellationToken cancellationToken = default);
        ValueTask<T> RemoveStartFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken = default);
        ValueTask<T> BlockingRemoveStartFromListAsync(IRedisList<T> fromList, TimeSpan? timeOut, CancellationToken cancellationToken = default);
        ValueTask<T> RemoveEndFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken = default);
        ValueTask RemoveAllFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken = default);
        ValueTask TrimListAsync(IRedisList<T> fromList, int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken = default);
        ValueTask<long> RemoveItemFromListAsync(IRedisList<T> fromList, T value, CancellationToken cancellationToken = default);
        ValueTask<long> RemoveItemFromListAsync(IRedisList<T> fromList, T value, int noOfMatches, CancellationToken cancellationToken = default);
        ValueTask<long> GetListCountAsync(IRedisList<T> fromList, CancellationToken cancellationToken = default);
        ValueTask<T> GetItemFromListAsync(IRedisList<T> fromList, int listIndex, CancellationToken cancellationToken = default);
        ValueTask SetItemInListAsync(IRedisList<T> toList, int listIndex, T value, CancellationToken cancellationToken = default);
        ValueTask InsertBeforeItemInListAsync(IRedisList<T> toList, T pivot, T value, CancellationToken cancellationToken = default);
        ValueTask InsertAfterItemInListAsync(IRedisList<T> toList, T pivot, T value, CancellationToken cancellationToken = default);

        //Queue operations
        ValueTask EnqueueItemOnListAsync(IRedisList<T> fromList, T item, CancellationToken cancellationToken = default);
        ValueTask<T> DequeueItemFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken = default);
        ValueTask<T> BlockingDequeueItemFromListAsync(IRedisList<T> fromList, TimeSpan? timeOut, CancellationToken cancellationToken = default);

        //Stack operations
        ValueTask PushItemToListAsync(IRedisList<T> fromList, T item, CancellationToken cancellationToken = default);
        ValueTask<T> PopItemFromListAsync(IRedisList<T> fromList, CancellationToken cancellationToken = default);
        ValueTask<T> BlockingPopItemFromListAsync(IRedisList<T> fromList, TimeSpan? timeOut, CancellationToken cancellationToken = default);
        ValueTask<T> PopAndPushItemBetweenListsAsync(IRedisList<T> fromList, IRedisList<T> toList, CancellationToken cancellationToken = default);
        ValueTask<T> BlockingPopAndPushItemBetweenListsAsync(IRedisList<T> fromList, IRedisList<T> toList, TimeSpan? timeOut, CancellationToken cancellationToken = default);

        //Sorted Set operations
        ValueTask AddItemToSortedSetAsync(IRedisSortedSet<T> toSet, T value, CancellationToken cancellationToken = default);
        ValueTask AddItemToSortedSetAsync(IRedisSortedSet<T> toSet, T value, double score, CancellationToken cancellationToken = default);
        ValueTask<bool> RemoveItemFromSortedSetAsync(IRedisSortedSet<T> fromSet, T value, CancellationToken cancellationToken = default);
        ValueTask<T> PopItemWithLowestScoreFromSortedSetAsync(IRedisSortedSet<T> fromSet, CancellationToken cancellationToken = default);
        ValueTask<T> PopItemWithHighestScoreFromSortedSetAsync(IRedisSortedSet<T> fromSet, CancellationToken cancellationToken = default);
        ValueTask<bool> SortedSetContainsItemAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken = default);
        ValueTask<double> IncrementItemInSortedSetAsync(IRedisSortedSet<T> set, T value, double incrementBy, CancellationToken cancellationToken = default);
        ValueTask<long> GetItemIndexInSortedSetAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken = default);
        ValueTask<long> GetItemIndexInSortedSetDescAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetAllItemsFromSortedSetAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetAllItemsFromSortedSetDescAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetDescAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetAllWithScoresFromSortedSetAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetDescAsync(IRedisSortedSet<T> set, int fromRank, int toRank, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByLowestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetRangeFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken = default);
        ValueTask<IDictionary<T, double>> GetRangeWithScoresFromSortedSetByHighestScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        ValueTask<long> RemoveRangeFromSortedSetAsync(IRedisSortedSet<T> set, int minRank, int maxRank, CancellationToken cancellationToken = default);
        ValueTask<long> RemoveRangeFromSortedSetByScoreAsync(IRedisSortedSet<T> set, double fromScore, double toScore, CancellationToken cancellationToken = default);
        ValueTask<long> GetSortedSetCountAsync(IRedisSortedSet<T> set, CancellationToken cancellationToken = default);
        ValueTask<double> GetItemScoreInSortedSetAsync(IRedisSortedSet<T> set, T value, CancellationToken cancellationToken = default);
        ValueTask<long> StoreIntersectFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, CancellationToken cancellationToken = default);
        ValueTask<long> StoreIntersectFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, string[] args, CancellationToken cancellationToken = default);
        ValueTask<long> StoreUnionFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, CancellationToken cancellationToken = default);
        ValueTask<long> StoreUnionFromSortedSetsAsync(IRedisSortedSet<T> intoSetId, IRedisSortedSet<T>[] setIds, string[] args, CancellationToken cancellationToken = default);

        //Hash operations
        ValueTask<bool> HashContainsEntryAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, CancellationToken cancellationToken = default);
        ValueTask<bool> SetEntryInHashAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, T value, CancellationToken cancellationToken = default);
        ValueTask<bool> SetEntryInHashIfNotExistsAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, T value, CancellationToken cancellationToken = default);
        ValueTask SetRangeInHashAsync<TKey>(IRedisHash<TKey, T> hash, IEnumerable<KeyValuePair<TKey, T>> keyValuePairs, CancellationToken cancellationToken = default);
        ValueTask<T> GetValueFromHashAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, CancellationToken cancellationToken = default);
        ValueTask<bool> RemoveEntryFromHashAsync<TKey>(IRedisHash<TKey, T> hash, TKey key, CancellationToken cancellationToken = default);
        ValueTask<long> GetHashCountAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken = default);
        ValueTask<List<TKey>> GetHashKeysAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetHashValuesAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken = default);
        ValueTask<Dictionary<TKey, T>> GetAllEntriesFromHashAsync<TKey>(IRedisHash<TKey, T> hash, CancellationToken cancellationToken = default);

        //Useful common app-logic 
        ValueTask StoreRelatedEntitiesAsync<TChild>(object parentId, List<TChild> children, CancellationToken cancellationToken = default);
        ValueTask StoreRelatedEntitiesAsync<TChild>(object parentId, TChild[] children, CancellationToken cancellationToken = default);
        ValueTask DeleteRelatedEntitiesAsync<TChild>(object parentId, CancellationToken cancellationToken = default);
        ValueTask DeleteRelatedEntityAsync<TChild>(object parentId, object childId, CancellationToken cancellationToken = default);
        ValueTask<List<TChild>> GetRelatedEntitiesAsync<TChild>(object parentId, CancellationToken cancellationToken = default);
        ValueTask<long> GetRelatedEntitiesCountAsync<TChild>(object parentId, CancellationToken cancellationToken = default);
        ValueTask AddToRecentsListAsync(T value, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetLatestFromRecentsListAsync(int skip, int take, CancellationToken cancellationToken = default);
        ValueTask<List<T>> GetEarliestFromRecentsListAsync(int skip, int take, CancellationToken cancellationToken = default);
    }

}