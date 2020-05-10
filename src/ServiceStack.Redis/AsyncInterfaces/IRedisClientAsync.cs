//
// https://github.com/ServiceStack/ServiceStack.Redis/
// ServiceStack.Redis: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Demis Bellot Async(demis.bellot@gmail.com)
//
// Copyright 2017 ServiceStack, Inc. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//

using System;
using System.Collections.Generic;
using ServiceStack.Caching;
using ServiceStack.Data;
using ServiceStack.Model;
using ServiceStack.Redis.Generic;
using ServiceStack.Redis.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    public interface IRedisClientAsync
        : IEntityStoreAsync, ICacheClientExtendedAsync, IRemoveByPatternAsync
    {
        /* non-obvious changes from IRedisClient:
        - Db is read-only; added ChangeDbAsync for setting
        - added unary version of RemoveEntryAsync - because of losing "params", usage was awkward otherwise
        - added GetTimeToLiveAsync - not available on the interface
        */
        //Basic Redis Connection operations
        long Db { get; }
        ValueTask ChangeDbAsync(long db, CancellationToken cancellationToken = default);
        // ValueTask<long> DbSizeAsync(CancellationToken cancellationToken = default);

        //Dictionary<string, string> Info { get; }
        ValueTask<DateTime> GetServerTimeAsync(CancellationToken cancellationToken = default);
        //DateTime LastSave { get; }
        string Host { get; }
        int Port { get; }
        int ConnectTimeout { get; set; }
        int RetryTimeout { get; set; }
        int RetryCount { get; set; }
        int SendTimeout { get; set; }
        string Password { get; set; }
        bool HadExceptions { get; }

        ValueTask<bool> PingAsync(CancellationToken cancellationToken = default);
        ValueTask<string> EchoAsync(string text, CancellationToken cancellationToken = default);

        //RedisText CustomAsync(params object[] cmdWithArgs, CancellationToken cancellationToken = default);

        //ValueTask SaveAsync(CancellationToken cancellationToken = default);
        //ValueTask SaveAsync(CancellationToken cancellationToken = default);
        //ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
        //ValueTask ShutdownNoSaveAsync(CancellationToken cancellationToken = default);
        //ValueTask RewriteAppendOnlyFileAsync(CancellationToken cancellationToken = default);
        //ValueTask FlushDbAsync(CancellationToken cancellationToken = default);

        //RedisServerRole GetServerRoleAsync(CancellationToken cancellationToken = default);
        //RedisText GetServerRoleInfoAsync(CancellationToken cancellationToken = default);
        //ValueTask<string> GetConfigAsync(string item, CancellationToken cancellationToken = default);
        //ValueTask SetConfigAsync(string item, string value, CancellationToken cancellationToken = default);
        //ValueTask SaveConfigAsync(CancellationToken cancellationToken = default);
        //ValueTask ResetInfoStatsAsync(CancellationToken cancellationToken = default);

        //ValueTask<string> GetClientAsync(CancellationToken cancellationToken = default);
        //ValueTask SetClientAsync(string name, CancellationToken cancellationToken = default);
        //ValueTask KillClientAsync(string address, CancellationToken cancellationToken = default);
        //ValueTask<long> KillClientsAsync(string fromAddress = null, string withId = null, RedisClientType? ofType = null, bool? skipMe = null, CancellationToken cancellationToken = default);
        //List<Dictionary<string, string>> GetClientsInfoAsync(CancellationToken cancellationToken = default);
        //ValueTask PauseAllClientsAsync(TimeSpan duration, CancellationToken cancellationToken = default);

        ////Basic Redis Connection Info
        //ValueTask<string> this[string key] { get; set; }

        //List<string> GetAllKeysAsync(CancellationToken cancellationToken = default);

        ////Fetch fully qualified key for specific Type and Id
        //ValueTask<string> UrnKey<T>Async(T value, CancellationToken cancellationToken = default);
        //ValueTask<string> UrnKey<T>Async(object id, CancellationToken cancellationToken = default);
        //ValueTask<string> UrnKeyAsync(Type type, object id, CancellationToken cancellationToken = default);

        //ValueTask SetAllAsync(IEnumerable<string> keys, IEnumerable<string> values, CancellationToken cancellationToken = default);
        ValueTask SetAllAsync(Dictionary<string, string> map, CancellationToken cancellationToken = default);
        //ValueTask SetValuesAsync(Dictionary<string, string> map, CancellationToken cancellationToken = default);

        ValueTask SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
        //ValueTask SetValueAsync(string key, string value, TimeSpan expireIn, CancellationToken cancellationToken = default);
        //ValueTask<bool> SetValueIfNotExistsAsync(string key, string value, CancellationToken cancellationToken = default);
        //ValueTask<bool> SetValueIfNotExistsAsync(string key, string value, TimeSpan expireIn, CancellationToken cancellationToken = default);
        //ValueTask<bool> SetValueIfExistsAsync(string key, string value, CancellationToken cancellationToken = default);
        //ValueTask<bool> SetValueIfExistsAsync(string key, string value, TimeSpan expireIn, CancellationToken cancellationToken = default);

        ValueTask<string> GetValueAsync(string key, CancellationToken cancellationToken = default);
        //ValueTask<string> GetAndSetValueAsync(string key, string value, CancellationToken cancellationToken = default);

        //List<string> GetValuesAsync(List<string> keys, CancellationToken cancellationToken = default);
        //List<T> GetValues<T>Async(List<string> keys, CancellationToken cancellationToken = default);
        //Dictionary<string, string> GetValuesMapAsync(List<string> keys, CancellationToken cancellationToken = default);
        //Dictionary<string, T> GetValuesMap<T>Async(List<string> keys, CancellationToken cancellationToken = default);
        //ValueTask<long> AppendToValueAsync(string key, string value, CancellationToken cancellationToken = default);
        ValueTask RenameKeyAsync(string fromName, string toName, CancellationToken cancellationToken = default);

        ////store POCOs as hash
        //T GetFromHash<T>Async(object id, CancellationToken cancellationToken = default);
        //ValueTask StoreAsHash<T>Async(T entity, CancellationToken cancellationToken = default);

        //object StoreObjectAsync(object entity, CancellationToken cancellationToken = default);

        ValueTask<bool> ContainsKeyAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<bool> RemoveEntryAsync(string[] keys, CancellationToken cancellationToken = default);
        ValueTask<bool> RemoveEntryAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<long> IncrementValueAsync(string key, CancellationToken cancellationToken = default);
        //ValueTask<long> IncrementValueByAsync(string key, int count, CancellationToken cancellationToken = default);
        //ValueTask<long> IncrementValueByAsync(string key, long count, CancellationToken cancellationToken = default);
        //double IncrementValueByAsync(string key, double count, CancellationToken cancellationToken = default);
        //ValueTask<long> DecrementValueAsync(string key, CancellationToken cancellationToken = default);
        //ValueTask<long> DecrementValueByAsync(string key, int count, CancellationToken cancellationToken = default);
        ValueTask<List<string>> SearchKeysAsync(string pattern, CancellationToken cancellationToken = default);

        //ValueTask<string> TypeAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<RedisKeyType> GetEntryTypeAsync(string key, CancellationToken cancellationToken = default);
        //ValueTask<long> GetStringCountAsync(string key, CancellationToken cancellationToken = default);
        ValueTask<string> GetRandomKeyAsync(CancellationToken cancellationToken = default);
        ValueTask<bool> ExpireEntryInAsync(string key, TimeSpan expireIn, CancellationToken cancellationToken = default);
        ValueTask<bool> ExpireEntryAtAsync(string key, DateTime expireAt, CancellationToken cancellationToken = default);
        ValueTask<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default);
        //List<string> GetSortedEntryValuesAsync(string key, int startingFrom, int endingAt, CancellationToken cancellationToken = default);

        ////Store entities without registering entity ids
        //ValueTask WriteAll<TEntity>Async(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        ////Scan APIs
        IAsyncEnumerable<string> ScanAllKeysAsync(string pattern = null, int pageSize = 1000, CancellationToken cancellationToken = default);
        //IEnumerable<string> ScanAllSetItemsAsync(string setId, string pattern = null, int pageSize = 1000, CancellationToken cancellationToken = default);
        //IEnumerable<KeyValuePair<string, double>> ScanAllSortedSetItemsAsync(string setId, string pattern = null, int pageSize = 1000, CancellationToken cancellationToken = default);
        //IEnumerable<KeyValuePair<string, string>> ScanAllHashEntriesAsync(string hashId, string pattern = null, int pageSize = 1000, CancellationToken cancellationToken = default);

        ////Hyperlog APIs
        //ValueTask<bool> AddToHyperLogAsync(string key, params string[] elements, CancellationToken cancellationToken = default);
        //ValueTask<long> CountHyperLogAsync(string key, CancellationToken cancellationToken = default);
        //ValueTask MergeHyperLogsAsync(string toKey, params string[] fromKeys, CancellationToken cancellationToken = default);

        ////GEO APIs
        //ValueTask<long> AddGeoMemberAsync(string key, double longitude, double latitude, string member, CancellationToken cancellationToken = default);
        //ValueTask<long> AddGeoMembersAsync(string key, params RedisGeo[] geoPoints, CancellationToken cancellationToken = default);
        //double CalculateDistanceBetweenGeoMembersAsync(string key, string fromMember, string toMember, string unit = null, CancellationToken cancellationToken = default);
        //string[] GetGeohashesAsync(string key, params string[] members, CancellationToken cancellationToken = default);
        //List<RedisGeo> GetGeoCoordinatesAsync(string key, params string[] members, CancellationToken cancellationToken = default);
        //string[] FindGeoMembersInRadiusAsync(string key, double longitude, double latitude, double radius, string unit, CancellationToken cancellationToken = default);
        //List<RedisGeoResult> FindGeoResultsInRadiusAsync(string key, double longitude, double latitude, double radius, string unit, int? count = null, bool? sortByNearest = null, CancellationToken cancellationToken = default);
        //string[] FindGeoMembersInRadiusAsync(string key, string member, double radius, string unit, CancellationToken cancellationToken = default);
        //List<RedisGeoResult> FindGeoResultsInRadiusAsync(string key, string member, double radius, string unit, int? count = null, bool? sortByNearest = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a high-level typed client API
        /// </summary>
        /// <typeparam name="T"></typeparam>
        IRedisTypedClientAsync<T> As<T>();

        //IHasNamed<IRedisList> Lists { get; set; }
        //IHasNamed<IRedisSet> Sets { get; set; }
        //IHasNamed<IRedisSortedSet> SortedSets { get; set; }
        //IHasNamed<IRedisHash> Hashes { get; set; }

        ValueTask<IRedisTransactionAsync> CreateTransactionAsync(CancellationToken cancellationToken = default);
        ValueTask<IRedisPipelineAsync> CreatePipelineAsync(CancellationToken cancellationToken = default);

        //IDisposable AcquireLockAsync(string key, CancellationToken cancellationToken = default);
        //IDisposable AcquireLockAsync(string key, TimeSpan timeOut, CancellationToken cancellationToken = default);

        //#region Redis pubsub

        //ValueTask WatchAsync(params string[] keys, CancellationToken cancellationToken = default);
        //ValueTask UnWatchAsync(CancellationToken cancellationToken = default);
        //IRedisSubscription CreateSubscriptionAsync(CancellationToken cancellationToken = default);
        //ValueTask<long> PublishMessageAsync(string toChannel, string message, CancellationToken cancellationToken = default);

        //#endregion


        //#region Set operations

        //HashSet<string> GetAllItemsFromSetAsync(string setId, CancellationToken cancellationToken = default);
        ValueTask AddItemToSetAsync(string setId, string item, CancellationToken cancellationToken = default);
        //ValueTask AddRangeToSetAsync(string setId, List<string> items, CancellationToken cancellationToken = default);
        //ValueTask RemoveItemFromSetAsync(string setId, string item, CancellationToken cancellationToken = default);
        //ValueTask<string> PopItemFromSetAsync(string setId, CancellationToken cancellationToken = default);
        //List<string> PopItemsFromSetAsync(string setId, int count, CancellationToken cancellationToken = default);
        //ValueTask MoveBetweenSetsAsync(string fromSetId, string toSetId, string item, CancellationToken cancellationToken = default);
        //ValueTask<long> GetSetCountAsync(string setId, CancellationToken cancellationToken = default);
        //ValueTask<bool> SetContainsItemAsync(string setId, string item, CancellationToken cancellationToken = default);
        //HashSet<string> GetIntersectFromSetsAsync(params string[] setIds, CancellationToken cancellationToken = default);
        //ValueTask StoreIntersectFromSetsAsync(string intoSetId, params string[] setIds, CancellationToken cancellationToken = default);
        //HashSet<string> GetUnionFromSetsAsync(params string[] setIds, CancellationToken cancellationToken = default);
        //ValueTask StoreUnionFromSetsAsync(string intoSetId, params string[] setIds, CancellationToken cancellationToken = default);
        //HashSet<string> GetDifferencesFromSetAsync(string fromSetId, params string[] withSetIds, CancellationToken cancellationToken = default);
        //ValueTask StoreDifferencesFromSetAsync(string intoSetId, string fromSetId, params string[] withSetIds, CancellationToken cancellationToken = default);
        //ValueTask<string> GetRandomItemFromSetAsync(string setId, CancellationToken cancellationToken = default);

        //#endregion


        //#region List operations

        //List<string> GetAllItemsFromListAsync(string listId, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromListAsync(string listId, int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedListAsync(string listId, int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        //List<string> GetSortedItemsFromListAsync(string listId, SortOptions sortOptions, CancellationToken cancellationToken = default);
        ValueTask AddItemToListAsync(string listId, string value, CancellationToken cancellationToken = default);
        //ValueTask AddRangeToListAsync(string listId, List<string> values, CancellationToken cancellationToken = default);
        //ValueTask PrependItemToListAsync(string listId, string value, CancellationToken cancellationToken = default);
        //ValueTask PrependRangeToListAsync(string listId, List<string> values, CancellationToken cancellationToken = default);

        //ValueTask RemoveAllFromListAsync(string listId, CancellationToken cancellationToken = default);
        //ValueTask<string> RemoveStartFromListAsync(string listId, CancellationToken cancellationToken = default);
        //ValueTask<string> BlockingRemoveStartFromListAsync(string listId, TimeSpan? timeOut, CancellationToken cancellationToken = default);
        //ItemRef BlockingRemoveStartFromListsAsync(string[] listIds, TimeSpan? timeOut, CancellationToken cancellationToken = default);
        //ValueTask<string> RemoveEndFromListAsync(string listId, CancellationToken cancellationToken = default);
        //ValueTask TrimListAsync(string listId, int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken = default);
        //ValueTask<long> RemoveItemFromListAsync(string listId, string value, CancellationToken cancellationToken = default);
        //ValueTask<long> RemoveItemFromListAsync(string listId, string value, int noOfMatches, CancellationToken cancellationToken = default);
        //ValueTask<long> GetListCountAsync(string listId, CancellationToken cancellationToken = default);
        //ValueTask<string> GetItemFromListAsync(string listId, int listIndex, CancellationToken cancellationToken = default);
        //ValueTask SetItemInListAsync(string listId, int listIndex, string value, CancellationToken cancellationToken = default);

        ////Queue operations
        //ValueTask EnqueueItemOnListAsync(string listId, string value, CancellationToken cancellationToken = default);
        //ValueTask<string> DequeueItemFromListAsync(string listId, CancellationToken cancellationToken = default);
        //ValueTask<string> BlockingDequeueItemFromListAsync(string listId, TimeSpan? timeOut, CancellationToken cancellationToken = default);
        //ItemRef BlockingDequeueItemFromListsAsync(string[] listIds, TimeSpan? timeOut, CancellationToken cancellationToken = default);

        ////Stack operations
        //ValueTask PushItemToListAsync(string listId, string value, CancellationToken cancellationToken = default);
        //ValueTask<string> PopItemFromListAsync(string listId, CancellationToken cancellationToken = default);
        //ValueTask<string> BlockingPopItemFromListAsync(string listId, TimeSpan? timeOut, CancellationToken cancellationToken = default);
        //ItemRef BlockingPopItemFromListsAsync(string[] listIds, TimeSpan? timeOut, CancellationToken cancellationToken = default);
        //ValueTask<string> PopAndPushItemBetweenListsAsync(string fromListId, string toListId, CancellationToken cancellationToken = default);
        //ValueTask<string> BlockingPopAndPushItemBetweenListsAsync(string fromListId, string toListId, TimeSpan? timeOut, CancellationToken cancellationToken = default);

        //#endregion


        //#region Sorted Set operations

        ValueTask<bool> AddItemToSortedSetAsync(string setId, string value, CancellationToken cancellationToken = default);
        ValueTask<bool> AddItemToSortedSetAsync(string setId, string value, double score, CancellationToken cancellationToken = default);
        //ValueTask<bool> AddRangeToSortedSetAsync(string setId, List<string> values, double score, CancellationToken cancellationToken = default);
        //ValueTask<bool> AddRangeToSortedSetAsync(string setId, List<string> values, long score, CancellationToken cancellationToken = default);
        //ValueTask<bool> RemoveItemFromSortedSetAsync(string setId, string value, CancellationToken cancellationToken = default);
        //ValueTask<long> RemoveItemsFromSortedSetAsync(string setId, List<string> values, CancellationToken cancellationToken = default);
        //ValueTask<string> PopItemWithLowestScoreFromSortedSetAsync(string setId, CancellationToken cancellationToken = default);
        //ValueTask<string> PopItemWithHighestScoreFromSortedSetAsync(string setId, CancellationToken cancellationToken = default);
        //ValueTask<bool> SortedSetContainsItemAsync(string setId, string value, CancellationToken cancellationToken = default);
        //double IncrementItemInSortedSetAsync(string setId, string value, double incrementBy, CancellationToken cancellationToken = default);
        //double IncrementItemInSortedSetAsync(string setId, string value, long incrementBy, CancellationToken cancellationToken = default);
        //ValueTask<long> GetItemIndexInSortedSetAsync(string setId, string value, CancellationToken cancellationToken = default);
        //ValueTask<long> GetItemIndexInSortedSetDescAsync(string setId, string value, CancellationToken cancellationToken = default);
        //List<string> GetAllItemsFromSortedSetAsync(string setId, CancellationToken cancellationToken = default);
        //List<string> GetAllItemsFromSortedSetDescAsync(string setId, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetDescAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetAllWithScoresFromSortedSetAsync(string setId, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetDescAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //List<string> GetRangeFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //IDictionary<string, double> GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken = default);
        //ValueTask<long> RemoveRangeFromSortedSetAsync(string setId, int minRank, int maxRank, CancellationToken cancellationToken = default);
        //ValueTask<long> RemoveRangeFromSortedSetByScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken = default);
        //ValueTask<long> RemoveRangeFromSortedSetByScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken = default);
        //ValueTask<long> GetSortedSetCountAsync(string setId, CancellationToken cancellationToken = default);
        //ValueTask<long> GetSortedSetCountAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken = default);
        //ValueTask<long> GetSortedSetCountAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken = default);
        //ValueTask<long> GetSortedSetCountAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken = default);
        //double GetItemScoreInSortedSetAsync(string setId, string value, CancellationToken cancellationToken = default);
        //ValueTask<long> StoreIntersectFromSortedSetsAsync(string intoSetId, params string[] setIds, CancellationToken cancellationToken = default);
        //ValueTask<long> StoreIntersectFromSortedSetsAsync(string intoSetId, string[] setIds, string[] args, CancellationToken cancellationToken = default);
        //ValueTask<long> StoreUnionFromSortedSetsAsync(string intoSetId, params string[] setIds, CancellationToken cancellationToken = default);
        //ValueTask<long> StoreUnionFromSortedSetsAsync(string intoSetId, string[] setIds, string[] args, CancellationToken cancellationToken = default);
        //List<string> SearchSortedSetAsync(string setId, string start = null, string end = null, int? skip = null, int? take = null, CancellationToken cancellationToken = default);
        //ValueTask<long> SearchSortedSetCountAsync(string setId, string start = null, string end = null, CancellationToken cancellationToken = default);
        //ValueTask<long> RemoveRangeFromSortedSetBySearchAsync(string setId, string start = null, string end = null, CancellationToken cancellationToken = default);

        //#endregion


        //#region Hash operations

        //ValueTask<bool> HashContainsEntryAsync(string hashId, string key, CancellationToken cancellationToken = default);
        ValueTask<bool> SetEntryInHashAsync(string hashId, string key, string value, CancellationToken cancellationToken = default);
        //ValueTask<bool> SetEntryInHashIfNotExistsAsync(string hashId, string key, string value, CancellationToken cancellationToken = default);
        //ValueTask SetRangeInHashAsync(string hashId, IEnumerable<KeyValuePair<string, string>> keyValuePairs, CancellationToken cancellationToken = default);
        //ValueTask<long> IncrementValueInHashAsync(string hashId, string key, int incrementBy, CancellationToken cancellationToken = default);
        //double IncrementValueInHashAsync(string hashId, string key, double incrementBy, CancellationToken cancellationToken = default);
        //ValueTask<string> GetValueFromHashAsync(string hashId, string key, CancellationToken cancellationToken = default);
        //List<string> GetValuesFromHashAsync(string hashId, params string[] keys, CancellationToken cancellationToken = default);
        //ValueTask<bool> RemoveEntryFromHashAsync(string hashId, string key, CancellationToken cancellationToken = default);
        //ValueTask<long> GetHashCountAsync(string hashId, CancellationToken cancellationToken = default);
        //List<string> GetHashKeysAsync(string hashId, CancellationToken cancellationToken = default);
        //List<string> GetHashValuesAsync(string hashId, CancellationToken cancellationToken = default);
        //Dictionary<string, string> GetAllEntriesFromHashAsync(string hashId, CancellationToken cancellationToken = default);

        //#endregion


        //#region Eval/Lua operations

        //T ExecCachedLua<T>Async(string scriptBody, Func<string, T> scriptSha1, CancellationToken cancellationToken = default);

        //RedisText ExecLuaAsync(string body, params string[] args, CancellationToken cancellationToken = default);
        //RedisText ExecLuaAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken = default);
        //RedisText ExecLuaShaAsync(string sha1, params string[] args, CancellationToken cancellationToken = default);
        //RedisText ExecLuaShaAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken = default);

        //ValueTask<string> ExecLuaAsStringAsync(string luaBody, params string[] args, CancellationToken cancellationToken = default);
        //ValueTask<string> ExecLuaAsStringAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken = default);
        //ValueTask<string> ExecLuaShaAsStringAsync(string sha1, params string[] args, CancellationToken cancellationToken = default);
        //ValueTask<string> ExecLuaShaAsStringAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken = default);

        //ValueTask<long> ExecLuaAsIntAsync(string luaBody, params string[] args, CancellationToken cancellationToken = default);
        //ValueTask<long> ExecLuaAsIntAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken = default);
        //ValueTask<long> ExecLuaShaAsIntAsync(string sha1, params string[] args, CancellationToken cancellationToken = default);
        //ValueTask<long> ExecLuaShaAsIntAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken = default);

        //List<string> ExecLuaAsListAsync(string luaBody, params string[] args, CancellationToken cancellationToken = default);
        //List<string> ExecLuaAsListAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken = default);
        //List<string> ExecLuaShaAsListAsync(string sha1, params string[] args, CancellationToken cancellationToken = default);
        //List<string> ExecLuaShaAsListAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken = default);

        //ValueTask<string> CalculateSha1Async(string luaBody, CancellationToken cancellationToken = default);

        //ValueTask<bool> HasLuaScriptAsync(string sha1Ref, CancellationToken cancellationToken = default);
        //Dictionary<string, bool> WhichLuaScriptsExistsAsync(params string[] sha1Refs, CancellationToken cancellationToken = default);
        //ValueTask RemoveAllLuaScriptsAsync(CancellationToken cancellationToken = default);
        //ValueTask KillRunningLuaScriptAsync(CancellationToken cancellationToken = default);
        //ValueTask<string> LoadLuaScriptAsync(string body, CancellationToken cancellationToken = default);

        //#endregion
    }
}