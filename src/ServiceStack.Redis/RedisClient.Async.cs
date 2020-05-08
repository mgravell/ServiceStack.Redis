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

        internal ValueTask<byte[]> GetAsync(string key, CancellationToken cancellationToken = default)
            => NativeAsync.GetAsync(key, cancellationToken);

        internal ValueTask<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
            => IsSuccess(NativeAsync.DelAsync(key, cancellationToken));

        internal async ValueTask<bool> SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            await Exec(r => ((IRedisNativeClientAsync)r).SetAsync(key, ToBytes(value), cancellationToken));
            return true;
        }

        internal async ValueTask Exec(Func<IRedisClientAsync, ValueTask> action)
        {
            using (JsConfig.With(new Text.Config { ExcludeTypeInfo = false }))
            {
                await action(this).ConfigureAwait(false);
            }
        }

        ValueTask IRedisClientAsync.SetValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            var bytesValue = value?.ToUtf8Bytes();
            return NativeAsync.SetAsync(key, bytesValue, cancellationToken);
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
    }
}
 