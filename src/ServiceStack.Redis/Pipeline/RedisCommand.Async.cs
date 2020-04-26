#if ASYNC_REDIS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    /// <summary>
    /// Redis command that does not get queued
    /// </summary>
    internal partial class RedisCommand
    {
        private Delegate _asyncReturnCommand;
        private RedisCommand SetAsyncReturnCommand(Delegate value)
        {
            if (_asyncReturnCommand is object && _asyncReturnCommand != value)
                throw new InvalidOperationException("Only a single async return command can be assigned");
            _asyncReturnCommand = value;
            return this;
        }
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask> VoidReturnCommandAsync)
            => SetAsyncReturnCommand(VoidReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<int>> IntReturnCommandAsync)
            => SetAsyncReturnCommand(IntReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<long>> LongReturnCommandAsync)
            => SetAsyncReturnCommand(LongReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<bool>> BoolReturnCommandAsync)
            => SetAsyncReturnCommand(BoolReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<byte[]>> BytesReturnCommandAsync)
            => SetAsyncReturnCommand(BytesReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<byte[][]>> MultiBytesReturnCommandAsync)
            => SetAsyncReturnCommand(MultiBytesReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<string>> StringReturnCommandAsync)
            => SetAsyncReturnCommand(StringReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<List<string>>> MultiStringReturnCommandAsync)
            => SetAsyncReturnCommand(MultiStringReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<Dictionary<string, string>>> DictionaryStringReturnCommandAsync)
            => SetAsyncReturnCommand(DictionaryStringReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<RedisData>> RedisDataReturnCommandAsync)
            => SetAsyncReturnCommand(RedisDataReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<RedisText>> RedisTextReturnCommandAsync)
            => SetAsyncReturnCommand(RedisTextReturnCommandAsync);
        internal RedisCommand WithAsyncReturnCommand(Func<IRedisClientAsync, ValueTask<double>> DoubleReturnCommandAsync)
            => SetAsyncReturnCommand(DoubleReturnCommandAsync);

        public override async ValueTask ExecuteAsync(IRedisClientAsync client)
        {
            try
            {
                switch (_asyncReturnCommand)
                {
                    case null:
                        break;
                    case Func<IRedisClientAsync, ValueTask> VoidReturnCommandAsync:
                        await VoidReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<int>> IntReturnCommandAsync:
                        await IntReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<long>> LongReturnCommandAsync:
                        await LongReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<double>> DoubleReturnCommandAsync:
                        await DoubleReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<byte[]>> BytesReturnCommandAsync:
                        await BytesReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<string>> StringReturnCommandAsync:
                        await StringReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<byte[][]>> MultiBytesReturnCommandAsync:
                        await MultiBytesReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<List<string>>> MultiStringReturnCommandAsync:
                        await MultiStringReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<Dictionary<string, string>>> DictionaryStringReturnCommandAsync:
                        await DictionaryStringReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<RedisData>> RedisDataReturnCommandAsync:
                        await RedisDataReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<RedisText>> RedisTextReturnCommandAsync:
                        await RedisTextReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    case Func<IRedisClientAsync, ValueTask<bool>> BoolReturnCommandAsync:
                        await BoolReturnCommandAsync(client).ConfigureAwait(false);
                        break;
                    default:
                        ThrowIfSync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        partial void ThrowIfAsync()
        {
            if (_asyncReturnCommand is object)
            {
                throw new InvalidOperationException("An async return command was present, but the queued operation is being processed synchronously");
            }
        }
        private void ThrowIfSync()
        {
            if (VoidReturnCommand is object
                || IntReturnCommand is object
                || LongReturnCommand is object
                || BoolReturnCommand is object
                || BytesReturnCommand is object
                || MultiBytesReturnCommand is object
                || StringReturnCommand is object
                || MultiStringReturnCommand is object
                || DictionaryStringReturnCommand is object
                || RedisDataReturnCommand is object
                || RedisTextReturnCommand is object
                || DoubleReturnCommand is object)
            {
                throw new InvalidOperationException("A sync return command was present, but the queued operation is being processed asynchronously");
            }
        }
    }
}
#endif