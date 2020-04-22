using BenchmarkDotNet.Running;
using ServiceStack.Text;
using System;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Benchmark
{
    class Program
    {
#if DEBUG
        static void Main()
        {
            using (var redis = new RedisClient("127.0.0.1", 6379))
            using (var pipe = redis.CreatePipeline())
            {
                for (int i = 0; i < 5; i++)
                {
                    pipe.QueueCommand(r => r.GetServerTime().ToUnixTime(), t => Console.WriteLine(t));
                }
                pipe.Flush();
            }
        }
        static async Task Main2()
        {
            var obj = new ServerTime();
            try
            {
                await obj.Setup(true);
                await obj.SSRedisPipelineTimeAsync();
                // await obj.SSRedisTimeAsync();
            }
            finally
            {
                await obj.Teardown();
            }
        }
#else
        static void Main(string[] args)
            => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
#endif
    }
}
