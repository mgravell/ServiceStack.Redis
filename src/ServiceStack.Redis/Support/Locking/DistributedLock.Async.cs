﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Support.Locking
{
    partial class DistributedLock : IDistributedLockAsync
    {
        public IDistributedLockAsync AsAsync() => this;

        async ValueTask<LockState> IDistributedLockAsync.LockAsync(string key, int acquisitionTimeout, int lockTimeout, IRedisClientAsync client, CancellationToken cancellationToken)
        {
            long lockExpire = 0;

            // cannot lock on a null key
            if (key == null)
                return new LockState(LOCK_NOT_ACQUIRED, lockExpire);

            const int sleepIfLockSet = 200;
            acquisitionTimeout *= 1000; //convert to ms
            int tryCount = (acquisitionTimeout / sleepIfLockSet) + 1;

            var ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            var newLockExpire = CalculateLockExpire(ts, lockTimeout);

            var nativeClient = (IRedisNativeClientAsync)client;
            long wasSet = await nativeClient.SetNXAsync(key, BitConverter.GetBytes(newLockExpire), cancellationToken).ConfigureAwait(false);
            int totalTime = 0;
            while (wasSet == LOCK_NOT_ACQUIRED && totalTime < acquisitionTimeout)
            {
                int count = 0;
                while (wasSet == 0 && count < tryCount && totalTime < acquisitionTimeout)
                {
                    await Task.Delay(sleepIfLockSet).ConfigureAwait(false);
                    totalTime += sleepIfLockSet;
                    ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
                    newLockExpire = CalculateLockExpire(ts, lockTimeout);
                    wasSet = await nativeClient.SetNXAsync(key, BitConverter.GetBytes(newLockExpire), cancellationToken).ConfigureAwait(false);
                    count++;
                }
                // acquired lock!
                if (wasSet != LOCK_NOT_ACQUIRED) break;

                // handle possibliity of crashed client still holding the lock
                var pipe = await client.CreatePipelineAsync(cancellationToken);
                await using (pipe.ConfigureAwait(false))
                {
                    long lockValue = 0;
                    pipe.QueueCommand(r => ((IRedisNativeClientAsync)r).WatchAsync(new[] { key }, cancellationToken));
                    pipe.QueueCommand(r => ((IRedisNativeClientAsync)r).GetAsync(key, cancellationToken), x => lockValue = (x != null) ? BitConverter.ToInt64(x, 0) : 0);
                    await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

                    // if lock value is 0 (key is empty), or expired, then we can try to acquire it
                    ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
                    if (lockValue < ts.TotalSeconds)
                    {
                        ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
                        newLockExpire = CalculateLockExpire(ts, lockTimeout);
                        var trans = await client.CreateTransactionAsync(cancellationToken).ConfigureAwait(false);
                        await using (trans.ConfigureAwait(false))
                        {
                            var expire = newLockExpire;
                            trans.QueueCommand(r => ((IRedisNativeClientAsync)r).SetAsync(key, BitConverter.GetBytes(expire), cancellationToken: cancellationToken));
                            if (await trans.CommitAsync(cancellationToken).ConfigureAwait(false))
                                wasSet = LOCK_RECOVERED; //recovered lock!
                        }
                    }
                    else
                    {
                        await nativeClient.UnWatchAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                if (wasSet != LOCK_NOT_ACQUIRED) break;
                await Task.Delay(sleepIfLockSet).ConfigureAwait(false);
                totalTime += sleepIfLockSet;
            }
            if (wasSet != LOCK_NOT_ACQUIRED)
            {
                lockExpire = newLockExpire;
            }
            return new LockState(wasSet, lockExpire);
        }

        async ValueTask<bool> IDistributedLockAsync.UnlockAsync(string key, long lockExpire, IRedisClientAsync client, CancellationToken cancellationToken)
        {
            if (lockExpire <= 0)
                return false;
            long lockVal = 0;
            var nativeClient = (IRedisNativeClientAsync)client;
            var pipe = await client.CreatePipelineAsync(cancellationToken).ConfigureAwait(false);
            await using (pipe.ConfigureAwait(false))
            {
                pipe.QueueCommand(r => ((IRedisNativeClientAsync)r).WatchAsync(new[] { key }, cancellationToken));
                pipe.QueueCommand(r => ((IRedisNativeClientAsync)r).GetAsync(key, cancellationToken),
                                  x => lockVal = (x != null) ? BitConverter.ToInt64(x, 0) : 0);
                await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (lockVal != lockExpire)
            {
                if (lockVal != 0)
                    Debug.WriteLine($"Unlock(): Failed to unlock key {key}; lock has been acquired by another client ");
                else
                    Debug.WriteLine($"Unlock(): Failed to unlock key {key}; lock has been identifed as a zombie and harvested ");
                await nativeClient.UnWatchAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var trans = await client.CreateTransactionAsync(cancellationToken);
            await using (trans.ConfigureAwait(false))
            {
                trans.QueueCommand(r => ((IRedisNativeClientAsync)r).DelAsync(key, cancellationToken));
                var rc = await trans.CommitAsync(cancellationToken).ConfigureAwait(false);
                if (!rc)
                    Debug.WriteLine($"Unlock(): Failed to delete key {key}; lock has been acquired by another client ");
                return rc;
            }
        }
    }
}