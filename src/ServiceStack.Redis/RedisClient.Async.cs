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
using ServiceStack.Redis.Internal;
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


        ValueTask<DateTime> IRedisClientAsync.GetServerTimeAsync(CancellationToken cancellationToken)
            => NativeAsync.TimeAsync(cancellationToken).Await(parts => ParseTimeResult(parts));

        ValueTask<long> IRedisClientAsync.IncrementValueAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.IncrAsync(key, cancellationToken);

        ValueTask<IRedisPipelineAsync> IRedisClientAsync.CreatePipelineAsync(CancellationToken cancellationToken)
            => new RedisAllPurposePipeline(this).AsValueTask<IRedisPipelineAsync>();

        ValueTask<IRedisTransactionAsync> IRedisClientAsync.CreateTransactionAsync(CancellationToken cancellationToken)
        {
            AssertServerVersionNumber(); // pre-fetch call to INFO before transaction if needed
            return new RedisTransaction(this, true).AsValueTask<IRedisTransactionAsync>(); // note that the MULTI here will be held and flushed async
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
            => NativeAsync.GetAsync(key, cancellationToken).AwaitFromUtf8Bytes();

        ValueTask<T> ICacheClientAsync.GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            return ExecAsync(r =>
                typeof(T) == typeof(byte[])
                    ? ((IRedisNativeClientAsync)r).GetAsync(key, cancellationToken).Await(val => (T)(object)val)
                    : r.GetValueAsync(key, cancellationToken).Await(val => JsonSerializer.DeserializeFromString<T>(val))
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
            => NativeAsync.TypeAsync(key, cancellationToken).Await((val,state) => state.ParseEntryType(val), this);

        ValueTask IRedisClientAsync.AddItemToSetAsync(string setId, string item, CancellationToken cancellationToken)
            => NativeAsync.SAddAsync(setId, item.ToUtf8Bytes(), cancellationToken).Await();

        ValueTask IRedisClientAsync.AddItemToListAsync(string listId, string value, CancellationToken cancellationToken)
            => NativeAsync.RPushAsync(listId, value.ToUtf8Bytes(), cancellationToken).Await();

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

        ValueTask<TimeSpan?> IRedisClientAsync.GetTimeToLiveAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.TtlAsync(key, cancellationToken).Await(ttlSecs => ParseTimeToLiveResult(ttlSecs));

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

        ValueTask<List<string>> IRedisClientAsync.GetValuesAsync(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new List<string>().AsValueTask();

            return NativeAsync.MGetAsync(keys.ToArray(), cancellationToken).Await(val => ParseGetValuesResult(val));
        }

        ValueTask<List<T>> IRedisClientAsync.GetValuesAsync<T>(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new List<T>().AsValueTask();

            return NativeAsync.MGetAsync(keys.ToArray(), cancellationToken).Await(value => ParseGetValuesResult<T>(value));
        }

       ValueTask<Dictionary<string, string>> IRedisClientAsync.GetValuesMapAsync(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new Dictionary<string, string>().AsValueTask();

            var keysArray = keys.ToArray();
            return NativeAsync.MGetAsync(keysArray, cancellationToken).Await((resultBytesArray, state) => ParseGetValuesMapResult(state, resultBytesArray), keysArray);
        }

        ValueTask<Dictionary<string, T>> IRedisClientAsync.GetValuesMapAsync<T>(List<string> keys, CancellationToken cancellationToken)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0) return new Dictionary<string, T>().AsValueTask();

            var keysArray = keys.ToArray();
            return NativeAsync.MGetAsync(keysArray, cancellationToken).Await((resultBytesArray, state) => ParseGetValuesMapResult<T>(state, resultBytesArray), keysArray);
        }

        ValueTask<IAsyncDisposable> IRedisClientAsync.AcquireLockAsync(string key, TimeSpan? timeOut, CancellationToken cancellationToken)
            => RedisLock.CreateAsync(this, key, timeOut, cancellationToken).Await<RedisLock, IAsyncDisposable>(value => value);

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
            => NativeAsync.SPopAsync(setId, cancellationToken).AwaitFromUtf8Bytes();

        ValueTask<List<string>> IRedisClientAsync.PopItemsFromSetAsync(string setId, int count, CancellationToken cancellationToken)
            => NativeAsync.SPopAsync(setId, count, cancellationToken).Await(result => result.ToStringList());

        ValueTask IRedisClientAsync.SlowlogResetAsync(CancellationToken cancellationToken)
            => NativeAsync.SlowlogResetAsync(cancellationToken);

        ValueTask<SlowlogItem[]> IRedisClientAsync.SlowlogGetAsync(int? top, CancellationToken cancellationToken)
            => NativeAsync.SlowlogGetAsync(top, cancellationToken).Await(data => ParseSlowlog(data));


        ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, CancellationToken cancellationToken)
            => ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), cancellationToken: cancellationToken)).AwaitAsTrue();

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
            => RawCommandAsync(cancellationToken, cmdWithArgs).Await(result => result.ToRedisText());

        ValueTask IRedisClientAsync.SetValuesAsync(IDictionary<string, string> map, CancellationToken cancellationToken)
            => ((IRedisClientAsync)this).SetAllAsync(map, cancellationToken);

        ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, DateTime expiresAt, CancellationToken cancellationToken)
        {
            AssertNotInTransaction();
            return ExecAsync(async r =>
            {
                await r.SetAsync(key, value).ConfigureAwait(false);
                await r.ExpireEntryAtAsync(key, ConvertToServerDate(expiresAt)).ConfigureAwait(false);
            }).AwaitAsTrue();
        }
        ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, TimeSpan expiresIn, CancellationToken cancellationToken)
        {
            if (AssertServerVersionNumber() >= 2600)
            {
                return ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), 0, expiryMilliseconds: (long)expiresIn.TotalMilliseconds)).AwaitAsTrue();
            }
            else
            {
                return ExecAsync(r => ((IRedisNativeClientAsync)r).SetExAsync(key, (int)expiresIn.TotalSeconds, ToBytes(value))).AwaitAsTrue();
            }
        }

        ValueTask ICacheClientAsync.FlushAllAsync(CancellationToken cancellationToken)
            => NativeAsync.FlushAllAsync(cancellationToken);

        ValueTask<IDictionary<string, T>> ICacheClientAsync.GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            return ExecAsync(r =>
            {
                var keysArray = keys.ToArray();

                return ((IRedisNativeClientAsync)r).MGetAsync(keysArray, cancellationToken).Await((keyValues, state) => ProcessGetAllResult<T>(state, keyValues), keysArray);
            });
        }

        ValueTask<bool> ICacheClientAsync.RemoveAsync(string key, CancellationToken cancellationToken)
            => IsSuccess(NativeAsync.DelAsync(key, cancellationToken));

        ValueTask<TimeSpan?> ICacheClientExtendedAsync.GetTimeToLiveAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.TtlAsync(key, cancellationToken).Await(val => ParseTimeToLiveResult(val));

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
            => ExecAsync(r => r.RemoveEntryAsync(keys.ToArray(), cancellationToken)).Await();

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

        async ValueTask<T> IEntityStoreAsync.GetByIdAsync<T>(object id, CancellationToken cancellationToken)
        {
            var key = UrnKey<T>(id);
            var valueString = await AsAsync().GetValueAsync(key, cancellationToken).ConfigureAwait(false);
            var value = JsonSerializer.DeserializeFromString<T>(valueString);
            return value;
        }

        async ValueTask<IList<T>> IEntityStoreAsync.GetByIdsAsync<T>(ICollection ids, CancellationToken cancellationToken)
        {
            if (ids == null || ids.Count == 0)
                return new List<T>();

            var urnKeys = ids.Cast<object>().Map(UrnKey<T>);
            return await AsAsync().GetValuesAsync<T>(urnKeys, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<T> IEntityStoreAsync.StoreAsync<T>(T entity, CancellationToken cancellationToken)
        {
            var urnKey = UrnKey(entity);
            var valueString = JsonSerializer.SerializeToString(entity);

            await AsAsync().SetValueAsync(urnKey, valueString, cancellationToken).ConfigureAwait(false);
            RegisterTypeId(entity);

            return entity;
        }

        ValueTask IEntityStoreAsync.StoreAllAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
            => _StoreAllAsync(entities, cancellationToken);

        internal async ValueTask _StoreAllAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
        {
            if (PrepareStoreAll(entities, out var keys, out var values, out var entitiesList))
            {
                await NativeAsync.MSetAsync(keys, values, cancellationToken);
                RegisterTypeIds(entitiesList);
            }
        }

        async ValueTask IEntityStoreAsync.DeleteAsync<T>(T entity, CancellationToken cancellationToken)
        {
            var urnKey = UrnKey(entity);
            await AsAsync().RemoveAsync(urnKey, cancellationToken).ConfigureAwait(false);
            this.RemoveTypeIds(entity);
        }

        async ValueTask IEntityStoreAsync.DeleteByIdAsync<T>(object id, CancellationToken cancellationToken)
        {
            var urnKey = UrnKey<T>(id);
            await AsAsync().RemoveAsync(urnKey, cancellationToken);
            this.RemoveTypeIds<T>(id.ToString());
        }

        async ValueTask IEntityStoreAsync.DeleteByIdsAsync<T>(ICollection ids, CancellationToken cancellationToken)
        {
            if (ids == null || ids.Count == 0) return;

            var idsList = ids.Cast<object>();
            var urnKeys = idsList.Map(UrnKey<T>);
            await AsAsync().RemoveEntryAsync(urnKeys.ToArray(), cancellationToken).ConfigureAwait(false);
            this.RemoveTypeIds<T>(idsList.Map(x => x.ToString()).ToArray());
        }

        async ValueTask IEntityStoreAsync.DeleteAllAsync<T>(CancellationToken cancellationToken)
        {
            var typeIdsSetKey = this.GetTypeIdsSetKey<T>();
            var ids = await AsAsync().GetAllItemsFromSetAsync(typeIdsSetKey, cancellationToken).ConfigureAwait(false);
            if (ids.Count > 0)
            {
                var urnKeys = ids.ToList().ConvertAll(UrnKey<T>);
                await AsAsync().RemoveEntryAsync(urnKeys.ToArray()).ConfigureAwait(false);
                await AsAsync().RemoveAsync(typeIdsSetKey).ConfigureAwait(false);
            }
        }

        ValueTask<List<string>> IRedisClientAsync.SearchSortedSetAsync(string setId, string start, string end, int? skip, int? take, CancellationToken cancellationToken)
        {
            start = GetSearchStart(start);
            end = GetSearchEnd(end);

            return NativeAsync.ZRangeByLexAsync(setId, start, end, skip, take, cancellationToken).Await(ret => ret.ToStringList());
        }

        ValueTask<long> IRedisClientAsync.SearchSortedSetCountAsync(string setId, string start, string end, CancellationToken cancellationToken)
            => NativeAsync.ZLexCountAsync(setId, GetSearchStart(start), GetSearchEnd(end), cancellationToken);

        ValueTask<long> IRedisClientAsync.RemoveRangeFromSortedSetBySearchAsync(string setId, string start, string end, CancellationToken cancellationToken)
            => NativeAsync.ZRemRangeByLexAsync(setId, GetSearchStart(start), GetSearchEnd(end), cancellationToken);

        ValueTask<string> IRedisClientAsync.TypeAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.TypeAsync(key, cancellationToken);

        ValueTask<long> IRedisClientAsync.GetStringCountAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.StrLenAsync(key, cancellationToken);

        ValueTask<long> IRedisClientAsync.GetSetCountAsync(string setId, CancellationToken cancellationToken)
            => NativeAsync.SCardAsync(setId, cancellationToken);

        ValueTask<long> IRedisClientAsync.GetListCountAsync(string listId, CancellationToken cancellationToken)
            => NativeAsync.LLenAsync(listId, cancellationToken);

        ValueTask<long> IRedisClientAsync.GetHashCountAsync(string hashId, CancellationToken cancellationToken)
            => NativeAsync.HLenAsync(hashId, cancellationToken);

        async ValueTask<T> IRedisClientAsync.ExecCachedLuaAsync<T>(string scriptBody, Func<string, ValueTask<T>> scriptSha1, CancellationToken cancellationToken)
        {
            if (!CachedLuaSha1Map.TryGetValue(scriptBody, out var sha1))
                CachedLuaSha1Map[scriptBody] = sha1 = await AsAsync().LoadLuaScriptAsync(scriptBody, cancellationToken).ConfigureAwait(false);

            try
            {
                return await scriptSha1(sha1).ConfigureAwait(false);
            }
            catch (RedisResponseException ex)
            {
                if (!ex.Message.StartsWith("NOSCRIPT"))
                    throw;

                CachedLuaSha1Map[scriptBody] = sha1 = await AsAsync().LoadLuaScriptAsync(scriptBody, cancellationToken).ConfigureAwait(false);
                return await scriptSha1(sha1).ConfigureAwait(false);
            }
        }

        ValueTask<RedisText> IRedisClientAsync.ExecLuaAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken)
            => NativeAsync.EvalCommandAsync(luaBody, keys?.Length ?? 0, MergeAndConvertToBytes(keys, args), cancellationToken).Await(data => data.ToRedisText());

        ValueTask<RedisText> IRedisClientAsync.ExecLuaShaAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken)
            => NativeAsync.EvalShaCommandAsync(sha1, keys?.Length ?? 0, MergeAndConvertToBytes(keys, args), cancellationToken).Await(data => data.ToRedisText());

        ValueTask<string> IRedisClientAsync.ExecLuaAsStringAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken)
            => NativeAsync.EvalStrAsync(luaBody, keys?.Length ?? 0, MergeAndConvertToBytes(keys, args), cancellationToken);

        ValueTask<string> IRedisClientAsync.ExecLuaShaAsStringAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken)
            => NativeAsync.EvalShaStrAsync(sha1, keys?.Length ?? 0, MergeAndConvertToBytes(keys, args), cancellationToken);

        ValueTask<string> IRedisClientAsync.LoadLuaScriptAsync(string body, CancellationToken cancellationToken)
            => NativeAsync.ScriptLoadAsync(body, cancellationToken).AwaitFromUtf8Bytes();

        ValueTask IRedisClientAsync.WriteAllAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
            => PrepareWriteAll(entities, out var keys, out var values) ? NativeAsync.MSetAsync(keys, values, cancellationToken) : default;

        async ValueTask<HashSet<string>> IRedisClientAsync.GetAllItemsFromSetAsync(string setId, CancellationToken cancellationToken)
        {
            var multiDataList = await NativeAsync.SMembersAsync(setId, cancellationToken);
            return CreateHashSet(multiDataList);
        }
    }
}
 