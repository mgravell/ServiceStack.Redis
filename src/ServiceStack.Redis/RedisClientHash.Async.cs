//
// https://github.com/ServiceStack/ServiceStack.Redis
// ServiceStack.Redis: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Demis Bellot (demis.bellot@gmail.com)
//
// Copyright 2013 Service Stack LLC. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//

using ServiceStack.Redis.Internal;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    internal partial class RedisClientHash
        : IRedisHashAsync
    {
        private IRedisClientAsync AsyncClient => client;
        ValueTask<bool> IRedisHashAsync.AddIfNotExistsAsync(KeyValuePair<string, string> item, CancellationToken cancellationToken)
            => AsyncClient.SetEntryInHashIfNotExistsAsync(hashId, item.Key, item.Value, cancellationToken);

        ValueTask IRedisHashAsync.AddRangeAsync(IEnumerable<KeyValuePair<string, string>> items, CancellationToken cancellationToken)
            => AsyncClient.SetRangeInHashAsync(hashId, items, cancellationToken);

        ValueTask<int> IRedisHashAsync.CountAsync(CancellationToken cancellationToken)
            => AsyncClient.GetHashCountAsync(hashId, cancellationToken).AsInt32();

        IAsyncEnumerator<KeyValuePair<string, string>> IAsyncEnumerable<KeyValuePair<string, string>>.GetAsyncEnumerator(CancellationToken cancellationToken)
            => AsyncClient.ScanAllHashEntriesAsync(hashId).GetAsyncEnumerator(cancellationToken); // note: we're using HSCAN here, not HGETALL

        ValueTask<long> IRedisHashAsync.IncrementValue(string key, int incrementBy, CancellationToken cancellationToken)
            => AsyncClient.IncrementValueInHashAsync(hashId, key, incrementBy, cancellationToken);
    }
}