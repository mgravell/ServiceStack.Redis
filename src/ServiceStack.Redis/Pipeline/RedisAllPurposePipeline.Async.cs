#if ASYNC_REDIS
using ServiceStack.Redis.Pipeline;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{

    public partial class RedisAllPurposePipeline : IRedisPipelineAsync
    {
        private IRedisPipelineAsync AsyncPipeline => this;

        async Task<bool> IRedisPipelineSharedAsync.ReplayAsync(CancellationToken cancellationToken)
        {
            Init();
            Execute();
            await AsyncPipeline.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        async Task IRedisPipelineSharedAsync.FlushAsync(CancellationToken cancellationToken)
        {
            // flush send buffers
            await RedisClient.FlushSendBufferAsync(cancellationToken).ConfigureAwait(false);
            RedisClient.ResetSendBuffer();
            
            try
            {
                //receive expected results
                foreach (var queuedCommand in QueuedCommands)
                {
                    await queuedCommand.ProcessResultAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The connection cannot be reused anymore. All queued commands have been sent to redis. Even if a new command is executed, the next response read from the
                // network stream can be the response of one of the queued commands, depending on when the exception occurred. This response would be invalid for the new command.
                RedisClient.DisposeConnection();
                throw;
            }

            ClosePipeline();
        }
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            // don't need to send anything; just clean up
            Dispose();
            return default;
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask> command, Action onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<int>> command, Action<int> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            BeginQueuedCommand(new QueuedRedisCommand
            {
                IntReturnCommandAsync = command,
                OnSuccessIntCallback = onSuccessCallback,
                OnErrorCallback = onErrorCallback
            });
            command(RedisClient);
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<long>> command, Action<long> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<bool>> command, Action<bool> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<double>> command, Action<double> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<byte[]>> command, Action<byte[]> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<byte[][]>> command, Action<byte[][]> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<string>> command, Action<string> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<List<string>>> command, Action<List<string>> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<HashSet<string>>> command, Action<HashSet<string>> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<Dictionary<string, string>>> command, Action<Dictionary<string, string>> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<RedisData>> command, Action<RedisData> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }

        void IRedisQueueableOperationAsync.QueueCommand(Func<IRedisClientAsync, ValueTask<RedisText>> command, Action<RedisText> onSuccessCallback, Action<Exception> onErrorCallback)
        {
            throw new NotImplementedException();
        }
    }
}
#endif