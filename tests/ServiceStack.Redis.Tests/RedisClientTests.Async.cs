using NUnit.Framework;
using ServiceStack.Redis.Support.Locking;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Tests
{
    [TestFixture, Category("Integration")]
    public class RedisClientTestsAsync
        : RedisClientTestsBaseAsync
    {
        const string Value = "Value";

        public override void OnBeforeEachTest()
        {
            base.OnBeforeEachTest();
            RedisRaw.NamespacePrefix = "RedisClientTestsAsync";
        }

        [Test]
        public async Task Can_Set_and_Get_string()
        {
            await RedisAsync.SetValueAsync("key", Value);
            var valueBytes = await RedisRaw.GetAsync("key");
            var valueString = GetString(valueBytes);
            await RedisRaw.RemoveAsync("key");

            Assert.That(valueString, Is.EqualTo(Value));
        }

        [Test]
        public async Task Can_Set_and_Get_key_with_space()
        {
            await RedisAsync.SetValueAsync("key with space", Value);
            var valueBytes = await RedisRaw.GetAsync("key with space");
            var valueString = GetString(valueBytes);
            await RedisRaw.RemoveAsync("key with space");

            Assert.That(valueString, Is.EqualTo(Value));
        }

        [Test]
        public async Task Can_Set_and_Get_key_with_spaces()
        {
            const string key = "key with spaces";

            await RedisAsync.SetValueAsync(key, Value);
            var valueBytes = await RedisRaw.GetAsync(key);
            var valueString = GetString(valueBytes);

            Assert.That(valueString, Is.EqualTo(Value));
        }

        [Test]
        public async Task Can_Set_and_Get_key_with_all_byte_values()
        {
            const string key = "bytesKey";

            var value = new byte[256];
            for (var i = 0; i < value.Length; i++)
            {
                value[i] = (byte)i;
            }

            var redis = RedisAsync.As<byte[]>();

            await redis.SetValueAsync(key, value);
            var resultValue = await redis.GetValueAsync(key);

            Assert.That(resultValue, Is.EquivalentTo(value));
        }

        [Test]
        public async Task GetKeys_returns_matching_collection()
        {
            await RedisRaw.SetAsync("ss-tests:a1", "One");
            await RedisRaw.SetAsync("ss-tests:a2", "One");
            await RedisRaw.SetAsync("ss-tests:b3", "One");
            var matchingKeys = await RedisAsync.SearchKeysAsync("ss-tests:a*");

            Assert.That(matchingKeys.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetKeys_on_non_existent_keys_returns_empty_collection()
        {
            var matchingKeys = await RedisAsync.SearchKeysAsync("ss-tests:NOTEXISTS");

            Assert.That(matchingKeys.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task Can_get_Types()
        {
            await RedisAsync.SetValueAsync("string", "string");
            await RedisAsync.AddItemToListAsync("list", "list");
            await RedisAsync.AddItemToSetAsync("set", "set");
            await RedisAsync.AddItemToSortedSetAsync("sortedset", "sortedset");
            await RedisAsync.SetEntryInHashAsync("hash", "key", "val");

            Assert.That(await RedisAsync.GetEntryTypeAsync("nokey"), Is.EqualTo(RedisKeyType.None));
            Assert.That(await RedisAsync.GetEntryTypeAsync("string"), Is.EqualTo(RedisKeyType.String));
            Assert.That(await RedisAsync.GetEntryTypeAsync("list"), Is.EqualTo(RedisKeyType.List));
            Assert.That(await RedisAsync.GetEntryTypeAsync("set"), Is.EqualTo(RedisKeyType.Set));
            Assert.That(await RedisAsync.GetEntryTypeAsync("sortedset"), Is.EqualTo(RedisKeyType.SortedSet));
            Assert.That(await RedisAsync.GetEntryTypeAsync("hash"), Is.EqualTo(RedisKeyType.Hash));
        }

        [Test]
        public async Task Can_delete_keys()
        {
            await RedisAsync.SetValueAsync("key", "val");

            Assert.That(await RedisAsync.ContainsKeyAsync("key"), Is.True);

            await RedisRaw.DelAsync("key");

            Assert.That(await RedisAsync.ContainsKeyAsync("key"), Is.False);

            var keysMap = new Dictionary<string, string>();

            10.Times(i => keysMap.Add("key" + i, "val" + i));

            await RedisAsync.SetAllAsync(keysMap);

            10.TimesAsync(async i => Assert.That(await RedisAsync.ContainsKeyAsync("key" + i), Is.True));

            await RedisRaw.DelAsync(keysMap.Keys.ToArray());

            10.TimesAsync(async i => Assert.That(await RedisAsync.ContainsKeyAsync("key" + i), Is.False));
        }

        [Test]
        public async Task Can_get_RandomKey()
        {
            RedisAsync.Db = 15;
            var keysMap = new Dictionary<string, string>();

            10.Times(i => keysMap.Add(RedisRaw.NamespacePrefix + "key" + i, "val" + i));

            await RedisAsync.SetAllAsync(keysMap);

            var randKey = await RedisAsync.GetRandomKeyAsync();

            Assert.That(keysMap.ContainsKey(randKey), Is.True);
        }

        [Test]
        public async Task Can_RenameKey()
        {
            await RedisAsync.SetValueAsync("oldkey", "val");
            await RedisAsync.RenameKeyAsync("oldkey", "newkey");

            Assert.That(await RedisAsync.ContainsKeyAsync("oldkey"), Is.False);
            Assert.That(await RedisAsync.ContainsKeyAsync("newkey"), Is.True);
        }

        /*
        [Test]
        public async Task Can_Expire()
        {
            Redis.SetValue("key", "val");
            Redis.Expire("key", 1);
            Assert.That(Redis.ContainsKey("key"), Is.True);
            Thread.Sleep(2000);
            Assert.That(Redis.ContainsKey("key"), Is.False);
        }

        [Test]
        public async Task Can_Expire_Ms()
        {
            Redis.SetValue("key", "val");
            Redis.ExpireEntryIn("key", TimeSpan.FromMilliseconds(100));
            Assert.That(Redis.ContainsKey("key"), Is.True);
            Thread.Sleep(500);
            Assert.That(Redis.ContainsKey("key"), Is.False);
        }

        [Ignore("Changes in system clock can break test")]
        [Test]
        public async Task Can_ExpireAt()
        {
            Redis.SetValue("key", "val");

            var unixNow = DateTime.Now.ToUnixTime();
            var in2Secs = unixNow + 2;

            Redis.ExpireAt("key", in2Secs);

            Assert.That(Redis.ContainsKey("key"), Is.True);
            Thread.Sleep(3000);
            Assert.That(Redis.ContainsKey("key"), Is.False);
        }

        [Test]
        public async Task Can_GetTimeToLive()
        {
            Redis.SetValue("key", "val");
            Redis.Expire("key", 10);

            var ttl = Redis.GetTimeToLive("key");
            Assert.That(ttl.Value.TotalSeconds, Is.GreaterThanOrEqualTo(9));
            Thread.Sleep(1700);

            ttl = Redis.GetTimeToLive("key");
            Assert.That(ttl.Value.TotalSeconds, Is.LessThanOrEqualTo(9));
        }

        [Test]
        public async Task Can_GetServerTime()
        {
            var now = Redis.GetServerTime();

            now.Kind.PrintDump();
            now.ToString("D").Print();
            now.ToString("T").Print();

            "UtcNow".Print();
            DateTime.UtcNow.ToString("D").Print();
            DateTime.UtcNow.ToString("T").Print();

            Assert.That(now.Date, Is.EqualTo(DateTime.UtcNow.Date));
        }

        [Test]
        public async Task Can_Ping()
        {
            Assert.That(Redis.Ping(), Is.True);
        }

        [Test]
        public async Task Can_Echo()
        {
            Assert.That(Redis.Echo("Hello"), Is.EqualTo("Hello"));
        }

        [Test]
        public async Task Can_SlaveOfNoOne()
        {
            Redis.SlaveOfNoOne();
        }

        [Test]
        public async Task Can_Save()
        {
            try
            {
                Redis.Save();
            }
            catch (RedisResponseException e)
            {
                // if exception has that message then it still proves that BgSave works as expected.
                if (e.Message.StartsWith("Can't BGSAVE while AOF log rewriting is in progress")
                    || e.Message.StartsWith("An AOF log rewriting in progress: can't BGSAVE right now")
                    || e.Message.StartsWith("Background save already in progress"))
                    return;

                throw;
            }
        }

        [Test]
        public async Task Can_BgSave()
        {
            try
            {
                Redis.BgSave();
            }
            catch (RedisResponseException e)
            {
                // if exception has that message then it still proves that BgSave works as expected.
                if (e.Message.StartsWith("Can't BGSAVE while AOF log rewriting is in progress")
                    || e.Message.StartsWith("An AOF log rewriting in progress: can't BGSAVE right now")
                    || e.Message.StartsWith("Background save already in progress"))
                    return;

                throw;
            }
        }

        [Test]
        public async Task Can_Quit()
        {
            Redis.Quit();
            Redis.NamespacePrefix = null;
            CleanMask = null;
        }

        [Test]
        public async Task Can_BgRewriteAof()
        {
            Redis.BgRewriteAof();
        }

        [Test]
        [Ignore("Works too well and shutdown the server")]
        public async Task Can_Shutdown()
        {
            Redis.Shutdown();
        }

        [Test]
        public async Task Can_get_Keys_with_pattern()
        {
            5.Times(i => Redis.SetValue("k1:" + i, "val"));
            5.Times(i => Redis.SetValue("k2:" + i, "val"));

            var keys = Redis.Keys("k1:*");
            Assert.That(keys.Length, Is.EqualTo(5));

            var scanKeys = Redis.ScanAllKeys("k1:*").ToArray();
            Assert.That(scanKeys.Length, Is.EqualTo(5));
        }

        [Test]
        public async Task Can_GetAll()
        {
            var keysMap = new Dictionary<string, string>();

            10.Times(i => keysMap.Add("key" + i, "val" + i));

            Redis.SetAll(keysMap);

            var map = Redis.GetAll<string>(keysMap.Keys);
            var mapKeys = Redis.GetValues(keysMap.Keys.ToList<string>());

            foreach (var entry in keysMap)
            {
                Assert.That(map.ContainsKey(entry.Key), Is.True);
                Assert.That(mapKeys.Contains(entry.Value), Is.True);
            }
        }

        [Test]
        public async Task Can_GetValues_JSON_strings()
        {
            var val = "{\"AuthorId\":0,\"Created\":\"\\/Date(1345961754013)\\/\",\"Name\":\"test\",\"Base64\":\"BQELAAEBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAViA/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP8BWAFYgP8BWAFYAViA/wFYAVgBWID/AVgBWAFYgP8BWAFYAViA/4D/gP+A/4D/AVgBWID/gP8BWID/gP8BWID/gP+A/wFYgP+A/4D/gP8BWID/gP+A/4D/gP+A/wFYAViA/4D/AViA/4D/AVgBWAFYgP8BWAFYAViA/4D/AViA/4D/gP+A/4D/gP8BWAFYgP+A/wFYgP+A/wFYgP+A/4D/gP+A/wFYgP+A/wFYgP+A/4D/gP+A/4D/AVgBWID/gP8BWID/gP8BWAFYAViA/wFYAVgBWID/gP8BWID/gP+A/4D/gP+A/wFYAViA/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP8BWAFYgP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/AVgBWID/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/wFYAViA/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP8BWAFYgP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/AVgBWID/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/4D/gP+A/wFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAFYAVgBWAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}";

            Redis.SetValue("UserLevel/1", val);

            var vals = Redis.GetValues(new List<string>(new[] { "UserLevel/1" }));

            Assert.That(vals.Count, Is.EqualTo(1));
            Assert.That(vals[0], Is.EqualTo(val));
        }

        [Test]
        public async Task Can_AcquireLock()
        {
            var key = PrefixedKey("AcquireLockKey");
            var lockKey = PrefixedKey("Can_AcquireLock");
            Redis.IncrementValue(key); //1

            var asyncResults = 5.TimesAsync(i =>
                IncrementKeyInsideLock(key, lockKey, i, new RedisClient(TestConfig.SingleHost) { NamespacePrefix = Redis.NamespacePrefix }));

            asyncResults.WaitAll(TimeSpan.FromSeconds(5));

            var val = Redis.Get<int>(key);

            Assert.That(val, Is.EqualTo(1 + 5));
        }

        private void IncrementKeyInsideLock(String key, String lockKey, int clientNo, IRedisClient client)
        {
            using (client.AcquireLock(lockKey))
            {
                Debug.WriteLine(String.Format("client {0} acquired lock", clientNo));
                var val = client.Get<int>(key);

                Thread.Sleep(200);

                client.Set(key, val + 1);
                Debug.WriteLine(String.Format("client {0} released lock", clientNo));
            }
        }

        [Test]
        public async Task Can_AcquireLock_TimeOut()
        {
            var key = PrefixedKey("AcquireLockKeyTimeOut");
            var lockKey = PrefixedKey("Can_AcquireLock_TimeOut");
            Redis.IncrementValue(key); //1
            var acquiredLock = Redis.AcquireLock(lockKey);
            var waitFor = TimeSpan.FromMilliseconds(1000);
            var now = DateTime.Now;

            try
            {
                using (var client = new RedisClient(TestConfig.SingleHost))
                {
                    using (client.AcquireLock(lockKey, waitFor))
                    {
                        Redis.IncrementValue(key); //2
                    }
                }
            }
            catch (TimeoutException)
            {
                var val = Redis.Get<int>(key);
                Assert.That(val, Is.EqualTo(1));

                var timeTaken = DateTime.Now - now;
                Assert.That(timeTaken.TotalMilliseconds > waitFor.TotalMilliseconds, Is.True);
                Assert.That(timeTaken.TotalMilliseconds < waitFor.TotalMilliseconds + 1000, Is.True);
                return;
            }
            Assert.Fail("should have Timed out");
        }

        [Test]
        public async Task Can_Append()
        {
            const string expectedString = "Hello, " + "World!";
            Redis.SetValue("key", "Hello, ");
            var currentLength = Redis.AppendToValue("key", "World!");

            Assert.That(currentLength, Is.EqualTo(expectedString.Length));

            var val = Redis.GetValue("key");
            Assert.That(val, Is.EqualTo(expectedString));
        }

        [Test]
        public async Task Can_GetRange()
        {
            const string helloWorld = "Hello, World!";
            Redis.SetValue("key", helloWorld);

            var fromIndex = "Hello, ".Length;
            var toIndex = "Hello, World".Length - 1;

            var expectedString = helloWorld.Substring(fromIndex, toIndex - fromIndex + 1);
            var world = Redis.GetRange("key", fromIndex, toIndex);

            Assert.That(world.Length, Is.EqualTo(expectedString.Length));
        }

        [Test]
        public async Task Can_create_distributed_lock()
        {
            var key = "lockkey";
            int lockTimeout = 2;

            var distributedLock = new DistributedLock();
            long lockExpire;
            Assert.AreEqual(distributedLock.Lock(key, lockTimeout, lockTimeout, out lockExpire, Redis), DistributedLock.LOCK_ACQUIRED);

            //can't re-lock
            distributedLock = new DistributedLock();
            Assert.AreEqual(distributedLock.Lock(key, lockTimeout, lockTimeout, out lockExpire, Redis), DistributedLock.LOCK_NOT_ACQUIRED);

            // re-acquire lock after timeout
            Thread.Sleep(lockTimeout * 1000 + 1000);
            distributedLock = new DistributedLock();
            Assert.AreEqual(distributedLock.Lock(key, lockTimeout, lockTimeout, out lockExpire, Redis), DistributedLock.LOCK_RECOVERED);


            Assert.IsTrue(distributedLock.Unlock(key, lockExpire, Redis));

            //can now lock
            distributedLock = new DistributedLock();
            Assert.AreEqual(distributedLock.Lock(key, lockTimeout, lockTimeout, out lockExpire, Redis), DistributedLock.LOCK_ACQUIRED);


            //cleanup
            Assert.IsTrue(distributedLock.Unlock(key, lockExpire, Redis));
        }

        public class MyPoco
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Test]
        public async Task Can_StoreObject()
        {
            object poco = new MyPoco { Id = 1, Name = "Test" };

            Redis.StoreObject(poco);

            Assert.That(Redis.GetValue(Redis.NamespacePrefix + "urn:mypoco:1"), Is.EqualTo("{\"Id\":1,\"Name\":\"Test\"}"));

            Assert.That(Redis.PopItemFromSet(Redis.NamespacePrefix + "ids:MyPoco"), Is.EqualTo("1"));
        }

        [Test]
        public async Task Can_store_multiple_keys()
        {
            var keys = 5.Times(x => "key" + x);
            var vals = 5.Times(x => "val" + x);

            using (var redis = RedisClient.New())
            {
                redis.SetAll(keys, vals);

                var all = redis.GetValues(keys);
                Assert.AreEqual(vals, all);
            }
        }

        [Test]
        public async Task Can_store_Dictionary()
        {
            var keys = 5.Times(x => "key" + x);
            var vals = 5.Times(x => "val" + x);
            var map = new Dictionary<string, string>();
            keys.ForEach(x => map[x] = "val" + x);

            using (var redis = RedisClient.New())
            {
                redis.SetAll(map);

                var all = redis.GetValuesMap(keys);
                Assert.AreEqual(map, all);
            }
        }

        [Test]
        public async Task Can_store_Dictionary_as_objects()
        {
            var map = new Dictionary<string, object>();
            map["key_a"] = "123";
            map["key_b"] = null;

            using (var redis = RedisClient.New())
            {
                redis.SetAll(map);

                Assert.That(redis.Get<string>("key_a"), Is.EqualTo("123"));
                Assert.That(redis.Get("key_b"), Is.EqualTo(""));
            }
        }


        [Test]
        public async Task Can_store_Dictionary_as_bytes()
        {
            var map = new Dictionary<string, byte[]>();
            map["key_a"] = "123".ToUtf8Bytes();
            map["key_b"] = null;

            using (var redis = RedisClient.New())
            {
                redis.SetAll(map);

                Assert.That(redis.Get<string>("key_a"), Is.EqualTo("123"));
                Assert.That(redis.Get("key_b"), Is.EqualTo(""));
            }
        }

        [Test]
        public async Task Should_reset_slowlog()
        {
            using (var redis = RedisClient.New())
            {
                redis.SlowlogReset();
            }
        }

        [Test]
        public async Task Can_get_showlog()
        {
            using (var redis = RedisClient.New())
            {
                var log = redis.GetSlowlog(10);

                foreach (var t in log)
                {
                    Console.WriteLine(t.Id);
                    Console.WriteLine(t.Duration);
                    Console.WriteLine(t.Timestamp);
                    Console.WriteLine(string.Join(":", t.Arguments));
                }
            }
        }


        [Test]
        public async Task Can_change_db_at_runtime()
        {
            using (var redis = new RedisClient(TestConfig.SingleHost, TestConfig.RedisPort, db: 1))
            {
                var val = Environment.TickCount;
                var key = "test" + val;
                try
                {
                    redis.Set(key, val);
                    redis.ChangeDb(2);
                    Assert.That(redis.Get<int>(key), Is.EqualTo(0));
                    redis.ChangeDb(1);
                    Assert.That(redis.Get<int>(key), Is.EqualTo(val));
                    redis.Dispose();
                }
                finally
                {
                    redis.ChangeDb(1);
                    redis.Del(key);
                }
            }
        }

        [Test]
        public async Task Can_Set_Expire_Seconds()
        {
            Redis.SetValue("key", "val", expireIn: TimeSpan.FromSeconds(1));
            Assert.That(Redis.ContainsKey("key"), Is.True);
            Thread.Sleep(2000);
            Assert.That(Redis.ContainsKey("key"), Is.False);
        }

        [Test]
        public async Task Can_Set_Expire_MilliSeconds()
        {
            Redis.SetValue("key", "val", expireIn: TimeSpan.FromMilliseconds(1000));
            Assert.That(Redis.ContainsKey("key"), Is.True);
            Thread.Sleep(2000);
            Assert.That(Redis.ContainsKey("key"), Is.False);
        }

        [Test]
        public async Task Can_Set_Expire_Seconds_if_exists()
        {
            Assert.That(Redis.SetValueIfExists("key", "val", expireIn: TimeSpan.FromMilliseconds(1500)),
                Is.False);
            Assert.That(Redis.ContainsKey("key"), Is.False);

            Redis.SetValue("key", "val");
            Assert.That(Redis.SetValueIfExists("key", "val", expireIn: TimeSpan.FromMilliseconds(1000)),
                Is.True);
            Assert.That(Redis.ContainsKey("key"), Is.True);

            Thread.Sleep(2000);
            Assert.That(Redis.ContainsKey("key"), Is.False);
        }

        [Test]
        public async Task Can_Set_Expire_Seconds_if_not_exists()
        {
            Assert.That(Redis.SetValueIfNotExists("key", "val", expireIn: TimeSpan.FromMilliseconds(1000)),
                Is.True);
            Assert.That(Redis.ContainsKey("key"), Is.True);

            Assert.That(Redis.SetValueIfNotExists("key", "val", expireIn: TimeSpan.FromMilliseconds(1000)),
                Is.False);

            Thread.Sleep(2000);
            Assert.That(Redis.ContainsKey("key"), Is.False);

            Redis.Remove("key");
            Redis.SetValueIfNotExists("key", "val", expireIn: TimeSpan.FromMilliseconds(1000));
            Assert.That(Redis.ContainsKey("key"), Is.True);
        }
        */
    }

}
