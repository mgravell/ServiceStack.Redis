using System;

namespace ServiceStack.Redis.Tests
{
    public abstract class RedisClientTestsBaseAsync : RedisClientTestsBase
    {
        protected IRedisClientAsync RedisAsync => base.Redis;
        protected IRedisNativeClientAsync NativeAsync => base.Redis;

        [Obsolete("This should use RedisAsync or RedisRaw")]
        protected new RedisClient Redis => base.Redis;

        protected RedisClient RedisRaw => base.Redis;
    }
}