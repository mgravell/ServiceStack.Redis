//
// https://github.com/ServiceStack/ServiceStack.Redis
// ServiceStack.Redis: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Demis Bellot Async(demis.bellot@gmail.com, CancellationToken cancellationToken = default)
//
// Copyright 2017 ServiceStack, Inc. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Model;

namespace ServiceStack.Redis.Generic
{
    public interface IRedisSetAsync<T> : IAsyncEnumerable<T>, IHasStringId
    {
        ValueTask<List<T>> SortAsync(int startingFrom, int endingAt, CancellationToken cancellationToken = default);
        ValueTask<HashSet<T>> GetAllAsync(CancellationToken cancellationToken = default);
        ValueTask<T> PopRandomItemAsync(CancellationToken cancellationToken = default);
        ValueTask<T> GetRandomItemAsync(CancellationToken cancellationToken = default);
        ValueTask MoveToAsync(T item, IRedisSetAsync<T> toSet, CancellationToken cancellationToken = default);
        ValueTask PopulateWithIntersectOfAsync(IRedisSetAsync<T>[] sets, CancellationToken cancellationToken = default);
        ValueTask PopulateWithUnionOfAsync(IRedisSetAsync<T>[] sets, CancellationToken cancellationToken = default);
        ValueTask GetDifferencesAsync(IRedisSetAsync<T>[] withSets, CancellationToken cancellationToken = default);
        ValueTask PopulateWithDifferencesOfAsync(IRedisSetAsync<T> fromSet, IRedisSetAsync<T>[] withSets, CancellationToken cancellationToken = default);
    }

}