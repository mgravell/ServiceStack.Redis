using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Redis.Internal;
using ServiceStack.Text;

namespace ServiceStack.Redis
{
    public partial class RedisLock
        : IAsyncDisposable
    {
        internal static ValueTask<RedisLock> CreateAsync(IRedisClientAsync redisClient, string key,
            TimeSpan? timeOut = default, CancellationToken cancellationToken = default)
        {
            var obj = new RedisLock(redisClient, key);
            return obj.AcquireAsync(timeOut, cancellationToken).Await(obj);
        }

        // async version of ExecUtils.RetryUntilTrue
        private static async ValueTask RetryUntilTrue(Func<CancellationToken, ValueTask<bool>> action,
            TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        {
            var i = 0;
            var firstAttempt = DateTime.UtcNow;

            while (timeOut == null || DateTime.UtcNow - firstAttempt < timeOut.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                i++;
                if (await action(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
                await Task.Delay(ExecUtils.CalculateFullJitterBackOffDelay(i)).ConfigureAwait(false);
            }

            throw new TimeoutException($"Exceeded timeout of {timeOut.Value}");
        }


        private async ValueTask AcquireAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
        {
            var redisClient = (IRedisClientAsync)untypedClient;
            await RetryUntilTrue( // .ConfigureAwait(false) is below
                async ct =>
                    {
                        //This pattern is taken from the redis command for SETNX http://redis.io/commands/setnx

                        //Calculate a unix time for when the lock should expire
                        var realSpan = timeOut ?? new TimeSpan(365, 0, 0, 0); //if nothing is passed in the timeout hold for a year
                        var expireTime = DateTime.UtcNow.Add(realSpan);
                        var lockString = (expireTime.ToUnixTimeMs() + 1).ToString();

                        //Try to set the lock, if it does not exist this will succeed and the lock is obtained
                        var nx = await redisClient.SetValueIfNotExistsAsync(key, lockString, cancellationToken: ct).ConfigureAwait(false);
                        if (nx)
                            return true;

                        //If we've gotten here then a key for the lock is present. This could be because the lock is
                        //correctly acquired or it could be because a client that had acquired the lock crashed (or didn't release it properly).
                        //Therefore we need to get the value of the lock to see when it should expire

                        await redisClient.WatchAsync(new[] { key }, ct).ConfigureAwait(false);
                        var lockExpireString = await redisClient.GetValueAsync(key, ct).ConfigureAwait(false);
                        if (!long.TryParse(lockExpireString, out var lockExpireTime))
                        {
                            await redisClient.UnWatchAsync(ct).ConfigureAwait(false);  // since the client is scoped externally
                            return false;
                        }

                        //If the expire time is greater than the current time then we can't let the lock go yet
                        if (lockExpireTime > DateTime.UtcNow.ToUnixTimeMs())
                        {
                            await redisClient.UnWatchAsync(ct).ConfigureAwait(false);  // since the client is scoped externally
                            return false;
                        }

                        //If the expire time is less than the current time then it wasn't released properly and we can attempt to 
                        //acquire the lock. The above call to Watch(_lockKey) enrolled the key in monitoring, so if it changes
                        //before we call Commit() below, the Commit will fail and return false, which means that another thread 
                        //was able to acquire the lock before we finished processing.
                        await using (var trans = await redisClient.CreateTransactionAsync(ct).ConfigureAwait(false)) // we started the "Watch" above; this tx will succeed if the value has not moved 
                        {
                            trans.QueueCommand(r => r.SetValueAsync(key, lockString));
                            return await trans.CommitAsync(ct).ConfigureAwait(false); //returns false if Transaction failed
                        }
                    },
                timeOut, cancellationToken
            ).ConfigureAwait(false);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
            => ((IRedisClientAsync)untypedClient).RemoveAsync(key).Await();
    }
}