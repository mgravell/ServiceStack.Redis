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
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Generic
{
    internal partial class RedisClientList<T>
        : IRedisListAsync<T>
    {
        IRedisTypedClientAsync<T> AsyncClient => client;
        IRedisListAsync<T> AsAsync() => this;

        async ValueTask IRedisListAsync<T>.AddRangeAsync(IEnumerable<T> values, CancellationToken cancellationToken)
        {
            //TODO: replace it with a pipeline implementation ala AddRangeToSet
            foreach (var value in values)
            {
                await AsyncClient.AddItemToListAsync(this, value, cancellationToken).ConfigureAwait(false);
            }
        }

        ValueTask IRedisListAsync<T>.AppendAsync(T value, CancellationToken cancellationToken)
            => AsyncClient.AddItemToListAsync(this, value, cancellationToken);

        ValueTask<T> IRedisListAsync<T>.BlockingDequeueAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
            => AsyncClient.BlockingDequeueItemFromListAsync(this, timeOut, cancellationToken);

        ValueTask<T> IRedisListAsync<T>.BlockingPopAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
            => AsyncClient.BlockingPopItemFromListAsync(this, timeOut, cancellationToken);

        ValueTask<T> IRedisListAsync<T>.BlockingRemoveStartAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
            => AsyncClient.BlockingRemoveStartFromListAsync(this, timeOut, cancellationToken);

        ValueTask<int> IRedisListAsync<T>.CountAsync(CancellationToken cancellationToken)
            => AsyncClient.GetListCountAsync(this, cancellationToken).AsInt32();

        ValueTask<T> IRedisListAsync<T>.DequeueAsync(CancellationToken cancellationToken)
            => AsyncClient.DequeueItemFromListAsync(this, cancellationToken);

        ValueTask IRedisListAsync<T>.EnqueueAsync(T value, CancellationToken cancellationToken)
            => AsyncClient.EnqueueItemOnListAsync(this, value, cancellationToken);

        ValueTask<List<T>> IRedisListAsync<T>.GetAllAsync(CancellationToken cancellationToken)
            => AsyncClient.GetAllItemsFromListAsync(this, cancellationToken);

        async IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var count = await AsAsync().CountAsync(cancellationToken).ConfigureAwait(false);
            if (count <= PageLimit)
            {
                var all = await AsyncClient.GetAllItemsFromListAsync(this, cancellationToken).ConfigureAwait(false);
                foreach (var item in all)
                {
                    yield return item;
                }
            }
            else
            {
                // from GetPagingEnumerator()
                var skip = 0;
                List<T> pageResults;
                do
                {
                    pageResults = await AsyncClient.GetRangeFromListAsync(this, skip, PageLimit, cancellationToken).ConfigureAwait(false);
                    foreach (var result in pageResults)
                    {
                        yield return result;
                    }
                    skip += PageLimit;
                } while (pageResults.Count == PageLimit);
            }
        }

        ValueTask<List<T>> IRedisListAsync<T>.GetRangeAsync(int startingFrom, int endingAt, CancellationToken cancellationToken)
            => AsyncClient.GetRangeFromListAsync(this, startingFrom, endingAt, cancellationToken);

        ValueTask<List<T>> IRedisListAsync<T>.GetRangeFromSortedListAsync(int startingFrom, int endingAt, CancellationToken cancellationToken)
            => AsyncClient.SortListAsync(this, startingFrom, endingAt, cancellationToken);

        ValueTask<T> IRedisListAsync<T>.PopAndPushAsync(IRedisListAsync<T> toList, CancellationToken cancellationToken)
            => AsyncClient.PopAndPushItemBetweenListsAsync(this, toList, cancellationToken);

        ValueTask<T> IRedisListAsync<T>.PopAsync(CancellationToken cancellationToken)
            => AsyncClient.PopItemFromListAsync(this, cancellationToken);

        ValueTask IRedisListAsync<T>.PrependAsync(T value, CancellationToken cancellationToken)
            => AsyncClient.PrependItemToListAsync(this, value, cancellationToken);

        ValueTask IRedisListAsync<T>.PushAsync(T value, CancellationToken cancellationToken)
            => AsyncClient.PushItemToListAsync(this, value, cancellationToken);

        ValueTask IRedisListAsync<T>.RemoveAllAsync(CancellationToken cancellationToken)
            => AsyncClient.RemoveAllFromListAsync(this, cancellationToken);

        ValueTask<T> IRedisListAsync<T>.RemoveEndAsync(CancellationToken cancellationToken)
            => AsyncClient.RemoveEndFromListAsync(this, cancellationToken);

        ValueTask<T> IRedisListAsync<T>.RemoveStartAsync(CancellationToken cancellationToken)
            => AsyncClient.RemoveStartFromListAsync(this, cancellationToken);

        ValueTask<long> IRedisListAsync<T>.RemoveValueAsync(T value, CancellationToken cancellationToken)
            => AsyncClient.RemoveItemFromListAsync(this, value, cancellationToken);

        ValueTask<long> IRedisListAsync<T>.RemoveValueAsync(T value, int noOfMatches, CancellationToken cancellationToken)
            => AsyncClient.RemoveItemFromListAsync(this, value, noOfMatches, cancellationToken);

        ValueTask IRedisListAsync<T>.TrimAsync(int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken)
            => AsyncClient.TrimListAsync(this, keepStartingFrom, keepEndingAt, cancellationToken);
    }
}