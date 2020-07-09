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

        internal ValueTask RegisterTypeIdAsync<T>(T value, CancellationToken cancellationToken)
        {
            var typeIdsSetKey = GetTypeIdsSetKey<T>();
            var id = value.GetId().ToString();

            return RegisterTypeIdAsync(typeIdsSetKey, id, cancellationToken);
        }
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

        ValueTask<IRedisPipelineAsync> IRedisClientAsync.CreatePipelineAsync(CancellationToken cancellationToken)
            => new RedisAllPurposePipeline(this).AsValueTask<IRedisPipelineAsync>();

        ValueTask<IRedisTransactionAsync> IRedisClientAsync.CreateTransactionAsync(CancellationToken cancellationToken)
        {
            AssertServerVersionNumber(); // pre-fetch call to INFO before transaction if needed
            return new RedisTransaction(this, true).AsValueTask<IRedisTransactionAsync>(); // note that the MULTI here will be held and flushed async
        }

        ValueTask<bool> IRedisClientAsync.RemoveEntryAsync(string[] keys, CancellationToken cancellationToken)
            => keys.Length == 0 ? default : NativeAsync.DelAsync(keys, cancellationToken).IsSuccess();

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
                ret = await (pattern != null // note ConfigureAwait is handled below
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
            => NativeAsync.TypeAsync(key, cancellationToken).Await((val, state) => state.ParseEntryType(val), this);

        ValueTask IRedisClientAsync.AddItemToSetAsync(string setId, string item, CancellationToken cancellationToken)
            => NativeAsync.SAddAsync(setId, item.ToUtf8Bytes(), cancellationToken).Await();

        ValueTask IRedisClientAsync.AddItemToListAsync(string listId, string value, CancellationToken cancellationToken)
            => NativeAsync.RPushAsync(listId, value.ToUtf8Bytes(), cancellationToken).Await();

        ValueTask<bool> IRedisClientAsync.AddItemToSortedSetAsync(string setId, string value, CancellationToken cancellationToken)
            => ((IRedisClientAsync)this).AddItemToSortedSetAsync(setId, value, GetLexicalScore(value), cancellationToken);

        ValueTask<bool> IRedisClientAsync.AddItemToSortedSetAsync(string setId, string value, double score, CancellationToken cancellationToken)
            => NativeAsync.ZAddAsync(setId, score, value.ToUtf8Bytes()).IsSuccess();

        ValueTask<bool> IRedisClientAsync.SetEntryInHashAsync(string hashId, string key, string value, CancellationToken cancellationToken)
            => NativeAsync.HSetAsync(hashId, key.ToUtf8Bytes(), value.ToUtf8Bytes()).IsSuccess();

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
            => NativeAsync.ExistsAsync(key, cancellationToken).IsSuccess();


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

        ValueTask<TimeSpan?> ICacheClientExtendedAsync.GetTimeToLiveAsync(string key, CancellationToken cancellationToken)
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
            => NativeAsync.DelAsync(key, cancellationToken).IsSuccess();

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
            await foreach (var key in AsAsync().ScanAllKeysAsync(pattern, cancellationToken: cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
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
            => ExecAsync(r => r.IncrementValueByAsync(key, (int)amount, cancellationToken));

        ValueTask<long> ICacheClientAsync.DecrementAsync(string key, uint amount, CancellationToken cancellationToken)
            => ExecAsync(r => r.DecrementValueByAsync(key, (int)amount, cancellationToken));


        ValueTask<bool> ICacheClientAsync.AddAsync<T>(string key, T value, CancellationToken cancellationToken)
            => ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), exists: false, cancellationToken: cancellationToken));

        ValueTask<bool> ICacheClientAsync.ReplaceAsync<T>(string key, T value, CancellationToken cancellationToken)
            => ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), exists: true, cancellationToken: cancellationToken));

        ValueTask<bool> ICacheClientAsync.AddAsync<T>(string key, T value, DateTime expiresAt, CancellationToken cancellationToken)
        {
            AssertNotInTransaction();

            return ExecAsync(async r =>
            {
                if (await r.AddAsync(key, value, cancellationToken).ConfigureAwait(false))
                {
                    await r.ExpireEntryAtAsync(key, ConvertToServerDate(expiresAt), cancellationToken).ConfigureAwait(false);
                    return true;
                }
                return false;
            });
        }

        ValueTask<bool> ICacheClientAsync.ReplaceAsync<T>(string key, T value, DateTime expiresAt, CancellationToken cancellationToken)
        {
            AssertNotInTransaction();

            return ExecAsync(async r =>
            {
                if (await r.ReplaceAsync(key, value, cancellationToken).ConfigureAwait(false))
                {
                    await r.ExpireEntryAtAsync(key, ConvertToServerDate(expiresAt), cancellationToken).ConfigureAwait(false);
                    return true;
                }
                return false;
            });
        }

        ValueTask<bool> ICacheClientAsync.AddAsync<T>(string key, T value, TimeSpan expiresIn, CancellationToken cancellationToken)
            => ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), exists: false, cancellationToken: cancellationToken));

        ValueTask<bool> ICacheClientAsync.ReplaceAsync<T>(string key, T value, TimeSpan expiresIn, CancellationToken cancellationToken)
            => ExecAsync(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), exists: true, cancellationToken: cancellationToken));

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
            await RegisterTypeIdAsync(entity, cancellationToken).ConfigureAwait(false);

            return entity;
        }

        ValueTask IEntityStoreAsync.StoreAllAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
            => StoreAllAsyncImpl(entities, cancellationToken);

        internal async ValueTask StoreAllAsyncImpl<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
        {
            if (PrepareStoreAll(entities, out var keys, out var values, out var entitiesList))
            {
                await NativeAsync.MSetAsync(keys, values, cancellationToken).ConfigureAwait(false);
                await RegisterTypeIdsAsync(entitiesList, cancellationToken).ConfigureAwait(false);
            }
        }

        internal ValueTask RegisterTypeIdsAsync<T>(IEnumerable<T> values, CancellationToken cancellationToken)
        {
            var typeIdsSetKey = GetTypeIdsSetKey<T>();
            var ids = values.Map(x => x.GetId().ToString());

            if (this.Pipeline != null)
            {
                var registeredTypeIdsWithinPipeline = GetRegisteredTypeIdsWithinPipeline(typeIdsSetKey);
                ids.ForEach(x => registeredTypeIdsWithinPipeline.Add(x));
                return default;
            }
            else
            {
                return AsAsync().AddRangeToSetAsync(typeIdsSetKey, ids, cancellationToken);
            }
        }

        internal async ValueTask RemoveTypeIdsAsync<T>(T[] values, CancellationToken cancellationToken)
        {
            var typeIdsSetKey = GetTypeIdsSetKey<T>();
            if (this.Pipeline != null)
            {
                var registeredTypeIdsWithinPipeline = GetRegisteredTypeIdsWithinPipeline(typeIdsSetKey);
                values.Each(x => registeredTypeIdsWithinPipeline.Remove(x.GetId().ToString()));
            }
            else
            {
                foreach (var x in values)
                {
                    await AsAsync().RemoveItemFromSetAsync(typeIdsSetKey, x.GetId().ToString(), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal async ValueTask RemoveTypeIdsAsync<T>(string[] ids, CancellationToken cancellationToken)
        {
            var typeIdsSetKey = GetTypeIdsSetKey<T>();
            if (this.Pipeline != null)
            {
                var registeredTypeIdsWithinPipeline = GetRegisteredTypeIdsWithinPipeline(typeIdsSetKey);
                ids.Each(x => registeredTypeIdsWithinPipeline.Remove(x));
            }
            else
            {
                foreach (var x in ids)
                {
                    await AsAsync().RemoveItemFromSetAsync(typeIdsSetKey, x, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        async ValueTask IEntityStoreAsync.DeleteAsync<T>(T entity, CancellationToken cancellationToken)
        {
            var urnKey = UrnKey(entity);
            await AsAsync().RemoveAsync(urnKey, cancellationToken).ConfigureAwait(false);
            await this.RemoveTypeIdsAsync(new[] { entity }, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask IEntityStoreAsync.DeleteByIdAsync<T>(object id, CancellationToken cancellationToken)
        {
            var urnKey = UrnKey<T>(id);
            await AsAsync().RemoveAsync(urnKey, cancellationToken).ConfigureAwait(false);
            await this.RemoveTypeIdsAsync<T>(new[] { id.ToString() }, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask IEntityStoreAsync.DeleteByIdsAsync<T>(ICollection ids, CancellationToken cancellationToken)
        {
            if (ids == null || ids.Count == 0) return;

            var idsList = ids.Cast<object>();
            var urnKeys = idsList.Map(UrnKey<T>);
            await AsAsync().RemoveEntryAsync(urnKeys.ToArray(), cancellationToken).ConfigureAwait(false);
            await this.RemoveTypeIdsAsync<T>(idsList.Map(x => x.ToString()).ToArray(), cancellationToken).ConfigureAwait(false);
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
            var multiDataList = await NativeAsync.SMembersAsync(setId, cancellationToken).ConfigureAwait(false);
            return CreateHashSet(multiDataList);
        }

        ValueTask IRedisClientAsync.AddRangeToSetAsync(string setId, List<string> items, CancellationToken cancellationToken)
        {
            if (AddRangeToSetNeedsSend(setId, items))
            {
                var uSetId = setId.ToUtf8Bytes();
                var pipeline = CreatePipelineCommand();
                foreach (var item in items)
                {
                    pipeline.WriteCommand(Commands.SAdd, uSetId, item.ToUtf8Bytes());
                }
                pipeline.Flush();

                //the number of items after
                return pipeline.ReadAllAsIntsAsync(cancellationToken).Await();
            }
            else
            {
                return default;
            }
        }

        ValueTask IRedisClientAsync.RemoveItemFromSetAsync(string setId, string item, CancellationToken cancellationToken)
            => NativeAsync.SRemAsync(setId, item.ToUtf8Bytes(), cancellationToken).Await();

        ValueTask<long> IRedisClientAsync.IncrementValueByAsync(string key, int count, CancellationToken cancellationToken)
            => NativeAsync.IncrByAsync(key, count, cancellationToken);

        ValueTask<long> IRedisClientAsync.IncrementValueByAsync(string key, long count, CancellationToken cancellationToken)
            => NativeAsync.IncrByAsync(key, count, cancellationToken);

        ValueTask<double> IRedisClientAsync.IncrementValueByAsync(string key, double count, CancellationToken cancellationToken)
            => NativeAsync.IncrByFloatAsync(key, count, cancellationToken);
        ValueTask<long> IRedisClientAsync.IncrementValueAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.IncrAsync(key, cancellationToken);

        ValueTask<long> IRedisClientAsync.DecrementValueAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.DecrAsync(key, cancellationToken);

        ValueTask<long> IRedisClientAsync.DecrementValueByAsync(string key, int count, CancellationToken cancellationToken)
            => NativeAsync.DecrByAsync(key, count, cancellationToken);

        RedisServerRole IRedisClientAsync.GetServerRoleAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        RedisText IRedisClientAsync.GetServerRoleInfoAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.GetConfigAsync(string item, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.SetConfigAsync(string item, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.SaveConfigAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.ResetInfoStatsAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.GetClientAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.SetClientAsync(string name, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.KillClientAsync(string address, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.KillClientsAsync(string fromAddress, string withId, RedisClientType? ofType, bool? skipMe, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<Dictionary<string, string>>> IRedisClientAsync.GetClientsInfoAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.PauseAllClientsAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetAllKeysAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        string IRedisClientAsync.UrnKey(object id)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.GetAndSetValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<T> IRedisClientAsync.GetFromHashAsync<T>(object id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.StoreAsHashAsync<T>(T entity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetSortedEntryValuesAsync(string key, int startingFrom, int endingAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IAsyncEnumerable<string> IRedisClientAsync.ScanAllSetItemsAsync(string setId, string pattern, int pageSize, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IAsyncEnumerable<KeyValuePair<string, double>> IRedisClientAsync.ScanAllSortedSetItemsAsync(string setId, string pattern, int pageSize, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IAsyncEnumerable<KeyValuePair<string, string>> IRedisClientAsync.ScanAllHashEntriesAsync(string hashId, string pattern, int pageSize, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.AddToHyperLogAsync(string key, string[] elements, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.CountHyperLogAsync(string key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.MergeHyperLogsAsync(string toKey, string[] fromKeys, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.AddGeoMemberAsync(string key, double longitude, double latitude, string member, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.AddGeoMembersAsync(string key, RedisGeo[] geoPoints, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisClientAsync.CalculateDistanceBetweenGeoMembersAsync(string key, string fromMember, string toMember, string unit, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string[]> IRedisClientAsync.GetGeohashesAsync(string key, string[] members, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<RedisGeo>> IRedisClientAsync.GetGeoCoordinatesAsync(string key, string[] members, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string[]> IRedisClientAsync.FindGeoMembersInRadiusAsync(string key, double longitude, double latitude, double radius, string unit, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<RedisGeoResult>> IRedisClientAsync.FindGeoResultsInRadiusAsync(string key, double longitude, double latitude, double radius, string unit, int? count, bool? sortByNearest, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string[]> IRedisClientAsync.FindGeoMembersInRadiusAsync(string key, string member, double radius, string unit, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<RedisGeoResult>> IRedisClientAsync.FindGeoResultsInRadiusAsync(string key, string member, double radius, string unit, int? count, bool? sortByNearest, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IRedisSubscriptionAsync IRedisClientAsync.CreateSubscriptionAsync()
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.PublishMessageAsync(string toChannel, string message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.MoveBetweenSetsAsync(string fromSetId, string toSetId, string item, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.SetContainsItemAsync(string setId, string item, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<HashSet<string>> IRedisClientAsync.GetIntersectFromSetsAsync(string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.StoreIntersectFromSetsAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<HashSet<string>> IRedisClientAsync.GetUnionFromSetsAsync(string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.StoreUnionFromSetsAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<HashSet<string>> IRedisClientAsync.GetDifferencesFromSetAsync(string fromSetId, string[] withSetIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.StoreDifferencesFromSetAsync(string intoSetId, string fromSetId, string[] withSetIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.GetRandomItemFromSetAsync(string setId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetAllItemsFromListAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromListAsync(string listId, int startingFrom, int endingAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedListAsync(string listId, int startingFrom, int endingAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetSortedItemsFromListAsync(string listId, SortOptions sortOptions, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.AddRangeToListAsync(string listId, List<string> values, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.PrependItemToListAsync(string listId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.PrependRangeToListAsync(string listId, List<string> values, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.RemoveAllFromListAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.RemoveStartFromListAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.BlockingRemoveStartFromListAsync(string listId, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<ItemRef> IRedisClientAsync.BlockingRemoveStartFromListsAsync(string[] listIds, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.RemoveEndFromListAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.TrimListAsync(string listId, int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.RemoveItemFromListAsync(string listId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.RemoveItemFromListAsync(string listId, string value, int noOfMatches, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.GetItemFromListAsync(string listId, int listIndex, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.SetItemInListAsync(string listId, int listIndex, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.EnqueueItemOnListAsync(string listId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.DequeueItemFromListAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.BlockingDequeueItemFromListAsync(string listId, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<ItemRef> IRedisClientAsync.BlockingDequeueItemFromListsAsync(string[] listIds, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.PushItemToListAsync(string listId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.PopItemFromListAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.BlockingPopItemFromListAsync(string listId, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<ItemRef> IRedisClientAsync.BlockingPopItemFromListsAsync(string[] listIds, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.PopAndPushItemBetweenListsAsync(string fromListId, string toListId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.BlockingPopAndPushItemBetweenListsAsync(string fromListId, string toListId, TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.AddRangeToSortedSetAsync(string setId, List<string> values, double score, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.AddRangeToSortedSetAsync(string setId, List<string> values, long score, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.RemoveItemFromSortedSetAsync(string setId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.RemoveItemsFromSortedSetAsync(string setId, List<string> values, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.PopItemWithLowestScoreFromSortedSetAsync(string setId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.PopItemWithHighestScoreFromSortedSetAsync(string setId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.SortedSetContainsItemAsync(string setId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisClientAsync.IncrementItemInSortedSetAsync(string setId, string value, double incrementBy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisClientAsync.IncrementItemInSortedSetAsync(string setId, string value, long incrementBy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.GetItemIndexInSortedSetAsync(string setId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.GetItemIndexInSortedSetDescAsync(string setId, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetAllItemsFromSortedSetAsync(string setId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetAllItemsFromSortedSetDescAsync(string setId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetDescAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetAllWithScoresFromSortedSetAsync(string setId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetDescAsync(string setId, int fromRank, int toRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByLowestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetRangeFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, string fromStringScore, string toStringScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, double fromScore, double toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<IDictionary<string, double>> IRedisClientAsync.GetRangeWithScoresFromSortedSetByHighestScoreAsync(string setId, long fromScore, long toScore, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.RemoveRangeFromSortedSetAsync(string setId, int minRank, int maxRank, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.RemoveRangeFromSortedSetByScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.RemoveRangeFromSortedSetByScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.StoreIntersectFromSortedSetsAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.StoreIntersectFromSortedSetsAsync(string intoSetId, string[] setIds, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.StoreUnionFromSortedSetsAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.StoreUnionFromSortedSetsAsync(string intoSetId, string[] setIds, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.HashContainsEntryAsync(string hashId, string key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.SetEntryInHashIfNotExistsAsync(string hashId, string key, string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.SetRangeInHashAsync(string hashId, IEnumerable<KeyValuePair<string, string>> keyValuePairs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.IncrementValueInHashAsync(string hashId, string key, int incrementBy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisClientAsync.IncrementValueInHashAsync(string hashId, string key, double incrementBy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.GetValueFromHashAsync(string hashId, string key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetValuesFromHashAsync(string hashId, string[] keys, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.RemoveEntryFromHashAsync(string hashId, string key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetHashKeysAsync(string hashId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.GetHashValuesAsync(string hashId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<Dictionary<string, string>> IRedisClientAsync.GetAllEntriesFromHashAsync(string hashId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<RedisText> IRedisClientAsync.ExecLuaAsync(string body, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<RedisText> IRedisClientAsync.ExecLuaShaAsync(string sha1, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.ExecLuaAsStringAsync(string luaBody, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.ExecLuaShaAsStringAsync(string sha1, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.ExecLuaAsIntAsync(string luaBody, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.ExecLuaAsIntAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.ExecLuaShaAsIntAsync(string sha1, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisClientAsync.ExecLuaShaAsIntAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.ExecLuaAsListAsync(string luaBody, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.ExecLuaAsListAsync(string luaBody, string[] keys, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.ExecLuaShaAsListAsync(string sha1, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<string>> IRedisClientAsync.ExecLuaShaAsListAsync(string sha1, string[] keys, string[] args, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisClientAsync.CalculateSha1Async(string luaBody, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisClientAsync.HasLuaScriptAsync(string sha1Ref, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<Dictionary<string, bool>> IRedisClientAsync.WhichLuaScriptsExistsAsync(string[] sha1Refs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.RemoveAllLuaScriptsAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisClientAsync.KillRunningLuaScriptAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
 