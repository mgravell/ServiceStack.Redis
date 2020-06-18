using System;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    public static partial class RedisClientExtensions
    {
        // these APIs are mostly to help with "params" usage vs CancellationToken
        public static ValueTask<RedisText> CustomAsync(this IRedisClientAsync client, params object[] cmdWithArgs)
            => client.CustomAsync(cmdWithArgs, default);


        public static ValueTask<RedisText> ExecLuaShaAsync(this IRedisClientAsync client, string sha1, params string[] args)
            => client.ExecLuaShaAsync(sha1, Array.Empty<string>(), args, default);

        public static ValueTask<RedisText> ExecLuaAsync(this IRedisClientAsync client, string body, params string[] args)
            => client.ExecLuaAsync(body, Array.Empty<string>(), args, default);

        public static ValueTask<string> ExecLuaShaAsStringAsync(this IRedisClientAsync client, string sha1, params string[] args)
            => client.ExecLuaShaAsStringAsync(sha1, Array.Empty<string>(), args, default);

        public static ValueTask<string> ExecLuaAsStringAsync(this IRedisClientAsync client, string luaBody, params string[] args)
            => client.ExecLuaAsStringAsync(luaBody, Array.Empty<string>(), args, default);
    }
}