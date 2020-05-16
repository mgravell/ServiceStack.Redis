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
                var client = syncClient.AsAsync();
                const string cacheKey = "urn+metadata:All:SearchProProfiles?SwanShinichi Osawa /0/8,0,0,0";
                const long value = 1L;
                await client.SetValueAsync(cacheKey, value);
                var result = await client.GetValueAsync<long>(cacheKey);

                Assert.That(result, Is.EqualTo(value));
            }
        }

        // remaining tests from parent do not touch redis
    }
}