using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Pipeline
{
    /// <summary>
    /// Interface to operations that allow queued commands to be completed
    /// </summary>
    public interface IRedisQueueCompletableOperationAsync
    {
        void CompleteVoidQueuedCommandAsync(Func<CancellationToken, ValueTask> voidReadCommand);
        //void CompleteIntQueuedCommand(Func<int> intReadCommand);
        void CompleteLongQueuedCommandAsync(Func<CancellationToken, ValueTask<long>> longReadCommand);
        void CompleteBytesQueuedCommandAsync(Func<CancellationToken, ValueTask<byte[]>> bytesReadCommand);
        void CompleteMultiBytesQueuedCommandAsync(Func<CancellationToken, ValueTask<byte[][]>> multiBytesReadCommand);
        //void CompleteStringQueuedCommand(Func<string> stringReadCommand);
        //void CompleteMultiStringQueuedCommand(Func<List<string>> multiStringReadCommand);
        //void CompleteDoubleQueuedCommand(Func<double> doubleReadCommand);
        //void CompleteRedisDataQueuedCommand(Func<RedisData> redisDataReadCommand);
    }
}