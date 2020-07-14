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

namespace ServiceStack.Redis
{
    internal partial class RedisClientList
        : IRedisListAsync
    {
        private IRedisClientAsync AsyncClient => client;
        private IRedisListAsync AsAsync() => this;

        ValueTask IRedisListAsync.AppendAsync(string value, CancellationToken cancellationToken)
            => AsyncClient.AddItemToListAsync(listId, value, cancellationToken);

        ValueTask<string> IRedisListAsync.BlockingDequeueAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
            => AsyncClient.BlockingDequeueItemFromListAsync(listId, timeOut, cancellationToken);

        ValueTask<string> IRedisListAsync.BlockingPopAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
            => AsyncClient.BlockingPopItemFromListAsync(listId, timeOut, cancellationToken);

        ValueTask<string> IRedisListAsync.BlockingRemoveStartAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
            => AsyncClient.BlockingRemoveStartFromListAsync(listId, timeOut, cancellationToken);

        ValueTask<int> IRedisListAsync.CountAsync(CancellationToken cancellationToken)
            => AsyncClient.GetListCountAsync(listId, cancellationToken).AsInt32();

        ValueTask<string> IRedisListAsync.DequeueAsync(CancellationToken cancellationToken)
            => AsyncClient.DequeueItemFromListAsync(listId);

        ValueTask IRedisListAsync.EnqueueAsync(string value, CancellationToken cancellationToken)
            => AsyncClient.EnqueueItemOnListAsync(listId, value, cancellationToken);

        ValueTask<List<string>> IRedisListAsync.GetAllAsync(CancellationToken cancellationToken)
            => AsyncClient.GetAllItemsFromListAsync(listId, cancellationToken);


        async IAsyncEnumerator<string> IAsyncEnumerable<string>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var count = await AsAsync().CountAsync(cancellationToken).ConfigureAwait(false);
            if (count <= PageLimit)
            {
                var all = await AsyncClient.GetAllItemsFromListAsync(listId, cancellationToken).ConfigureAwait(false);
                foreach (var item in all)
                {
                    yield return item;
                }
            }
            else
            {
                // from GetPagingEnumerator()
                var skip = 0;
                List<string> pageResults;
                do
                {
                    pageResults = await AsyncClient.GetRangeFromListAsync(listId, skip, skip + PageLimit - 1, cancellationToken).ConfigureAwait(false);
                    foreach (var result in pageResults)
                    {
                        yield return result;
                    }
                    skip += PageLimit;
                } while (pageResults.Count == PageLimit);
            }
        }

        ValueTask<List<string>> IRedisListAsync.GetRangeAsync(int startingFrom, int endingAt, CancellationToken cancellationToken)
            => AsyncClient.GetRangeFromListAsync(listId, startingFrom, endingAt, cancellationToken);

        ValueTask<List<string>> IRedisListAsync.GetRangeFromSortedListAsync(int startingFrom, int endingAt, CancellationToken cancellationToken)
            => AsyncClient.GetRangeFromSortedListAsync(listId, startingFrom, endingAt, cancellationToken);

        ValueTask<string> IRedisListAsync.PopAndPushAsync(IRedisListAsync toList, CancellationToken cancellationToken)
            => AsyncClient.PopAndPushItemBetweenListsAsync(listId, toList.Id, cancellationToken);

        ValueTask<string> IRedisListAsync.PopAsync(CancellationToken cancellationToken)
            => AsyncClient.PopItemFromListAsync(listId, cancellationToken);

        ValueTask IRedisListAsync.PrependAsync(string value, CancellationToken cancellationToken)
            => AsyncClient.PrependItemToListAsync(listId, value, cancellationToken);

        ValueTask IRedisListAsync.PushAsync(string value, CancellationToken cancellationToken)
            => AsyncClient.PushItemToListAsync(listId, value, cancellationToken);

        ValueTask IRedisListAsync.RemoveAllAsync(CancellationToken cancellationToken)
            => AsyncClient.RemoveAllFromListAsync(listId, cancellationToken);

        ValueTask<string> IRedisListAsync.RemoveEndAsync(CancellationToken cancellationToken)
            => AsyncClient.RemoveEndFromListAsync(listId, cancellationToken);

        ValueTask<string> IRedisListAsync.RemoveStartAsync(CancellationToken cancellationToken)
            => AsyncClient.RemoveStartFromListAsync(listId, cancellationToken);

        ValueTask<long> IRedisListAsync.RemoveValueAsync(string value, CancellationToken cancellationToken)
            => AsyncClient.RemoveItemFromListAsync(listId, value, cancellationToken);

        ValueTask<long> IRedisListAsync.RemoveValueAsync(string value, int noOfMatches, CancellationToken cancellationToken)
            => AsyncClient.RemoveItemFromListAsync(listId, value, noOfMatches, cancellationToken);

        ValueTask IRedisListAsync.TrimAsync(int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken)
            => AsyncClient.TrimListAsync(listId, keepStartingFrom, keepEndingAt, cancellationToken);
    }
}