//
// https://github.com/ServiceStack/ServiceStack.Redis
// ServiceStack.Redis: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Demis Bellot (demis.bellot@gmail.com)
//
// Copyright 2013 Service Stack LLC. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Redis.Pipeline;

namespace ServiceStack.Redis.Generic
{
    /// <summary>
    /// Adds support for Redis Transactions (i.e. MULTI/EXEC/DISCARD operations).
    /// </summary>
    internal partial class RedisTypedTransaction<T>
        : IRedisTypedTransactionAsync<T>, IRedisTransactionBaseAsync
    {
        async ValueTask<bool> IRedisTypedTransactionAsync<T>.CommitAsync(CancellationToken cancellationToken)
        {
            bool rc = true;
            try
            {
                EnqueueCommandsWithExecAndFlush();

                /////////////////////////////
                //receive expected results
                foreach (var queuedCommand in QueuedCommands)
                {
                    await queuedCommand.ProcessResultAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (RedisTransactionFailedException)
            {
                rc = false;
            }
            finally
            {
                RedisClient.Transaction = null;
                ClosePipeline();
                await RedisClient.AddTypeIdsRegisteredDuringPipelineAsync(cancellationToken).ConfigureAwait(false);
            }
            return rc;
        }

        ValueTask IRedisTypedTransactionAsync<T>.RollbackAsync(CancellationToken cancellationToken)
        {
            Rollback(); // no async bits needed
            return default;
        }
    }
}