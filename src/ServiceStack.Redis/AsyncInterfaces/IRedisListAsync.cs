//
// https://github.com/ServiceStack/ServiceStack.Redis
// ServiceStack.Redis: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Demis Bellot Async(demis.bellot@gmail.com)
//
// Copyright 2017 ServiceStack, Inc. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Model;

namespace ServiceStack.Redis
{
    public interface IRedisListAsync
        : IAsyncEnumerable<string>, IHasStringId
    {
        ValueTask<List<string>> GetAllAsync(CancellationToken cancellationToken = default);
        ValueTask<List<string>> GetRangeAsync(int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        ValueTask<List<string>> GetRangeFromSortedListAsync(int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        ValueTask RemoveAllAsync(CancellationToken cancellationToken = default);
        ValueTask TrimAsync(int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken = default);
        ValueTask<long> RemoveValueAsync(string value, CancellationToken cancellationToken = default);
        ValueTask<long> RemoveValueAsync(string value, int noOfMatches, CancellationToken cancellationToken = default);

        ValueTask PrependAsync(string value, CancellationToken cancellationToken = default);
        ValueTask AppendAsync(string value, CancellationToken cancellationToken = default);
        ValueTask<string> RemoveStartAsync(CancellationToken cancellationToken = default);
        ValueTask<string> BlockingRemoveStartAsync(TimeSpan? timeOut, CancellationToken cancellationToken = default);
        ValueTask<string> RemoveEndAsync(CancellationToken cancellationToken = default);

        ValueTask EnqueueAsync(string value, CancellationToken cancellationToken = default);
        ValueTask<string> DequeueAsync(CancellationToken cancellationToken = default);
        ValueTask<string> BlockingDequeueAsync(TimeSpan? timeOut, CancellationToken cancellationToken = default);

        ValueTask PushAsync(string value, CancellationToken cancellationToken = default);
        ValueTask<string> PopAsync(CancellationToken cancellationToken = default);
        ValueTask<string> BlockingPopAsync(TimeSpan? timeOut, CancellationToken cancellationToken = default);
        ValueTask<string> PopAndPushAsync(IRedisListAsync toList, CancellationToken cancellationToken = default);
    }
}