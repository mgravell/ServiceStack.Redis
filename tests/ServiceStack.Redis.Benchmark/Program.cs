using BenchmarkDotNet.Running;
using System.Threading.Tasks;
using System;
namespace ServiceStack.Redis.Benchmark
{
    class Program
    {
#if DEBUG
        static async Task Main()
        {
            var obj = new ServerTime();
            try
            {
                await obj.Setup(true);
                Console.WriteLine(obj.SSRedisTimeSync());
                Console.WriteLine(obj.SSRedisPipelineTimeSync());
                //await obj.SSRedisTimeAsync();
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
