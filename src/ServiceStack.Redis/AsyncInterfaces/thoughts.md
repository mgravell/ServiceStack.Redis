Working assumptions:

0. all the interfaces need move to ServiceStack.Interfaces

1. There is a tricky question around GetAsyncClient

   (note: probably resolved via IAsyncRedisClientsManager)

   because it is implemented via a lock, it is not really possible
   to make this a true async-friendly fetch when the queue is drained, so we could end up with (worker) threads
   blocked waiting to get connections; the ideal would be a method like

       ValueTask<IAsyncRedisClient> IRedisClientsManager.GetAsyncClientAsync()

   which returns a connection immediately if is available, else *asynchronously* awaits an available collection;
   however, having a pool that supports both sync+async awaiters is non-trivial.

   I *have* implemented such, but it is a bigger change to the pool core.

2. generally working with `Task[<T>]`; this is a huge topic in itself, but... we can revisit it as needed

3. need to investigate how timeouts work currently, and think whether to retain that model, or take
`CancellationToken` inputs

4. `TrackThread` doesn't make sense with async; simply omitting

5. `Bstream` - is exposed via `protected`, but: very sub-optimal for `async`; have proposed deprecating

6. Similarly, have proposed deprecating local `BufferedStream` impl

7. Have proposed adding TFM for net472; can't go as low as net45, and net462 is a mess re ns/netfx; net472 is a
   reasonale starting point for adding the async API into netfx