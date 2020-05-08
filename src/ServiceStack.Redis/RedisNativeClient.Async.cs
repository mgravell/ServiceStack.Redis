using ServiceStack.Redis.Pipeline;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisNativeClient
        : IRedisNativeClientAsync
    {
        internal IRedisPipelineSharedAsync PipelineAsync
            => (IRedisPipelineSharedAsync)pipeline;
        ValueTask<byte[][]> IRedisNativeClientAsync.TimeAsync(CancellationToken cancellationToken)
            => SendExpectMultiDataAsync(cancellationToken, Commands.Time);

        ValueTask<long> IRedisNativeClientAsync.IncrAsync(string key, CancellationToken cancellationToken)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectLongAsync(cancellationToken, Commands.Incr, key.ToUtf8Bytes());
        }

        ValueTask IRedisNativeClientAsync.SetAsync(string key, byte[] value, CancellationToken cancellationToken)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            value = value ?? TypeConstants.EmptyByteArray;

            if (value.Length > OneGb)
                throw new ArgumentException("value exceeds 1G", "value");

            return SendExpectSuccessAsync(cancellationToken, Commands.Set, key.ToUtf8Bytes(), value);
        }

        ValueTask<byte[]> IRedisNativeClientAsync.GetAsync(string key, CancellationToken cancellationToken)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectDataAsync(cancellationToken, Commands.Get, key.ToUtf8Bytes());
        }

        ValueTask<long> IRedisNativeClientAsync.DelAsync(string key, CancellationToken cancellationToken)
        {
            var bytes = key.ToUtf8Bytes();
            if (bytes == null)
                throw new ArgumentNullException("key");

            return SendExpectLongAsync(cancellationToken, Commands.Del, bytes);
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
            if (key == null)
                throw new ArgumentNullException("key");

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


        internal ValueTask<long> DelAsync(string key, CancellationToken cancellationToken = default)
        {
            return DelAsync(key.ToUtf8Bytes(), cancellationToken);
        }

        internal ValueTask<long> DelAsync(byte[] key, CancellationToken cancellationToken = default)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectLongAsync(cancellationToken, Commands.Del, key);
        }

        internal ValueTask<long> DelAsync(string[] keys, CancellationToken cancellationToken = default)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");

            var cmdWithArgs = MergeCommandWithArgs(Commands.Del, keys);
            return SendExpectLongAsync(cancellationToken, cmdWithArgs);
        }

        ValueTask<long> IRedisNativeClientAsync.ExistsAsync(string key, CancellationToken cancellationToken)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectLongAsync(cancellationToken, Commands.Exists, key.ToUtf8Bytes());
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
    }
}