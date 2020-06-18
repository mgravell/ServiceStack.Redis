//
// https://github.com/ServiceStack/ServiceStack.Redis/
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
using ServiceStack.Data;
using ServiceStack.Redis.Generic;
using ServiceStack.Redis.Pipeline;
using ServiceStack.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisClient : IRedisClientAsync, IRemoveByPatternAsync, ICacheClientAsync
    {
        /// <summary>
        /// Access this instance for async usage
        /// </summary>
        public IRedisClientAsync AsAsync() => this;

        // the typed client implements this for us
        IRedisTypedClientAsync<T> IRedisClientAsync.As<T>() => (IRedisTypedClientAsync<T>)As<T>();

        // convenience since we're not saturating the public API; this makes it easy to call
        // the explicit interface implementations; the JIT should make this a direct call
        private IRedisNativeClientAsync NativeAsync => this;

        internal ValueTask RegisterTypeIdAsync(string typeIdsSetKey, string id, CancellationToken cancellationToken)
        {
            if (this.Pipeline != null)
            {
                var registeredTypeIdsWithinPipeline = GetRegisteredTypeIdsWithinPipeline(typeIdsSetKey);
                registeredTypeIdsWithinPipeline.Add(id);
                return default;
            }
            else
            {
                return AsAsync().AddItemToSetAsync(typeIdsSetKey, id, cancellationToken);
            }
        }


        async ValueTask<DateTime> IRedisClientAsync.GetServerTimeAsync(CancellationToken cancellationToken)
        {
            var parts = await NativeAsync.TimeAsync(cancellationToken).ConfigureAwait(false);
            return ParseTimeResult(parts);
        }

        ValueTask<long> IRedisClientAsync.IncrementValueAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.IncrAsync(key, cancellationToken);

        ValueTask<IRedisPipelineAsync> IRedisClientAsync.CreatePipelineAsync(CancellationToken cancellationToken)
            => new ValueTask<IRedisPipelineAsync>(new RedisAllPurposePipeline(this));

        ValueTask<IRedisTransactionAsync> IRedisClientAsync.CreateTransactionAsync(CancellationToken cancellationToken)
        {
            AssertServerVersionNumber(); // pre-fetch call to INFO before transaction if needed
            return new ValueTask<IRedisTransactionAsync>(new RedisTransaction(this, true)); // note that the MULTI here will be held and flushed async
        }

        ValueTask<bool> IRedisClientAsync.RemoveEntryAsync(string[] keys, CancellationToken cancellationToken)
            => keys.Length == 0 ? default : IsSuccess(NativeAsync.DelAsync(keys, cancellationToken));



        private async ValueTask ExecAsync(Func<IRedisClientAsync, ValueTask> action)
        {
            using (JsConfig.With(new Text.Config { ExcludeTypeInfo = false }))
            {
                await action(this).ConfigureAwait(false);
            }
        }

        private async ValueTask<T> ExecAsync<T>(Func<IRedisClientAsync, ValueTask<T>> action)
        {
            using (JsConfig.With(new Text.Config { ExcludeTypeInfo = false }))
            {
                return await action(this).ConfigureAwait(false);
            }
        }

        ValueTask IRedisClientAsync.SetValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            var bytesValue = value?.ToUtf8Bytes();
            return NativeAsync.SetAsync(key, bytesValue, cancellationToken: cancellationToken);
        }

        ValueTask<string> IRedisClientAsync.GetValueAsync(string key, CancellationToken cancellationToken)
            => FromUtf8Bytes(NativeAsync.GetAsync(key, cancellationToken));

        ValueTask<T> ICacheClientAsync.GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            return ExecAsync(async r =>
                typeof(T) == typeof(byte[])
                    ? (T)(object)await ((IRedisNativeClientAsync)r).GetAsync(key, cancellationToken).ConfigureAwait(false)
                    : JsonSerializer.DeserializeFromString<T>(await r.GetValueAsync(key, cancellationToken).ConfigureAwait(false))
            );
        }

        async ValueTask<List<string>> IRedisClientAsync.SearchKeysAsync(string pattern, CancellationToken cancellationToken)
        {
            var list = new List<string>();
            await foreach (var value in ((IRedisClientAsync)this).ScanAllKeysAsync(pattern, cancellationToken: cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                list.Add(value);
            }
            return list;
        }

        async IAsyncEnumerable<string> IRedisClientAsync.ScanAllKeysAsync(string pattern, int pageSize, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ScanResult ret = default;
            while (true)
            {
                ret = await (pattern != null
                    ? NativeAsync.ScanAsync(ret?.Cursor ?? 0, pageSize, match: pattern, cancellationToken: cancellationToken)
                    : NativeAsync.ScanAsync(ret?.Cursor ?? 0, pageSize, cancellationToken: cancellationToken)
                    ).ConfigureAwait(false);

                foreach (var key in ret.Results)
                {
                    yield return key.FromUtf8Bytes();
                }

                if (ret.Cursor == 0) break;
            }
        }

        ValueTask<RedisKeyType> IRedisClientAsync.GetEntryTypeAsync(string key, CancellationToken cancellationToken)
        {
            var pending = NativeAsync.TypeAsync(key, cancellationToken);
            return pending.IsCompletedSuccessfully ? new ValueTask<RedisKeyType>(ParseEntryType(pending.Result)) : Awaited(this, pending);

            static async ValueTask<RedisKeyType> Awaited(RedisClient @this, ValueTask<string> pending)
                => @this.ParseEntryType(await pending.ConfigureAwait(false));
        }

        ValueTask IRedisClientAsync.AddItemToSetAsync(string setId, string item, CancellationToken cancellationToken)
            => DiscardResult(NativeAsync.SAddAsync(setId, item.ToUtf8Bytes(), cancellationToken));

        ValueTask IRedisClientAsync.AddItemToListAsync(string listId, string value, CancellationToken cancellationToken)
            => DiscardResult(NativeAsync.RPushAsync(listId, value.ToUtf8Bytes(), cancellationToken));

        ValueTask<bool> IRedisClientAsync.AddItemToSortedSetAsync(string setId, string value, CancellationToken cancellationToken)
            => ((IRedisClientAsync)this).AddItemToSortedSetAsync(setId, value, GetLexicalScore(value), cancellationToken);

        ValueTask<bool> IRedisClientAsync.AddItemToSortedSetAsync(string setId, string value, double score, CancellationToken cancellationToken)
            => IsSuccess(NativeAsync.ZAddAsync(setId, score, value.ToUtf8Bytes()));

        ValueTask<bool> IRedisClientAsync.SetEntryInHashAsync(string hashId, string key, string value, CancellationToken cancellationToken)
            => IsSuccess(NativeAsync.HSetAsync(hashId, key.ToUtf8Bytes(), value.ToUtf8Bytes()));

        ValueTask IRedisClientAsync.SetAllAsync(IDictionary<string, string> map, CancellationToken cancellationToken)
            => GetSetAllBytes(map, out var keyBytes, out var valBytes) ? NativeAsync.MSetAsync(keyBytes, valBytes, cancellationToken) : default;

        ValueTask IRedisClientAsync.SetAllAsync(IEnumerable<string> keys, IEnumerable<string> values, CancellationToken cancellationToken)
            => GetSetAllBytes(keys, values, out var keyBytes, out var valBytes) ? NativeAsync.MSetAsync(keyBytes, valBytes, cancellationToken) : default;

        ValueTask ICacheClientAsync.SetAllAsync<T>(IDictionary<string, T> values, CancellationToken cancellationToken)
        {
            if (values.Count != 0)
            {
                return ExecAsync(r =>
                {
                    // need to do this inside Exec for the JSON config bits
                    GetSetAllBytesTyped<T>(values, out var keys, out var valBytes);
                    return ((IRedisNativeClientAsync)r).MSetAsync(keys, valBytes, cancellationToken);
                });
            }
            else
            {
                return default;
            }
        }

        ValueTask IRedisClientAsync.RenameKeyAsync(string fromName, string toName, CancellationToken cancellationToken)
            => NativeAsync.RenameAsync(fromName, toName, cancellationToken);

        ValueTask<bool> IRedisClientAsync.ContainsKeyAsync(string key, CancellationToken cancellationToken)
            => IsSuccess(NativeAsync.ExistsAsync(key, cancellationToken));


        ValueTask<string> IRedisClientAsync.GetRandomKeyAsync(CancellationToken cancellationToken)
            => NativeAsync.RandomKeyAsync(cancellationToken);

        ValueTask IRedisClientAsync.ChangeDbAsync(long db, CancellationToken cancellationToken)
            => NativeAsync.SelectAsync(db, cancellationToken);

        ValueTask<bool> IRedisClientAsync.ExpireEntryInAsync(string key, TimeSpan expireIn, CancellationToken cancellationToken)
            => UseMillisecondExpiration(expireIn)
            ? NativeAsync.PExpireAsync(key, (long)expireIn.TotalMilliseconds, cancellationToken)
            : NativeAsync.ExpireAsync(key, (int)expireIn.TotalSeconds, cancellationToken);

        ValueTask<bool> IRedisClientAsync.ExpireEntryAtAsync(string key, DateTime expireAt, CancellationToken cancellationToken)
            => AssertServerVersionNumber() >= 2600
            ? NativeAsync.PExpireAtAsync(key, ConvertToServerDate(expireAt).ToUnixTimeMs(), cancellationToken)
            : NativeAsync.ExpireAtAsync(key, ConvertToServerDate(expireAt).ToUnixTime(), cancellationToken);

        async ValueTask<TimeSpan?> IRedisClientAsync.GetTimeToLiveAsync(string key, CancellationToken cancellationToken)
        {
            var ttlSecs = await NativeAsync.TtlAsync(key, cancellationToken).ConfigureAwait(false);
            if (ttlSecs == -1)
                return TimeSpan.MaxValue; //no expiry set

            if (ttlSecs == -2)
                return null; //key does not exist

            return TimeSpan.FromSeconds(ttlSecs);
        }

        ValueTask<bool> IRedisClientAsync.PingAsync(CancellationToken cancellationToken)
            => NativeAsync.PingAsync(cancellationToken);

        ValueTask<string> IRedisClientAsync.EchoAsync(string text, CancellationToken cancellationToken)
            => NativeAsync.EchoAsync(text, cancellationToken);

        ValueTask IRedisClientAsync.ForegroundSaveAsync(CancellationToken cancellationToken)
            => NativeAsync.SaveAsync(cancellationToken);

        ValueTask IRedisClientAsync.BackgroundSaveAsync(CancellationToken cancellationToken)
            => NativeAsync.BgSaveAsync(cancellationToken);

        ValueTask IRedisClientAsync.ShutdownAsync(CancellationToken cancellationToken)
            => NativeAsync.ShutdownAsync(false, cancellationToken);

        ValueTask IRedisClientAsync.ShutdownNoSaveAsync(CancellationToken cancellationToken)
            => NativeAsync.ShutdownAsync(true, cancellationToken);

        ValueTask IRedisClientAsync.BackgroundRewriteAppendOnlyFileAsync(CancellationToken cancellationToken)
            => NativeAsync.BgRewriteAofAsync(cancellationToken);

        ValueTask IRedisClientAsync.FlushDbAsync(CancellationToken cancellationToken)
            => NativeAsync.FlushDbAsync(cancellationToken);

        async ValueTask<List<string>> IRedisClientAsync.GetValuesAsync(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new List<string>();

            return ParseGetValuesResult(await NativeAsync.MGetAsync(keys.ToArray(), cancellationToken).ConfigureAwait(false));
        }

        async ValueTask<List<T>> IRedisClientAsync.GetValuesAsync<T>(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new List<T>();

            return ParseGetValuesResult<T>(await NativeAsync.MGetAsync(keys.ToArray(), cancellationToken).ConfigureAwait(false));
        }

       async ValueTask<Dictionary<string, string>> IRedisClientAsync.GetValuesMapAsync(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new Dictionary<string, string>();

            var keysArray = keys.ToArray();
            var resultBytesArray = await NativeAsync.MGetAsync(keysArray, cancellationToken).ConfigureAwait(false);

            return ParseGetValuesMapResult(keysArray, resultBytesArray);
        }

        async ValueTask<Dictionary<string, T>> IRedisClientAsync.GetValuesMapAsync<T>(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new Dictionary<string, T>();

            var keysArray = keys.ToArray();
            var resultBytesArray = await NativeAsync.MGetAsync(keysArray, cancellationToken).ConfigureAwait(false);

            return ParseGetValuesMapResult<T>(keysArray, resultBytesArray);
        }

        async ValueTask<IAsyncDisposable> IRedisClientAsync.AcquireLockAsync(string key, TimeSpan? timeOut, CancellationToken cancellationToken)
            => await RedisLock.CreateAsync(this, key, timeOut, cancellationToken).ConfigureAwait(false);

        ValueTask IRedisClientAsync.SetValueAsync(string key, string value, TimeSpan expireIn, CancellationToken cancellationToken)
        {
            var bytesValue = value?.ToUtf8Bytes();

            if (AssertServerVersionNumber() >= 2610)
            {
                PickTime(expireIn, out var seconds, out var milliseconds);
                return NativeAsync.SetAsync(key, bytesValue, expirySeconds: seconds,
                    expiryMilliseconds: milliseconds, cancellationToken: cancellationToken);
            }
            else
            {
                return NativeAsync.SetExAsync(key, (int)expireIn.TotalSeconds, bytesValue, cancellationToken);
            }
        }

        static void PickTime(TimeSpan? value, out long expirySeconds, out long expiryMilliseconds)
        {
            expirySeconds = expiryMilliseconds = 0;
            if (value.HasValue)
            {
                var expireIn = value.GetValueOrDefault();
                if (expireIn.Milliseconds > 0)
                {
                    expiryMilliseconds = (long)expireIn.TotalMilliseconds;
                }
                else
                {
                    expirySeconds = (long)expireIn.TotalSeconds;
                }
            }
        }
        ValueTask<bool> IRedisClientAsync.SetValueIfNotExistsAsync(string key, string value, TimeSpan? expireIn, CancellationToken cancellationToken)
        {
            var bytesValue = value?.ToUtf8Bytes();
            PickTime(expireIn, out var seconds, out var milliseconds);
            return NativeAsync.SetAsync(key, bytesValue, false, seconds, milliseconds, cancellationToken);
        }

        ValueTask<bool> IRedisClientAsync.SetValueIfExistsAsync(string key, string value, TimeSpan? expireIn, CancellationToken cancellationToken)
        {
            var bytesValue = value?.ToUtf8Bytes();
            PickTime(expireIn, out var seconds, out var milliseconds);
            return NativeAsync.SetAsync(key, bytesValue, true, seconds, milliseconds, cancellationToken);
        }

        ValueTask IRedisClientAsync.WatchAsync(string[] keys, CancellationToken cancellationToken)
            => NativeAsync.WatchAsync(keys, cancellationToken);

        ValueTask IRedisClientAsync.UnWatchAsync(CancellationToken cancellationToken)
            => NativeAsync.UnWatchAsync(cancellationToken);

        ValueTask<long> IRedisClientAsync.AppendToValueAsync(string key, string value, CancellationToken cancellationToken)
            => NativeAsync.AppendAsync(key, value.ToUtf8Bytes(), cancellationToken);

        async ValueTask<object> IRedisClientAsync.StoreObjectAsync(object entity, CancellationToken cancellationToken)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var id = entity.GetObjectId();
            var entityType = entity.GetType();
            var urnKey = UrnKey(entityType, id);
            var valueString = JsonSerializer.SerializeToString(entity);

            await ((IRedisClientAsync)this).SetValueAsync(urnKey, valueString, cancellationToken).ConfigureAwait(false);

            await RegisterTypeIdAsync(GetTypeIdsSetKey(entityType), id.ToString(), cancellationToken).ConfigureAwait(false);

            return entity;
        }

        ValueTask<string> IRedisClientAsync.PopItemFromSetAsync(string setId, CancellationToken cancellationToken)
            => FromUtf8Bytes(NativeAsync.SPopAsync(setId, cancellationToken));

        async ValueTask<List<string>> IRedisClientAsync.PopItemsFromSetAsync(string setId, int count, CancellationToken cancellationToken)
        {
            var result = await NativeAsync.SPopAsync(setId, count, cancellationToken).ConfigureAwait(false);
            return result.ToStringList();
        }

        ValueTask IRedisClientAsync.SlowlogResetAsync(CancellationToken cancellationToken)
            => NativeAsync.SlowlogResetAsync(cancellationToken);

        async ValueTask<SlowlogItem[]> IRedisClientAsync.SlowlogGetAsync(int? top, CancellationToken cancellationToken)
        {
            var data = await NativeAsync.SlowlogGetAsync(top, cancellationToken).ConfigureAwait(false);
            return ParseSlowlog(data);
        }

        ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            var pending = ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), cancellationToken: cancellationToken));
            return pending.IsCompletedSuccessfully ? new ValueTask<bool>(true) : Awaited(pending);

            async static ValueTask<bool> Awaited(ValueTask pending)
            {
                await pending.ConfigureAwait(false);
                return true;
            }
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            Dispose();
            return default;
        }

        ValueTask<long> IRedisClientAsync.GetSortedSetCountAsync(string setId, CancellationToken cancellationToken)
            => NativeAsync.ZCardAsync(setId, cancellationToken);

        ValueTask<long> IRedisClientAsync.GetSortedSetCountAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            var fromScore = GetLexicalScore(fromStringScore);
            var toScore = GetLexicalScore(toStringScore);
            return AsAsync().GetSortedSetCountAsync(setId, fromScore, toScore, cancellationToken);
        }

        ValueTask<long> IRedisClientAsync.GetSortedSetCountAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken)
            => NativeAsync.ZCountAsync(setId, fromScore, toScore, cancellationToken);

        ValueTask<long> IRedisClientAsync.GetSortedSetCountAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken)
            => NativeAsync.ZCountAsync(setId, fromScore, toScore, cancellationToken);

        ValueTask<double> IRedisClientAsync.GetItemScoreInSortedSetAsync(string setId, string value, CancellationToken cancellationToken)
            => NativeAsync.ZScoreAsync(setId, value.ToUtf8Bytes(), cancellationToken);

        ValueTask<RedisText> IRedisClientAsync.CustomAsync(object[] cmdWithArgs, CancellationToken cancellationToken)
        {
            var pending = base.RawCommandAsync(cancellationToken, cmdWithArgs);
            return pending.IsCompletedSuccessfully
                ? new ValueTask<RedisText>(pending.Result.ToRedisText())
                : Awaited(pending);
            
            static async ValueTask<RedisText> Awaited(ValueTask<RedisData> pending)
                => (await pending.ConfigureAwait(false)).ToRedisText();
        }

        ValueTask IRedisClientAsync.SetValuesAsync(IDictionary<string, string> map, CancellationToken cancellationToken)
            => ((IRedisClientAsync)this).SetAllAsync(map, cancellationToken);

        async ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, DateTime expiresAt, CancellationToken cancellationToken)
        {
            AssertNotInTransaction();
            await ExecAsync(async r =>
            {
                await r.SetAsync(key, value).ConfigureAwait(false);
                await r.ExpireEntryAtAsync(key, ConvertToServerDate(expiresAt)).ConfigureAwait(false);
            }).ConfigureAwait(false);
            return true;
        }
        async ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, TimeSpan expiresIn, CancellationToken cancellationToken)
        {
            if (AssertServerVersionNumber() >= 2600)
            {
                await ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), 0, expiryMilliseconds: (long)expiresIn.TotalMilliseconds)).ConfigureAwait(false);
            }
            else
            {
                await ExecAsync(r => ((IRedisNativeClientAsync)r).SetExAsync(key, (int)expiresIn.TotalSeconds, ToBytes(value))).ConfigureAwait(false);
            }
            return true;
        }

        ValueTask ICacheClientAsync.FlushAllAsync(CancellationToken cancellationToken)
            => NativeAsync.FlushAllAsync(cancellationToken);

        ValueTask<IDictionary<string, T>> ICacheClientAsync.GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            return ExecAsync(async r =>
            {
                var keysArray = keys.ToArray();
                var keyValues = await ((IRedisNativeClientAsync)r).MGetAsync(keysArray, cancellationToken).ConfigureAwait(false);

                return ProcessGetAllResult<T>(keysArray, keyValues);
            });
        }

        ValueTask<bool> ICacheClientAsync.RemoveAsync(string key, CancellationToken cancellationToken)
            => IsSuccess(NativeAsync.DelAsync(key, cancellationToken));

        ValueTask<TimeSpan?> ICacheClientExtendedAsync.GetTimeToLiveAsync(string key, CancellationToken cancellationToken)
        {
            var pending = NativeAsync.TtlAsync(key, cancellationToken);
            return pending.IsCompletedSuccessfully
                ? new ValueTask<TimeSpan?>(ParseTimeToLiveResult(pending.Result))
                : Awaited(pending);
            static async ValueTask<TimeSpan?> Awaited(ValueTask<long> pending)
                => ParseTimeToLiveResult(await pending.ConfigureAwait(false));
        }

        IAsyncEnumerable<string> ICacheClientExtendedAsync.GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken)
            => AsAsync().ScanAllKeysAsync(pattern, cancellationToken: cancellationToken);

        ValueTask ICacheClientExtendedAsync.RemoveExpiredEntriesAsync(CancellationToken cancellationToken)
        {
            //Redis automatically removed expired Cache Entries
            return default;
        }

        async ValueTask IRemoveByPatternAsync.RemoveByPatternAsync(string pattern, CancellationToken cancellationToken)
        {
            List<string> buffer = null;
            const int BATCH_SIZE = 1024;
            await foreach(var key in AsAsync().ScanAllKeysAsync(pattern, cancellationToken: cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                (buffer ??= new List<string>()).Add(key);
                if (buffer.Count == BATCH_SIZE)
                {
                    await NativeAsync.DelAsync(buffer.ToArray(), cancellationToken).ConfigureAwait(false);
                    buffer.Clear();
                }
            }
            if (buffer is object && buffer.Count != 0)
            {
                await NativeAsync.DelAsync(buffer.ToArray(), cancellationToken).ConfigureAwait(false);
            }
        }

        ValueTask IRemoveByPatternAsync.RemoveByRegexAsync(string regex, CancellationToken cancellationToken)
            => AsAsync().RemoveByPatternAsync(RegexToGlob(regex), cancellationToken);

        ValueTask ICacheClientAsync.RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> ICacheClientAsync.IncrementAsync(string key, uint amount, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> ICacheClientAsync.DecrementAsync(string key, uint amount, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> ICacheClientAsync.AddAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> ICacheClientAsync.ReplaceAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> ICacheClientAsync.AddAsync<T>(string key, T value, DateTime expiresAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> ICacheClientAsync.ReplaceAsync<T>(string key, T value, DateTime expiresAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> ICacheClientAsync.AddAsync<T>(string key, T value, TimeSpan expiresIn, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> ICacheClientAsync.ReplaceAsync<T>(string key, T value, TimeSpan expiresIn, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.DbSizeAsync(CancellationToken cancellationToken)
            => NativeAsync.DbSizeAsync(cancellationToken);

        ValueTask<Dictionary<string, string>> IRedisClientAsync.InfoAsync(CancellationToken cancellationToken)
            => NativeAsync.InfoAsync(cancellationToken);

        ValueTask<DateTime> IRedisClientAsync.LastSaveAsync(CancellationToken cancellationToken)
            => NativeAsync.LastSaveAsync(cancellationToken);

        ValueTask<T> IEntityStoreAsync.GetByIdAsync<T>(object id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IList<T>> IEntityStoreAsync.GetByIdsAsync<T>(ICollection ids, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IEntityStoreAsync.StoreAsync<T>(T entity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IEntityStoreAsync.StoreAllAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IEntityStoreAsync.DeleteAsync<T>(T entity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IEntityStoreAsync.DeleteByIdAsync<T>(object id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IEntityStoreAsync.DeleteByIdsAsync<T>(ICollection ids, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IEntityStoreAsync.DeleteAllAsync<TEntity>(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        async ValueTask<List<string>> IRedisClientAsync.SearchSortedSetAsync(string setId, string start, string end, int? skip, int? take, CancellationToken cancellationToken)
        {
            start = GetSearchStart(start);
            end = GetSearchEnd(end);

            var ret = await NativeAsync.ZRangeByLexAsync(setId, start, end, skip, take, cancellationToken);
            return ret.ToStringList();
        }

        ValueTask<long> IRedisClientAsync.SearchSortedSetCountAsync(string setId, string start, string end, CancellationToken cancellationToken)
            => NativeAsync.ZLexCountAsync(setId, GetSearchStart(start), GetSearchEnd(end), cancellationToken);

        ValueTask<long> IRedisClientAsync.RemoveRangeFromSortedSetBySearchAsync(string setId, string start, string end, CancellationToken cancellationToken)
            => NativeAsync.ZRemRangeByLexAsync(setId, GetSearchStart(start), GetSearchEnd(end), cancellationToken);
    }
}
 