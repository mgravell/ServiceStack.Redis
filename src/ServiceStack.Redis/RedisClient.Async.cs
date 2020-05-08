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

        internal async ValueTask<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
            => await NativeAsync.DelAsync(key, cancellationToken).ConfigureAwait(false) == Success;

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
                    ? NativeAsync.ScanAsync(ret?.Cursor ?? 0, pageSize, match: pattern)
                    : NativeAsync.ScanAsync(ret?.Cursor ?? 0, pageSize)
                    ).ConfigureAwait(false);

                foreach (var key in ret.Results)
                {
                    yield return key.FromUtf8Bytes();
                }

                if (ret.Cursor == 0) break;
            }
        }
    }
}
 