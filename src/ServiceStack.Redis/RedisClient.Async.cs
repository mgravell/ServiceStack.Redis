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
using System;
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

        ValueTask<byte[]> IRedisClientAsync.GetAsync(string key, CancellationToken cancellationToken)
            => NativeAsync.GetAsync(key, cancellationToken);

        async ValueTask<bool> IRedisClientAsync.RemoveAsync(string key, CancellationToken cancellationToken)
        {
            return await NativeAsync.DelAsync(key, cancellationToken).ConfigureAwait(false) == Success;
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
    }
}