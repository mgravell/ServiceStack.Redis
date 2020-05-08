using System;

namespace ServiceStack.Redis.Tests
{
    public abstract class RedisClientTestsBaseAsync : RedisClientTestsBase
    {
        protected IRedisClientAsync RedisAsync => base.Redis;

        [Obsolete("This should use RedisAsync or RedisSync")]
        protected new RedisClient Redis => base.Redis;

        protected RedisClient RedisSync => base.Redis;
    }
}