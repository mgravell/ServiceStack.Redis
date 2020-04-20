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
#if ASYNC_REDIS
using ServiceStack.Caching;
using ServiceStack.Redis.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisClient : IAsyncRedisClient, IAsyncRemoveByPattern
    {
        // the typed client implements this for us
        IAsyncRedisTypedClient<T> IAsyncRedisClient.As<T>() => (IAsyncRedisTypedClient<T>)As<T>();

        Task<DateTime> IAsyncRedisClient.GetServerTime(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
#endif