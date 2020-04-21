#if ASYNC_REDIS
using System;
using System.Collections.Generic;

namespace ServiceStack.Caching
{
    /// <summary>
    /// Extend ICacheClient API with shared, non-core features
    /// </summary>
    public interface ICacheClientExtendedAsync : ICacheClientAsync
    {
        //TimeSpan? GetTimeToLive(string key);

        //IEnumerable<string> GetKeysByPattern(string pattern);

        //void RemoveExpiredEntries();
    }
}
#endif