using ServiceStack.Caching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisClientManagerCacheClient : ICacheClientAsync, IRemoveByPatternAsync, ICacheClientExtendedAsync
    {
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            Dispose();
            return default;
        }

        private ValueTask<ICacheClientAsync> GetReadOnlyClientAsync(in CancellationToken cancellationToken)
        {
            AssertNotReadOnly();
            return redisManager.GetCacheClientAsync(cancellationToken);
        }

        async ValueTask<T> ICacheClientAsync.GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            await using var client = await GetReadOnlyClientAsync(cancellationToken).ConfigureAwait(false);
            return await client.GetAsync<T>(key).ConfigureAwait(false);
        }

        async ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            await using var client = await redisManager.GetCacheClientAsync(cancellationToken).ConfigureAwait(false);
            return await client.SetAsync<T>(key, value, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, DateTime expiresAt, CancellationToken cancellationToken)
        {
            await using var client = await redisManager.GetCacheClientAsync(cancellationToken).ConfigureAwait(false);
            return await client.SetAsync<T>(key, value, expiresAt, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<bool> ICacheClientAsync.SetAsync<T>(string key, T value, TimeSpan expiresIn, CancellationToken cancellationToken)
        {
            await using var client = await redisManager.GetCacheClientAsync(cancellationToken).ConfigureAwait(false);
            return await client.SetAsync<T>(key, value, expiresIn, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask ICacheClientAsync.FlushAllAsync(CancellationToken cancellationToken)
        {
            await using var client = await redisManager.GetCacheClientAsync(cancellationToken).ConfigureAwait(false);
            await client.FlushAllAsync(cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<IDictionary<string, T>> ICacheClientAsync.GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            await using var client = await GetReadOnlyClientAsync(cancellationToken).ConfigureAwait(false);
            return await client.GetAllAsync<T>(keys, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask ICacheClientAsync.SetAllAsync<T>(IDictionary<string, T> values, CancellationToken cancellationToken)
        {
            await using var client = await redisManager.GetCacheClientAsync(cancellationToken).ConfigureAwait(false);
            await client.SetAllAsync<T>(values, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<bool> ICacheClientAsync.RemoveAsync(string key, CancellationToken cancellationToken)
        {
            await using var client = await redisManager.GetCacheClientAsync(cancellationToken).ConfigureAwait(false);
            return await client.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }
}