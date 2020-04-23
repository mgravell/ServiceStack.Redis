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
        private Delegate _asyncExecutor;
        public void SetAsyncExecutor(Delegate value)
        {
            if (_asyncExecutor is object && _asyncExecutor != value)
                throw new InvalidOperationException("Only a single async executor can be assigned");
            _asyncExecutor = value;
        }
        public Func<IRedisClientAsync, ValueTask> VoidReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<int>> IntReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<int>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<long>> LongReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<long>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<bool>> BoolReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<bool>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<byte[]>> BytesReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<byte[]>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<byte[][]>> MultiBytesReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<byte[][]>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<string>> StringReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<string>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<List<string>>> MultiStringReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<List<string>>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<Dictionary<string, string>>> DictionaryStringReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<Dictionary<string, string>>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<RedisData>> RedisDataReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<RedisData>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<RedisText>> RedisTextReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<RedisText>>;
            set => SetAsyncExecutor(value);
        }
        public Func<IRedisClientAsync, ValueTask<double>> DoubleReturnCommandAsync
        {
            get => _asyncExecutor as Func<IRedisClientAsync, ValueTask<double>>;
            set => SetAsyncExecutor(value);
        }

        public override async ValueTask ExecuteAsync(IRedisClientAsync client)
        {
            try
            {
                if (VoidReturnCommandAsync != null)
                {
                    await VoidReturnCommandAsync(client).ConfigureAwait(false);

                }
                else if (IntReturnCommandAsync != null)
                {
                    await IntReturnCommandAsync(client).ConfigureAwait(false);

                }
                else if (LongReturnCommandAsync != null)
                {
                    await LongReturnCommandAsync(client).ConfigureAwait(false);

                }
                else if (DoubleReturnCommandAsync != null)
                {
                    await DoubleReturnCommandAsync(client).ConfigureAwait(false);

                }
                else if (BytesReturnCommandAsync != null)
                {
                    await BytesReturnCommandAsync(client).ConfigureAwait(false);

                }
                else if (StringReturnCommandAsync != null)
                {
                    await StringReturnCommandAsync(client).ConfigureAwait(false);

                }
                else if (MultiBytesReturnCommandAsync != null)
                {
                    await MultiBytesReturnCommandAsync(client).ConfigureAwait(false);

                }
                else if (MultiStringReturnCommandAsync != null)
                {
                    await MultiStringReturnCommandAsync(client).ConfigureAwait(false);
                }
                else if (DictionaryStringReturnCommandAsync != null)
                {
                    await DictionaryStringReturnCommandAsync(client).ConfigureAwait(false);
                }
                else if (RedisDataReturnCommandAsync != null)
                {
                    await RedisDataReturnCommandAsync(client).ConfigureAwait(false);
                }
                else if (RedisTextReturnCommandAsync != null)
                {
                    await RedisTextReturnCommandAsync(client).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
#endif