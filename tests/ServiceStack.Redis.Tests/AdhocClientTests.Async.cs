using NUnit.Framework;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Tests
{
    [TestFixture, Category("Integration")]
    public class AdhocClientTestsAsync
    {
        [Test]
        public async Task Search_Test()
        {
            using (var syncClient = new RedisClient(TestConfig.SingleHost))
            {
                var client = syncClient.AsAsyncCacheClient();
                const string cacheKey = "urn+metadata:All:SearchProProfiles?SwanShinichi Osawa /0/8,0,0,0";
                const long value = 1L;
                await client.SetAsync(cacheKey, value);
                var result = await client.GetAsync<long>(cacheKey);

                Assert.That(result, Is.EqualTo(value));
            }
        }

        // remaining tests from parent do not touch redis
    }
}