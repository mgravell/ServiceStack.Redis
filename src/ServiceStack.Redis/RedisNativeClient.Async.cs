using ServiceStack.Redis.Pipeline;
using ServiceStack.Text;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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

        ValueTask<byte[][]> IRedisNativeClientAsync.TimeAsync(CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.Time);

        ValueTask<long> IRedisNativeClientAsync.IncrAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Incr, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.ExistsAsync(string key, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            return SendExpectLongAsync(cancellationToken, Commands.Exists, key.ToUtf8Bytes());
        }

        ValueTask<bool> IRedisNativeClientAsync.SetAsync(string key, byte[] value, bool exists, long expirySeconds, long expiryMilliseconds, CancellationToken cancellationToken)
        {
            AssertNotNull(key);
            value = value ?? TypeConstants.EmptyByteArray;

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
            value = value ?? TypeConstants.EmptyByteArray;

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

        async ValueTask<string> IRedisNativeClientAsync.RandomKeyAsync(CancellationToken cancellationToken)
        {
            var bytes = await SendExpectDataAsync(cancellationToken, Commands.RandomKey).ConfigureAwait(false);
            return bytes.FromUtf8Bytes();
        }

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
                return new ValueTask<bool>(pending.Result == Success);
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
            return pending.IsCompletedSuccessfully ? new ValueTask<bool>(pending.Result == expected)
                : Awaited(pending, expected);

            static async ValueTask<bool> Awaited(ValueTask<string> pending, string expected)
                => await pending.ConfigureAwait(false) == expected;
        }

        ValueTask<string> IRedisNativeClientAsync.EchoAsync(string text, CancellationToken cancellationToken)
            => FromUtf8Bytes(SendExpectDataAsync(cancellationToken, Commands.Echo, text.ToUtf8Bytes()));

        private protected static ValueTask<string> FromUtf8Bytes(ValueTask<byte[]> pending)
        {
            return pending.IsCompletedSuccessfully ? new ValueTask<string>(pending.Result.FromUtf8Bytes())
                : Awaited(pending);

            static async ValueTask<string> Awaited(ValueTask<byte[]> pending)
                => (await pending.ConfigureAwait(false)).FromUtf8Bytes();
        }

        ValueTask<long> IRedisNativeClientAsync.DbSizeAsync(CancellationToken cancellationToken)
            => SendExpectLongAsync(cancellationToken, Commands.DbSize);
        
        async ValueTask<DateTime> IRedisNativeClientAsync.LastSaveAsync(CancellationToken cancellationToken)
        {
            var t = await SendExpectLongAsync(cancellationToken, Commands.LastSave).ConfigureAwait(false);
            return t.FromUnixTime();
        }

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

            return DiscardResult(SendExpectCodeAsync(cancellationToken, cmdWithArgs));
        }

        ValueTask IRedisNativeClientAsync.UnWatchAsync(CancellationToken cancellationToken)
            => DiscardResult(SendExpectCodeAsync(cancellationToken, Commands.UnWatch));

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
    }
}