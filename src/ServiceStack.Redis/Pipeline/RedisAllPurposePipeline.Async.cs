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
                    queuedCommand.ProcessResult();
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
    }
}
#endif