using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Model;

namespace ServiceStack.Redis
{
    public interface IRedisHashAsync
        : IAsyncEnumerable<KeyValuePair<string, string>>, IHasStringId
    {
        ValueTask<int> CountAsync(CancellationToken cancellationToken = default);
        ValueTask<bool> AddIfNotExistsAsync(KeyValuePair<string, string> item, CancellationToken cancellationToken = default);
        ValueTask AddRangeAsync(IEnumerable<KeyValuePair<string, string>> items, CancellationToken cancellationToken = default);
        ValueTask<long> IncrementValue(string key, int incrementBy, CancellationToken cancellationToken = default);
    }
}