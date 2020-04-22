using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Pipelines.Sockets.Unofficial;
using Respite;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Benchmark
{
    [SimpleJob(RuntimeMoniker.Net472)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser, MinColumn, MaxColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class ServerTime
    {
        ConnectionMultiplexer _seredis;
        IServer _seredis_server;
        RedisClient _ssredis;
        IRedisClientAsync _ssAsync;
        RespConnection _respite;

        [GlobalSetup]
        public Task Setup() => Setup(false);
        internal async Task Setup(bool minimal)
        {
            _ssredis = new RedisClient("127.0.0.1", 6379);
            _ssAsync = _ssredis;

            if (!minimal)
            {
                _seredis = await ConnectionMultiplexer.ConnectAsync("127.0.0.1:6379");
                _seredis_server = _seredis.GetServer(_seredis.GetEndPoints().Single());

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                SocketConnection.SetRecommendedClientOptions(socket);
                socket.Connect("127.0.0.1", 6379);

                _respite = RespConnection.Create(socket);
            }
        }

        [GlobalCleanup]
        public async Task Teardown()
        {
            _seredis?.Dispose();
            _ssredis?.Dispose();
            if (_respite != null) await _respite.DisposeAsync();

            _seredis_server = null;
            _seredis = null;
            _ssredis = null;
            _respite = null;
            _ssAsync = null;
        }

        const int PER_TEST = 1000;

        [BenchmarkCategory("TimeAsync")]
        [Benchmark(Description = "SERedis", OperationsPerInvoke = PER_TEST)]
        public async Task SERedisTimeAsync()
        {
            for (int i = 0; i < PER_TEST; i++)
            {
                await _seredis_server.TimeAsync().ConfigureAwait(false);
            }
        }

        [BenchmarkCategory("TimeAsync")]
        [Benchmark(Description = "SSRedis", OperationsPerInvoke = PER_TEST, Baseline = true)]
        public async Task SSRedisTimeAsync()
        {
            for (int i = 0; i < PER_TEST; i++)
            {
                await _ssAsync.GetServerTimeAsync().ConfigureAwait(false);
            }
        }


        [BenchmarkCategory("TimeSync")]
        [Benchmark(Description = "SERedis", OperationsPerInvoke = PER_TEST)]
        public void SERedisTimeSync()
        {
            for (int i = 0; i < PER_TEST; i++)
            {
                _seredis_server.Time();
            }
        }

        [BenchmarkCategory("TimeSync")]
        [Benchmark(Description = "SSRedis", OperationsPerInvoke = PER_TEST, Baseline = true)]
        public void SSRedisTimeSync()
        {
            for (int i = 0; i < PER_TEST; i++)
            {
                _ssredis.GetServerTime();
            }
        }

        static readonly RespValue s_Time = RespValue.CreateAggregate(
            RespType.Array, RespValue.Create(RespType.BlobString, "time"));

        static DateTime ParseTime(in RespValue value)
        {
            var parts = value.SubItems;
            if (parts.TryGetSingleSpan(out var span))
                return Parse(span[0], span[1]);
            return Slow(parts);
            static DateTime Slow(in ReadOnlyBlock<RespValue> parts)
            {
                var iter = parts.GetEnumerator();
                if (!iter.MoveNext()) Throw();
                var seconds = iter.Current;
                if (!iter.MoveNext()) Throw();
                var microseconds = iter.Current;
                return Parse(seconds, microseconds);
                static void Throw() => throw new InvalidOperationException();
            }

            static DateTime Parse(in RespValue seconds, in RespValue microseconds)
                => Epoch.AddSeconds(seconds.ToInt64()).AddMilliseconds(microseconds.ToInt64() / 1000.0);
        }
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [BenchmarkCategory("TimeSync")]
        [Benchmark(Description = "Respite", OperationsPerInvoke = PER_TEST)]
        public void RespiteTimeSync()
        {
            for (int i = 0; i < PER_TEST; i++)
            {
                _respite.Call(s_Time, val => ParseTime(val));
            }
        }

        [BenchmarkCategory("TimeAsync")]
        [Benchmark(Description = "Respite", OperationsPerInvoke = PER_TEST)]
        public async Task RespiteTimeAsync()
        {
            for (int i = 0; i < PER_TEST; i++)
            {
                await _respite.CallAsync(s_Time, val => ParseTime(val)).ConfigureAwait(false);
            }
        }
    }
}
