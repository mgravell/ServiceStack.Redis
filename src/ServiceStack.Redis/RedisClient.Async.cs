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
using ServiceStack.Redis.Generic;
using ServiceStack.Redis.Pipeline;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisClient : IRedisClientAsync, IRemoveByPatternAsync
    {
        // the typed client implements this for us
        IRedisTypedClientAsync<T> IRedisClientAsync.As<T>() => (IRedisTypedClientAsync<T>)As<T>();

        // convenience since we're not saturating the public API; this makes it easy to call
        // the explicit interface implementations; the JIT should make this a direct call
        private IRedisNativeClientAsync NativeAsync => this;

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

        ValueTask<bool> IRedisClientAsync.RemoveEntryAsync(string key, CancellationToken cancellationToken)
            => IsSuccess(NativeAsync.DelAsync(key, cancellationToken));

        ValueTask<bool> IRedisClientAsync.RemoveEntryAsync(string[] keys, CancellationToken cancellationToken)
            => keys.Length == 0 ? default : IsSuccess(NativeAsync.DelAsync(keys, cancellationToken));

        internal async ValueTask<bool> SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            await Exec(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), cancellationToken: cancellationToken));
            return true;
        }

        private async ValueTask Exec(Func<IRedisClientAsync, ValueTask> action)
        {
            using (JsConfig.With(new Text.Config { ExcludeTypeInfo = false }))
            {
                await action(this).ConfigureAwait(false);
            }
        }

        ValueTask IRedisClientAsync.SetValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            var bytesValue = value?.ToUtf8Bytes();
            return NativeAsync.SetAsync(key, bytesValue, cancellationToken: cancellationToken);
        }

        async ValueTask<string> IRedisClientAsync.GetValueAsync(string key, CancellationToken cancellationToken)
        {
            var bytes = await NativeAsync.GetAsync(key, cancellationToken).ConfigureAwait(false);
            return bytes?.FromUtf8Bytes();
        }

        async ValueTask<List<string>> IRedisClientAsync.SearchKeysAsync(string pattern, CancellationToken cancellationToken)
        {
            var list = new List<string>();
            await foreach (var value in ((IRedisClientAsync)this).ScanAllKeysAsync(pattern, cancellationToken: cancellationToken))
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

        ValueTask IRedisClientAsync.SetAllAsync(Dictionary<string, string> map, CancellationToken cancellationToken)
            => GetSetAllBytes(map, out var keyBytes, out var valBytes) ? NativeAsync.MSetAsync(keyBytes, valBytes, cancellationToken) : default;

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
    }
}
 