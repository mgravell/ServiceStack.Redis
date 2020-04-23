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
            var obj = new IncrBenchmarks();
            try
            {
                await obj.Setup(false);

                Console.WriteLine(obj.SERedisIncrSync());
                Console.WriteLine(await obj.SERedisIncrAsync());
                Console.WriteLine(await obj.SERedisPipelineIncrAsync());

                Console.WriteLine(obj.SSRedisIncrSync());
                Console.WriteLine(obj.SSRedisPipelineIncrSync());
                Console.WriteLine(await obj.SSRedisIncrAsync());
                Console.WriteLine(await obj.SSRedisPipelineIncrAsync());
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
