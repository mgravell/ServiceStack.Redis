using BenchmarkDotNet.Running;

namespace ServiceStack.Redis.Benchmark
{
    class Program
    {
        static void Main(string[] args)
            => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
