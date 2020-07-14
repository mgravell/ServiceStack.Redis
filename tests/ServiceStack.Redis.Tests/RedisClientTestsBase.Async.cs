using NUnit.Framework;
using System;

namespace ServiceStack.Redis.Tests
{
    public class RedisClientTestsBaseAsyncTests // testing the base class features
        : RedisClientTestsBaseAsync
    {
        [Test]
        public void DetectUnexpectedSync()
        {
    #if DEBUG
            Assert.False(RedisRaw.DebugAllowSync, nameof(RedisRaw.DebugAllowSync));
            var ex = Assert.Throws<InvalidOperationException>(() => RedisRaw.Ping());
            Assert.AreEqual("Unexpected synchronous operation detected from 'SendReceive'", ex.Message);
    #endif
        }
    }

    [Category("Async")]
    public abstract class RedisClientTestsBaseAsync : RedisClientTestsBase
    {
        protected IRedisClientAsync RedisAsync => base.Redis;
        protected IRedisNativeClientAsync NativeAsync => base.Redis;

        [Obsolete("This should use RedisAsync or RedisRaw")]
        protected new RedisClient Redis => base.Redis;

        protected RedisClient RedisRaw
        {
            get => base.Redis;
            set => base.Redis = value;
        }

        public override void OnBeforeEachTest()
        {
            base.OnBeforeEachTest();
            ForAsyncOnly(RedisRaw);
        }
        public override void OnAfterEachTest()
        {
#if DEBUG
            if(RedisRaw is object) RedisRaw.DebugAllowSync = true;
#endif
            base.OnAfterEachTest();
        }

        protected internal static IRedisClientAsync ForAsyncOnly(RedisClient client)
        {
#if DEBUG
            if (client is object) client.DebugAllowSync = false;
#endif
            return client;
        }
    }
}