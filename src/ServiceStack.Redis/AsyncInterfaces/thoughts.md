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
   reasonable starting point for adding the async API into netfx

8. Have proposed adding a ns2.1 TFM; allows access to more efficient Stream/Socket APIs

9. Throughout, I'm working on the assumption that the idea is to fit async into the existing infrastructure; there
   may also be a number of further optimizations possible, but I'm treating that as separate (unless very very low
   impact, like the `IList<T>` to `List<T>` swap); you may alo wish to consider my input on:

   a. running an optimization pass within the current infrastructure (there are a *lot* of allocations, for example)
      (low risk, bite sized pieces, so required effort is "as much time as you want to spend"; might be able to
      get a the allocations significantly down; probably no direct speedup, but it woud reduce GC for consumers)
   b. overhauling the internals to a different infrastructure
      (much higher risk; high effort; looking at the raw performance comparison between ServiceStack.Redis and Respite,
      my considered opinion is that this would **not** be a good idea, as any gains would not justify the risks,
      and may be able to get a high % of of the same gains simply with an in-place optimization pass; this is also
      highly constrained because ServiceStack.Redis is a fairly "leaky" abstraction, with a lot of the implementation
      details exposed - this makes it hard to go "all in" on an overhaul; I'd say: seriously don't)