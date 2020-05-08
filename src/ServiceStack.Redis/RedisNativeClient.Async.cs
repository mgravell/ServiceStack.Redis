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
    }
}