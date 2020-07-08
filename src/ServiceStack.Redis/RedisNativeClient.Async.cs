using ServiceStack.Redis.Pipeline;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Redis.Internal;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
        : IRedisNativeClientAsync
    {
        internal IRedisPipelineSharedAsync PipelineAsync
            => (IRedisPipelineSharedAsync)pipeline;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AssertNotNull(object obj, string name = "key")
        {
            if (obj is null) Throw(name);
            static void Throw(string name) => throw new ArgumentNullException(name);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            Dispose();
            return default;
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.TimeAsync(CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.Time);

        ValueTask<long> IRedisNativeClientAsync.ExistsAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Exists, key.ToUtf8Bytes());
        }

        ValueTask<bool> IRedisNativeClientAsync.SetAsync(string key, byte[] value, bool exists, long expirySeconds, long expiryMilliseconds, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            value ??= TypeConstants.EmptyByteArray;

            if (value.Length > OneGb)
                throw new ArgumentException("value exceeds 1G", "value");

            var entryExists = exists ? Commands.Xx : Commands.Nx;
            byte[][] args;
            if (expiryMilliseconds != 0)
            {
                args = new[] { Commands.Set, key.ToUtf8Bytes(), value, Commands.Px, expiryMilliseconds.ToUtf8Bytes(), entryExists };
            }
            else if (expirySeconds != 0)
            {
                args = new[] { Commands.Set, key.ToUtf8Bytes(), value, Commands.Ex, expirySeconds.ToUtf8Bytes(), entryExists };
            }
            else
            {
                args = new[] { Commands.Set, key.ToUtf8Bytes(), value, entryExists };
            }

            return IsString(SendExpectStringAsync(cancellationToken, args), OK);
        }
        ValueTask IRedisNativeClientAsync.SetAsync(string key, byte[] value, long expirySeconds, long expiryMilliseconds, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            value ??= TypeConstants.EmptyByteArray;

            if (value.Length > OneGb)
                throw new ArgumentException("value exceeds 1G", "value");

            byte[][] args;
            if (expiryMilliseconds != 0)
            {
                args = new[] { Commands.Set, key.ToUtf8Bytes(), value, Commands.Px, expiryMilliseconds.ToUtf8Bytes() };
            }
            else if (expirySeconds != 0)
            {
                args = new[] { Commands.Set, key.ToUtf8Bytes(), value, Commands.Ex, expirySeconds.ToUtf8Bytes() };
            }
            else
            {
                args = new[] { Commands.Set, key.ToUtf8Bytes(), value };
            }
            
            return SendExpectSuccessAsync(cancellationToken, args);
        }

        ValueTask<byte[]> IRedisNativeClientAsync.GetAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectDataAsync(cancellationToken, Commands.Get, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.DelAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Del, key.ToUtf8Bytes());
        }

        ValueTask<ScanResult> IRedisNativeClientAsync.ScanAsync(ulong cursor, int count, string match, CancellationToken cancellationToken)
        {
            if (match == null)
                return SendExpectScanResultAsync(cancellationToken, Commands.Scan, cursor.ToUtf8Bytes(),
                                            Commands.Count, count.ToUtf8Bytes());

            return SendExpectScanResultAsync(cancellationToken, Commands.Scan, cursor.ToUtf8Bytes(),
                                        Commands.Match, match.ToUtf8Bytes(),
                                        Commands.Count, count.ToUtf8Bytes());
        }

        ValueTask<string> IRedisNativeClientAsync.TypeAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectCodeAsync(cancellationToken, Commands.Type, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.RPushAsync(string listId, byte[] value, CancellationToken cancellationToken)
        {
            AssertListIdAndValue(listId, value);

            return SendExpectLongAsync(cancellationToken, Commands.RPush, listId.ToUtf8Bytes(), value);
        }

        ValueTask<long> IRedisNativeClientAsync.SAddAsync(string setId, byte[] value, CancellationToken cancellationToken)
        {
            AssertSetIdAndValue(setId, value);

            return SendExpectLongAsync(cancellationToken, Commands.SAdd, setId.ToUtf8Bytes(), value);
        }

        ValueTask<long> IRedisNativeClientAsync.ZAddAsync(string setId, double score, byte[] value, CancellationToken cancellationToken)
        {
            AssertSetIdAndValue(setId, value);

            return SendExpectLongAsync(cancellationToken, Commands.ZAdd, setId.ToUtf8Bytes(), score.ToFastUtf8Bytes(), value);
        }

        ValueTask<long> IRedisNativeClientAsync.ZAddAsync(string setId, long score, byte[] value, CancellationToken cancellationToken)
        {
            AssertSetIdAndValue(setId, value);

            return SendExpectLongAsync(cancellationToken, Commands.ZAdd, setId.ToUtf8Bytes(), score.ToUtf8Bytes(), value);
        }

        ValueTask<long> IRedisNativeClientAsync.HSetAsync(string hashId, byte[] key, byte[] value, CancellationToken cancellationToken)
            => HSetAsync(hashId.ToUtf8Bytes(), key, value, cancellationToken);

        internal ValueTask<long> HSetAsync(byte[] hashId, byte[] key, byte[] value, CancellationToken cancellationToken = default)
        {
            AssertHashIdAndKey(hashId, key);

            return SendExpectLongAsync(cancellationToken, Commands.HSet, hashId, key, value);
        }

        ValueTask<string> IRedisNativeClientAsync.RandomKeyAsync(CancellationToken cancellationToken)
            => SendExpectDataAsync(cancellationToken, Commands.RandomKey).AwaitFromUtf8Bytes();

        ValueTask IRedisNativeClientAsync.RenameAsync(string oldKeyname, string newKeyname, CancellationToken cancellationToken)
        {
            CheckRenameKeys(oldKeyname, newKeyname);
            return SendExpectSuccessAsync(cancellationToken, Commands.Rename, oldKeyname.ToUtf8Bytes(), newKeyname.ToUtf8Bytes());
        }

        ValueTask<bool> IRedisNativeClientAsync.RenameNxAsync(string oldKeyname, string newKeyname, CancellationToken cancellationToken)
        {
            CheckRenameKeys(oldKeyname, newKeyname);
            return IsSuccess(SendExpectLongAsync(cancellationToken, Commands.RenameNx, oldKeyname.ToUtf8Bytes(), newKeyname.ToUtf8Bytes()));
        }

        private protected static ValueTask<bool> IsSuccess(ValueTask<long> pending)
        {
            if (pending.IsCompletedSuccessfully)
            {
                return (pending.Result == Success).AsValueTask();
            }
            else
            {
                return Awaited(pending);
            }
            async static ValueTask<bool> Awaited(ValueTask<long> pending)
            {
                return (await pending.ConfigureAwait(false)) == Success;
            }
        }

        ValueTask IRedisNativeClientAsync.MSetAsync(byte[][] keys, byte[][] values, CancellationToken cancellationToken)
        {
            var keysAndValues = MergeCommandWithKeysAndValues(Commands.MSet, keys, values);

            return SendExpectSuccessAsync(cancellationToken, keysAndValues);
        }


        ValueTask IRedisNativeClientAsync.MSetAsync(string[] keys, byte[][] values, CancellationToken cancellationToken)
            => ((IRedisNativeClientAsync)this).MSetAsync(keys.ToMultiByteArray(), values, cancellationToken);

        ValueTask IRedisNativeClientAsync.SelectAsync(long db, CancellationToken cancellationToken)
        {
            this.db = db;
            return SendExpectSuccessAsync(cancellationToken, Commands.Select, db.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.DelAsync(string[] keys, CancellationToken cancellationToken)
        {
            AssertNotNull(keys, nameof(keys));

            var cmdWithArgs = MergeCommandWithArgs(Commands.Del, keys);
            return SendExpectLongAsync(cancellationToken, cmdWithArgs);
        }

        ValueTask<bool> IRedisNativeClientAsync.ExpireAsync(string key, int seconds, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return IsSuccess(SendExpectLongAsync(cancellationToken, Commands.Expire, key.ToUtf8Bytes(), seconds.ToUtf8Bytes()));
        }

        ValueTask<bool> IRedisNativeClientAsync.PExpireAsync(string key, long ttlMs, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return IsSuccess(SendExpectLongAsync(cancellationToken, Commands.PExpire, key.ToUtf8Bytes(), ttlMs.ToUtf8Bytes()));
        }

        ValueTask<bool> IRedisNativeClientAsync.ExpireAtAsync(string key, long unixTime, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return IsSuccess(SendExpectLongAsync(cancellationToken, Commands.ExpireAt, key.ToUtf8Bytes(), unixTime.ToUtf8Bytes()));
        }

        ValueTask<bool> IRedisNativeClientAsync.PExpireAtAsync(string key, long unixTimeMs, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return IsSuccess(SendExpectLongAsync(cancellationToken, Commands.PExpireAt, key.ToUtf8Bytes(), unixTimeMs.ToUtf8Bytes()));
        }

        ValueTask<long> IRedisNativeClientAsync.TtlAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Ttl, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.PTtlAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.PTtl, key.ToUtf8Bytes());
        }

        ValueTask<bool> IRedisNativeClientAsync.PingAsync(CancellationToken cancellationToken)
            => IsString(SendExpectCodeAsync(cancellationToken, Commands.Ping), "PONG");

        private static ValueTask<bool> IsString(ValueTask<string> pending, string expected)
        {
            return pending.IsCompletedSuccessfully ? (pending.Result == expected).AsValueTask()
                : Awaited(pending, expected);

            static async ValueTask<bool> Awaited(ValueTask<string> pending, string expected)
                => await pending.ConfigureAwait(false) == expected;
        }

        ValueTask<string> IRedisNativeClientAsync.EchoAsync(string text, CancellationToken cancellationToken)
            => SendExpectDataAsync(cancellationToken, Commands.Echo, text.ToUtf8Bytes()).AwaitFromUtf8Bytes();

        ValueTask<long> IRedisNativeClientAsync.DbSizeAsync(CancellationToken cancellationToken)
            => SendExpectLongAsync(cancellationToken, Commands.DbSize);

        ValueTask<DateTime> IRedisNativeClientAsync.LastSaveAsync(CancellationToken cancellationToken)
            => SendExpectLongAsync(cancellationToken, Commands.LastSave).Await(t => t.FromUnixTime());

        ValueTask IRedisNativeClientAsync.SaveAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.Save);

        ValueTask IRedisNativeClientAsync.BgSaveAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.BgSave);

        ValueTask IRedisNativeClientAsync.ShutdownAsync(bool noSave, CancellationToken cancellationToken)
            => noSave
            ? SendWithoutReadAsync(cancellationToken, Commands.Shutdown, Commands.NoSave)
            : SendWithoutReadAsync(cancellationToken, Commands.Shutdown);

        ValueTask IRedisNativeClientAsync.BgRewriteAofAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.BgRewriteAof);

        ValueTask IRedisNativeClientAsync.QuitAsync(CancellationToken cancellationToken)
            => SendWithoutReadAsync(cancellationToken, Commands.Quit);

        ValueTask IRedisNativeClientAsync.FlushDbAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.FlushDb);

        ValueTask IRedisNativeClientAsync.FlushAllAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.FlushAll);

        ValueTask IRedisNativeClientAsync.SlaveOfAsync(string hostname, int port, CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.SlaveOf, hostname.ToUtf8Bytes(), port.ToUtf8Bytes());

        ValueTask IRedisNativeClientAsync.SlaveOfNoOneAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.SlaveOf, Commands.No, Commands.One);

        ValueTask<byte[][]> IRedisNativeClientAsync.KeysAsync(string pattern, CancellationToken cancellationToken)
        {
            AssertNotNull(pattern, nameof(pattern));
            return SendExpectMultiDataAsync(cancellationToken, Commands.Keys, pattern.ToUtf8Bytes());
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.MGetAsync(string[] keys, CancellationToken cancellationToken)
        {
            AssertNotNull(keys, nameof(keys));
            if (keys.Length == 0)
                throw new ArgumentException("keys");

            var cmdWithArgs = MergeCommandWithArgs(Commands.MGet, keys);

            return SendExpectMultiDataAsync(cancellationToken, cmdWithArgs);
        }

        ValueTask IRedisNativeClientAsync.SetExAsync(string key, int expireInSeconds, byte[] value, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            value ??= TypeConstants.EmptyByteArray;

            if (value.Length > OneGb)
                throw new ArgumentException("value exceeds 1G", "value");

            return SendExpectSuccessAsync(cancellationToken, Commands.SetEx, key.ToUtf8Bytes(), expireInSeconds.ToUtf8Bytes(), value);
        }

        ValueTask IRedisNativeClientAsync.WatchAsync(string[] keys, CancellationToken cancellationToken)
        {
            AssertNotNull(keys, nameof(keys));
            if (keys.Length == 0)
                throw new ArgumentException("keys");

            var cmdWithArgs = MergeCommandWithArgs(Commands.Watch, keys);

            return SendExpectCodeAsync(cancellationToken, cmdWithArgs).Await();
        }

        ValueTask IRedisNativeClientAsync.UnWatchAsync(CancellationToken cancellationToken)
            => SendExpectCodeAsync(cancellationToken, Commands.UnWatch).Await();

        ValueTask<long> IRedisNativeClientAsync.AppendAsync(string key, byte[] value, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Append, key.ToUtf8Bytes(), value);
        }

        ValueTask<byte[]> IRedisNativeClientAsync.GetRangeAsync(string key, int fromIndex, int toIndex, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectDataAsync(cancellationToken, Commands.GetRange, key.ToUtf8Bytes(), fromIndex.ToUtf8Bytes(), toIndex.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.SetRangeAsync(string key, int offset, byte[] value, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.SetRange, key.ToUtf8Bytes(), offset.ToUtf8Bytes(), value);
        }

        ValueTask<long> IRedisNativeClientAsync.GetBitAsync(string key, int offset, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.GetBit, key.ToUtf8Bytes(), offset.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.SetBitAsync(string key, int offset, int value, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            if (value > 1 || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "value is out of range");
            return SendExpectLongAsync(cancellationToken, Commands.SetBit, key.ToUtf8Bytes(), offset.ToUtf8Bytes(), value.ToUtf8Bytes());
        }

        ValueTask<bool> IRedisNativeClientAsync.PersistAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return IsSuccess(SendExpectLongAsync(cancellationToken, Commands.Persist, key.ToUtf8Bytes()));
        }

        ValueTask IRedisNativeClientAsync.PSetExAsync(string key, long expireInMs, byte[] value, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectSuccessAsync(cancellationToken, Commands.PSetEx, key.ToUtf8Bytes(), expireInMs.ToUtf8Bytes(), value);
        }

        ValueTask<long> IRedisNativeClientAsync.SetNXAsync(string key, byte[] value, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            value ??= TypeConstants.EmptyByteArray;

            if (value.Length > OneGb)
                throw new ArgumentException("value exceeds 1G", "value");

            return SendExpectLongAsync(cancellationToken, Commands.SetNx, key.ToUtf8Bytes(), value);
        }

        ValueTask<byte[]> IRedisNativeClientAsync.SPopAsync(string setId, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));
            return SendExpectDataAsync(cancellationToken, Commands.SPop, setId.ToUtf8Bytes());
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.SPopAsync(string setId, int count, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));
            return SendExpectMultiDataAsync(cancellationToken, Commands.SPop, setId.ToUtf8Bytes(), count.ToUtf8Bytes());
        }

        ValueTask IRedisNativeClientAsync.SlowlogResetAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.Slowlog, "RESET".ToUtf8Bytes());

        ValueTask<object[]> IRedisNativeClientAsync.SlowlogGetAsync(int? top, CancellationToken cancellationToken)
        {
            if (top.HasValue)
                return SendExpectDeeplyNestedMultiDataAsync(cancellationToken, Commands.Slowlog, Commands.Get, top.Value.ToUtf8Bytes());
            else
                return SendExpectDeeplyNestedMultiDataAsync(cancellationToken, Commands.Slowlog, Commands.Get);
        }

        ValueTask<long> IRedisNativeClientAsync.ZCardAsync(string setId, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));
            return SendExpectLongAsync(cancellationToken, Commands.ZCard, setId.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.ZCountAsync(string setId, double min, double max, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));
            return SendExpectLongAsync(cancellationToken, Commands.ZCount, setId.ToUtf8Bytes(), min.ToUtf8Bytes(), max.ToUtf8Bytes());
        }

        ValueTask<double> IRedisNativeClientAsync.ZScoreAsync(string setId, byte[] value, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));
            return SendExpectDoubleAsync(cancellationToken, Commands.ZScore, setId.ToUtf8Bytes(), value);
        }

        protected ValueTask<RedisData> RawCommandAsync(CancellationToken cancellationToken, params object[] cmdWithArgs)
        {
            var byteArgs = new List<byte[]>();

            foreach (var arg in cmdWithArgs)
            {
                if (arg == null)
                {
                    byteArgs.Add(TypeConstants.EmptyByteArray);
                    continue;
                }

                if (arg is byte[] bytes)
                {
                    byteArgs.Add(bytes);
                }
                else if (arg.GetType().IsUserType())
                {
                    var json = arg.ToJson();
                    byteArgs.Add(json.ToUtf8Bytes());
                }
                else
                {
                    var str = arg.ToString();
                    byteArgs.Add(str.ToUtf8Bytes());
                }
            }

            return SendExpectComplexResponseAsync(cancellationToken, byteArgs.ToArray());
        }

        ValueTask<Dictionary<string, string>> IRedisNativeClientAsync.InfoAsync(CancellationToken cancellationToken)
            => SendExpectStringAsync(cancellationToken, Commands.Info).Await(info => ParseInfoResult(info));

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRangeByLexAsync(string setId, string min, string max, int? skip, int? take, CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, GetZRangeByLexArgs(setId, min, max, skip, take));

        ValueTask<long> IRedisNativeClientAsync.ZLexCountAsync(string setId, string min, string max, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));

            return SendExpectLongAsync(cancellationToken,
                Commands.ZLexCount, setId.ToUtf8Bytes(), min.ToUtf8Bytes(), max.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.ZRemRangeByLexAsync(string setId, string min, string max, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));

            return SendExpectLongAsync(cancellationToken,
                Commands.ZRemRangeByLex, setId.ToUtf8Bytes(), min.ToUtf8Bytes(), max.ToUtf8Bytes());
        }

        ValueTask<string> IRedisNativeClientAsync.CalculateSha1Async(string luaBody, CancellationToken cancellationToken)
        {
            AssertNotNull(luaBody, nameof(luaBody));

            byte[] buffer = Encoding.UTF8.GetBytes(luaBody);
            return BitConverter.ToString(buffer.ToSha1Hash()).Replace("-", "").AsValueTask();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ScriptExistsAsync(byte[][] sha1Refs, CancellationToken cancellationToken)
        {
            var keysAndValues = MergeCommandWithArgs(Commands.Script, Commands.Exists, sha1Refs);
            return SendExpectMultiDataAsync(cancellationToken, keysAndValues);
        }

        ValueTask IRedisNativeClientAsync.ScriptFlushAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.Script, Commands.Flush);

        ValueTask IRedisNativeClientAsync.ScriptKillAsync(CancellationToken cancellationToken)
            => SendExpectSuccessAsync(cancellationToken, Commands.Script, Commands.Kill);

        ValueTask<byte[]> IRedisNativeClientAsync.ScriptLoadAsync(string body, CancellationToken cancellationToken)
        {
            AssertNotNull(body, nameof(body));

            var cmdArgs = MergeCommandWithArgs(Commands.Script, Commands.Load, body.ToUtf8Bytes());
            return SendExpectDataAsync(cancellationToken, cmdArgs);
        }

        ValueTask<long> IRedisNativeClientAsync.StrLenAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.StrLen, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.LLenAsync(string listId, CancellationToken cancellationToken)
        {
            AssertNotNull(listId, nameof(listId));
            return SendExpectLongAsync(cancellationToken, Commands.LLen, listId.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.SCardAsync(string setId, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));
            return SendExpectLongAsync(cancellationToken, Commands.SCard, setId.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.HLenAsync(string hashId, CancellationToken cancellationToken)
        {
            AssertNotNull(hashId, nameof(hashId));
            return SendExpectLongAsync(cancellationToken, Commands.HLen, hashId.ToUtf8Bytes());
        }

        ValueTask<RedisData> IRedisNativeClientAsync.EvalCommandAsync(string luaBody, int numberKeysInArgs, byte[][] keys, CancellationToken cancellationToken)
        {
            AssertNotNull(luaBody, nameof(luaBody));

            var cmdArgs = MergeCommandWithArgs(Commands.Eval, luaBody.ToUtf8Bytes(), keys.PrependInt(numberKeysInArgs));
            return RawCommandAsync(cancellationToken, cmdArgs);
        }

        ValueTask<RedisData> IRedisNativeClientAsync.EvalShaCommandAsync(string sha1, int numberKeysInArgs, byte[][] keys, CancellationToken cancellationToken)
        {
            AssertNotNull(sha1, nameof(sha1));

            var cmdArgs = MergeCommandWithArgs(Commands.EvalSha, sha1.ToUtf8Bytes(), keys.PrependInt(numberKeysInArgs));
            return RawCommandAsync(cancellationToken, cmdArgs);
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.EvalAsync(string luaBody, int numberOfKeys, byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            AssertNotNull(luaBody, nameof(luaBody));

            var cmdArgs = MergeCommandWithArgs(Commands.Eval, luaBody.ToUtf8Bytes(), keysAndArgs.PrependInt(numberOfKeys));
            return SendExpectMultiDataAsync(cancellationToken, cmdArgs);
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.EvalShaAsync(string sha1, int numberOfKeys, byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            AssertNotNull(sha1, nameof(sha1));

            var cmdArgs = MergeCommandWithArgs(Commands.EvalSha, sha1.ToUtf8Bytes(), keysAndArgs.PrependInt(numberOfKeys));
            return SendExpectMultiDataAsync(cancellationToken, cmdArgs);
        }

        ValueTask<long> IRedisNativeClientAsync.EvalIntAsync(string luaBody, int numberOfKeys, byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            AssertNotNull(luaBody, nameof(luaBody));

            var cmdArgs = MergeCommandWithArgs(Commands.Eval, luaBody.ToUtf8Bytes(), keysAndArgs.PrependInt(numberOfKeys));
            return SendExpectLongAsync(cancellationToken, cmdArgs);
        }

        ValueTask<long> IRedisNativeClientAsync.EvalShaIntAsync(string sha1, int numberOfKeys, byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            AssertNotNull(sha1, nameof(sha1));

            var cmdArgs = MergeCommandWithArgs(Commands.EvalSha, sha1.ToUtf8Bytes(), keysAndArgs.PrependInt(numberOfKeys));
            return SendExpectLongAsync(cancellationToken, cmdArgs);
        }

        ValueTask<string> IRedisNativeClientAsync.EvalStrAsync(string luaBody, int numberOfKeys, byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            AssertNotNull(luaBody, nameof(luaBody));

            var cmdArgs = MergeCommandWithArgs(Commands.Eval, luaBody.ToUtf8Bytes(), keysAndArgs.PrependInt(numberOfKeys));
            return SendExpectDataAsync(cancellationToken, cmdArgs).AwaitFromUtf8Bytes();
        }

        ValueTask<string> IRedisNativeClientAsync.EvalShaStrAsync(string sha1, int numberOfKeys, byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            AssertNotNull(sha1, nameof(sha1));

            var cmdArgs = MergeCommandWithArgs(Commands.EvalSha, sha1.ToUtf8Bytes(), keysAndArgs.PrependInt(numberOfKeys));
            return SendExpectDataAsync(cancellationToken, cmdArgs).AwaitFromUtf8Bytes();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.SMembersAsync(string setId, CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.SMembers, setId.ToUtf8Bytes());

        ValueTask<long> IRedisNativeClientAsync.SAddAsync(string setId, byte[][] values, CancellationToken cancellationToken)
        {
            AssertNotNull(setId, nameof(setId));
            AssertNotNull(values, nameof(values));
            if (values.Length == 0)
                throw new ArgumentException(nameof(values));

            var cmdWithArgs = MergeCommandWithArgs(Commands.SAdd, setId.ToUtf8Bytes(), values);
            return SendExpectLongAsync(cancellationToken, cmdWithArgs);
        }

        ValueTask<long> IRedisNativeClientAsync.SRemAsync(string setId, byte[] value, CancellationToken cancellationToken)
        {
            AssertSetIdAndValue(setId, value);
            return SendExpectLongAsync(cancellationToken, Commands.SRem, setId.ToUtf8Bytes(), value);
        }

        ValueTask<long> IRedisNativeClientAsync.IncrByAsync(string key, long count, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.IncrBy, key.ToUtf8Bytes(), count.ToUtf8Bytes());
        }

        ValueTask<double> IRedisNativeClientAsync.IncrByFloatAsync(string key, double incrBy, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectDoubleAsync(cancellationToken, Commands.IncrByFloat, key.ToUtf8Bytes(), incrBy.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.IncrAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Incr, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.DecrAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Decr, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.DecrByAsync(string key, long count, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.DecrBy, key.ToUtf8Bytes(), count.ToUtf8Bytes());
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ConfigGetAsync(string pattern, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.ConfigSetAsync(string item, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.ConfigResetStatAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.ConfigRewriteAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.DebugSegfaultAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.DumpAsync(string key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.RestoreAsync(string key, long expireMs, byte[] dumpValue, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.MigrateAsync(string host, int port, string key, int destinationDb, long timeoutMs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisNativeClientAsync.MoveAsync(string key, int db, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ObjectIdleTimeAsync(string key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<RedisText> IRedisNativeClientAsync.RoleAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<RedisData> IRedisNativeClientAsync.RawCommandAsync(object[] cmdWithArgs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<RedisData> IRedisNativeClientAsync.RawCommandAsync(byte[][] cmdWithBinaryArgs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string> IRedisNativeClientAsync.ClientGetNameAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.ClientSetNameAsync(string client, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.ClientKillAsync(string host, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ClientKillAsync(string addr, string id, string type, string skipMe, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.ClientListAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.ClientPauseAsync(int timeOutMs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisNativeClientAsync.MSetNxAsync(byte[][] keys, byte[][] values, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisNativeClientAsync.MSetNxAsync(string[] keys, byte[][] values, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.GetSetAsync(string key, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.MGetAsync(byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<ScanResult> IRedisNativeClientAsync.SScanAsync(string setId, ulong cursor, int count, string match, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<ScanResult> IRedisNativeClientAsync.ZScanAsync(string setId, ulong cursor, int count, string match, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<ScanResult> IRedisNativeClientAsync.HScanAsync(string hashId, ulong cursor, int count, string match, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IRedisNativeClientAsync.PfAddAsync(string key, byte[][] elements, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.PfCountAsync(string key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.PfMergeAsync(string toKeyId, string[] fromKeys, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.SortAsync(string listOrSetId, SortOptions sortOptions, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.LRangeAsync(string listId, int startingFrom, int endingAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.RPushXAsync(string listId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.LPushAsync(string listId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.LPushXAsync(string listId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.LTrimAsync(string listId, int keepStartingFrom, int keepEndingAt, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.LRemAsync(string listId, int removeNoOfMatches, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.LIndexAsync(string listId, int listIndex, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.LInsertAsync(string listId, bool insertBefore, byte[] pivot, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.LSetAsync(string listId, int listIndex, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.LPopAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.RPopAsync(string listId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.BLPopAsync(string listId, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.BLPopAsync(string[] listIds, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.BLPopValueAsync(string listId, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.BLPopValueAsync(string[] listIds, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.BRPopAsync(string listId, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.BRPopAsync(string[] listIds, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.RPopLPushAsync(string fromListId, string toListId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.BRPopValueAsync(string listId, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.BRPopValueAsync(string[] listIds, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.BRPopLPushAsync(string fromListId, string toListId, int timeOutSecs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.SMoveAsync(string fromSetId, string toSetId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.SIsMemberAsync(string setId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.SInterAsync(string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.SInterStoreAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.SUnionAsync(string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.SUnionStoreAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.SDiffAsync(string fromSetId, string[] withSetIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.SDiffStoreAsync(string intoSetId, string fromSetId, string[] withSetIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.SRandMemberAsync(string setId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZRemAsync(string setId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZRemAsync(string setId, byte[][] values, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisNativeClientAsync.ZIncrByAsync(string setId, double incrBy, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisNativeClientAsync.ZIncrByAsync(string setId, long incrBy, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZRankAsync(string setId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZRevRankAsync(string setId, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRangeAsync(string setId, int min, int max, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRangeWithScoresAsync(string setId, int min, int max, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRevRangeAsync(string setId, int min, int max, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRevRangeWithScoresAsync(string setId, int min, int max, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRangeByScoreAsync(string setId, double min, double max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRangeByScoreAsync(string setId, long min, long max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRangeByScoreWithScoresAsync(string setId, double min, double max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRangeByScoreWithScoresAsync(string setId, long min, long max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRevRangeByScoreAsync(string setId, double min, double max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRevRangeByScoreAsync(string setId, long min, long max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRevRangeByScoreWithScoresAsync(string setId, double min, double max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ZRevRangeByScoreWithScoresAsync(string setId, long min, long max, int? skip, int? take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZRemRangeByRankAsync(string setId, int min, int max, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZRemRangeByScoreAsync(string setId, double fromScore, double toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZRemRangeByScoreAsync(string setId, long fromScore, long toScore, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZUnionStoreAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.ZInterStoreAsync(string intoSetId, string[] setIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask IRedisNativeClientAsync.HMSetAsync(string hashId, byte[][] keys, byte[][] values, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.HSetNXAsync(string hashId, byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.HIncrbyAsync(string hashId, byte[] key, int incrementBy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisNativeClientAsync.HIncrbyFloatAsync(string hashId, byte[] key, double incrementBy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[]> IRedisNativeClientAsync.HGetAsync(string hashId, byte[] key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.HMGetAsync(string hashId, byte[][] keysAndArgs, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.HDelAsync(string hashId, byte[] key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.HExistsAsync(string hashId, byte[] key, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.HKeysAsync(string hashId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.HValsAsync(string hashId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.HGetAllAsync(string hashId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.GeoAddAsync(string key, double longitude, double latitude, string member, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.GeoAddAsync(string key, RedisGeo[] geoPoints, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<double> IRedisNativeClientAsync.GeoDistAsync(string key, string fromMember, string toMember, string unit, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<string[]> IRedisNativeClientAsync.GeoHashAsync(string key, string[] members, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<RedisGeo>> IRedisNativeClientAsync.GeoPosAsync(string key, string[] members, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<RedisGeoResult>> IRedisNativeClientAsync.GeoRadiusAsync(string key, double longitude, double latitude, double radius, string unit, bool withCoords, bool withDist, bool withHash, int? count, bool? asc, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<List<RedisGeoResult>> IRedisNativeClientAsync.GeoRadiusByMemberAsync(string key, string member, double radius, string unit, bool withCoords, bool withDist, bool withHash, int? count, bool? asc, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<long> IRedisNativeClientAsync.PublishAsync(string toChannel, byte[] message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.SubscribeAsync(string[] toChannels, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.UnSubscribeAsync(string[] toChannels, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.PSubscribeAsync(string[] toChannelsMatchingPatterns, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.PUnSubscribeAsync(string[] toChannelsMatchingPatterns, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        ValueTask<byte[][]> IRedisNativeClientAsync.ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IRedisSubscriptionAsync IRedisNativeClientAsync.CreateSubscriptionAsync()
            => new RedisSubscription(this);
    }
}