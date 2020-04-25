# File by file design notes

This is a walkthrough of the changes [here](https://github.com/mgravell/ServiceStack.Redis/pull/1/files)
as at the current time.

## `AsyncInterfaces/*`

These are largely direct ports of the various equivalent non-async interfaces; mostly just
empty stubs right now, but in general `Foo ISomeInterface.Bar()` maps to
`ValueTask<Foo> ISomeInterfaceAsync.BarAsync(CancellationToken cancellationToken = default)`.

The choice of `ValueTask[<T>]` reflects the combination that:

- many of the methods can genuinely return immediately, making `ValueTask<T>` optimal
- even in case of the main redis functions, when used in pipelines and transactions the
  same method ends up being quasi-immediate, especially when processing results and
  repeating a batch
- using `ValutTask[<T>]` provides [more options later](https://blog.marcgravell.com/2019/08/prefer-valuetask-to-task-always-and.html)

The `CancellationToken` is proided to enable proper cancellation usage, but is optional
so most callers will not be inconvenienced.

The naming of `ISomeInterfaceAsync` rather than `IAsyncSomeInterface` is following discussion,
and for consistency with the rest of the library.

Only a small fraction of the APIs have been implemented so far - most are currently simply
commented-out versions of the old code and have *not* yet been touched. An example of
an implemented API is:

``` c#
ValueTask<long> IncrAsync(string key, CancellationToken cancellationToken = default);
```

## src/ServiceStack.Redis/BasicRedisClientManager.ICacheClient.cs

The public `GetReadOnlyClient` has been forwarded to `GetReadOnlyClientImpl` - since
`GetReadOnlyClientImpl` returns `RedisClient`, this API can now be reused by both the
sync and async versions (`RedisClient` supports both).

## src/ServiceStack.Redis/BasicRedisClientManager.cs

Exactly the same as above, but for the read-write client.

## src/ServiceStack.Redis/BufferedReader.cs

This is probably one of the more important changes - replacing `BufferedStream`.
The existing code-base is focused on
byte-by-byte consumption. There is no async-friendly `ReadByteAsync` equivalent, so staying
with `Stream` is unhelpful; also, frankly the existing code uses such a trivial subset of
the `Stream` API that it is possible to provide a very efficient minimal API surface here
that works well for both sync and async APIs.

## src/ServiceStack.Redis/BufferedStream.cs

Directly related to the above; I am proposing marking `BufferedStream` here as deprecated. There
are a number of reasons for this, but primarily: the code doesn't use it any more! However, it
is part of the public API surface. This in itself presents a few problems:

1. it is incomplete
2. it is actively dangerous already; it won't work correctly with the "bait and switch" mechanism
   for TFMs; in particular, right now if you created a library targeting ns2.0 only  that used
   ServiceStack.Redis and
   touched this API, and then consumed your library from a netfx 4.6 exe, I would expect it to
   explode with a type-load exception; this is because the transitive dependency tree is evaluated
   at build time *from the perspective of an application*, so you'd get the netfx version of
   ServiceStack.Redis, which *does not include* this API.

Right now this warning is only in `DEBUG` builds to check for additional usage internally, but I
honestly think this should be formalized and this type deprecated. Similarly the `BStream` property.

## src/ServiceStack.Redis/Generic/RedisTypedClient.Async.cs

Right now this just declares a placeholder API.

## src/ServiceStack.Redis/Pipeline/QueuedRedisOperation.Async.cs

Adds async similes for the read-commands, and a `ProcessResultAsync` API that works like `ProcessResult`,
but allows for async reads. Rather than add a lot of delegate fields, since only a single processor is
*actually* expected, I made them share the backing field as a union. I am tempted to change from `if` to
`switch` using the inbuilt type testing, though - seems ideal. It may be useful to use this
"delegate union" on some of the similar pre-existing code, too. The use of `?.Invoke` in the processor
was purely for readability (less noise).

There is a `ThrowIfAsync` and `ThrowIfSync` pair, mostly to help verify that we haven't hooked up the
delegates incorrectly (i.e. hooked an async read-processor with a sync read).

## src/ServiceStack.Redis/Pipeline/QueuedRedisOperation.cs

The changes here are a: to use the same `?.Invoke` purely for readability, and b: to invoke `ThrowIfAsync`
(declared as a `partial` method to avoid some `#if` mess).

## src/ServiceStack.Redis/Pipeline/RedisAllPurposePipeline.Async.cs

Duplicates the pipeline code, with async love. One obervation here is that I am actively trying to avoid
adding anything on the discoverable API surface; everything is exposed through the interfaces - or via
`private protected` (which is "protected intersect internal").

One subtlety is that the pipeline API invokes the `command` during each `QueueCommand` call, which *looks*
like an async call, but in reality it is just writing to the local buffer (pending a flush). Because of this,
we assert (verified) that the method really does complete synchronously - if it didn't, the caller probably
did something odd. Note that several of the `QueueCommand` are not yet implemented, purely as an intermediate
state. The `CompleteMultiBytesQueuedCommandAsync` etc work similarly to their sync twins - just a different
delegate is hooked.

## src/ServiceStack.Redis/Pipeline/RedisAllPurposePipeline.cs

Just marked `partial`

## src/ServiceStack.Redis/Pipeline/RedisCommand.Async.cs

Very similar story to `QueuedRedisOperation.Async.cs` with the delegate union, this time implementing a new
`ExecuteAsync`. I should probably add the same `ThrowIfAsync` and `ThrowIfSync` pair to check the correct
delegates are hooked. This would also work nicely as a typed `switch` test.

## src/ServiceStack.Redis/Pipeline/RedisCommand.cs

Just marked `partial`

## src/ServiceStack.Redis/Pipeline/RedisQueueCompletableOperation.cs

Just marked `partial`

## src/ServiceStack.Redis/PooledRedisClientManager.Async.cs

New APIs for getting async clients; these *could* perhaps have been sync calls, but by making it `ValueTask<T>`
we have flexibility if you *do* choose to use an awaited pool later, rather than a synchronous pool.

## src/ServiceStack.Redis/PooledRedisClientManager.cs

`GetClient` now knows whether we are doing this for an async-aware caller. The significance of this is that
enforcing thread affinity doesn't make sense when `async`/`await` is involved, since we expet to be
thread-jumping. It *may* be possible to enforce something with the execution-context instead, but this is
complex because execution-context flows *down* but not *up* (can eplain more if desired).

## src/ServiceStack.Redis/RedisClient.Async.cs

Some very mimilar initial APIs: `time`, `incr`

## src/ServiceStack.Redis/RedisClient.cs

`RedisTransaction` is also now aware of whether it is expecting sync vs async; pass in the appropriate value.

## src/ServiceStack.Redis/RedisClientManagerCacheClient.Async.cs

Just exposes APIs that are yet to be fleshed out.

## src/ServiceStack.Redis/RedisClientManagerCacheClient.cs

Just marked `partial`

## src/ServiceStack.Redis/RedisClient_Admin.cs

The time result now needs to be used from 2 code paths, so move the reused piece to a utility method.

## src/ServiceStack.Redis/RedisClientsManagerExtensions.Async.cs

Convenience APIs to access async operations from typical pre-existing code; `GetClientAsync` tests for the
new async manager API if possible, falling back to the sync API, then asserting that the client actually
turns out to be async. `ExecAsync` use the above helper to avoid repetition. `ExecTrans` and the pub/sub piece
are just currently incomplete.

## src/ServiceStack.Redis/RedisClientsManagerExtensions.cs

Just marked `partial`

## src/ServiceStack.Redis/RedisManagerPool.Async.cs

Exposes the existing types via the async APIs

## src/ServiceStack.Redis/RedisManagerPool.cs

Very similar to code already discussed; async aware, and avoiding thread-affinity with async.

## src/ServiceStack.Redis/RedisNativeClient_Utils.Async.cs

Re-implements pre-existing read/write ligic with async equivalents; `TrackThread` is ignored, otherwise: the
code is a direct - but asyncified - port. Only logic required by `incr`, `time`, pipelines and transactions
has been touched so far.

## src/ServiceStack.Redis/RedisNativeClient.Async.cs

Implements `incr` and `time`.

## src/ServiceStack.Redis/RedisNativeClient.cs

Marks `Bstream` as deprecated - see notes of `BufferedStream`. Initializes and uses `BufferedReader`. The
`Bstream.Write(...)` call was the only usage of `Bstream` for writing, so that now goes direct to the socket.

`cmdBuffer` has been moved from `IList<T>` to `List<T>`; this allows the compiler to use the duck-typed custom
iterator, which is a value-type, to avoid allocations during `foreach`. It also avoids a `try`/`finally` (essentially
a `using`) for each `foreach`.

## src/ServiceStack.Redis/ServiceStack.Redis.csproj

Adds some TFMs:

- net472 gives netfx some `async` access, without getting into the mess that is net462 vs ns2.0
- ns2.1 gives us access to better async IO API (benefits netcoreapp3.1 - see `ASYNC_MEMORY`)

I've added `Microsoft.Bcl.AsyncInterfaces` where needed (`IAsyncEnumerable<T>` - think `kyes`, and `IAsyncDisposable`)

## src/ServiceStack.Redis/Support/Diagnostic/TrackingRedisClientProxy.cs

Not sure what this is, but I'm guessing we can continue to ignore it for ns2.1

## src/ServiceStack.Redis/Support/Diagnostic/TrackingRedisClientsManager.cs

Ditto

## src/ServiceStack.Redis/Transaction/RedisTransaction.Async.cs

This worked out easier than I expected, by making the transaction itself know whether it is expecting
sync vs async (note: I should probably verify the correct usage in the APIs). It helps that things are
mostly buffering, so the sync and async can both just call that, without having huge differences. Mostly,
it is just hooking the correct delegates, for example via `QueueExpectQueuedAsync`

## src/ServiceStack.Redis/Transaction/RedisTransaction.cs

New `_forAsync` field - the transaction knows what it is expecting to do. To echo the comment in the code:

``` c#
// if someone casts between sync/async: the sync-over-async or
// async-over-sync is entirely self-inflicted; I can't fix stupid
```

Note that `AddCurrentQueuedOperation` tests this method to see whether to use the new `partial` method or
not; this avoids a *lot* of work having to change tons of code to know about `AddCurrentQueuedOperation` and
some other hypothetical `AddCurrentQueuedOperationAsync`.

## tests/ServiceStack.Redis.Benchmark/IncrBenchmarks.cs

Mostly to show typical usage; for example, for non-batched `incr`:

``` c#
long last = default;
_ssredis.Del(Key); // todo: asyncify
for (int i = 0; i < PER_TEST; i++)
{
    last = await _ssAsync.IncrementValueAsync(Key);
}
return last;
```

## tests/ServiceStack.Redis.Benchmark/Program.cs

BDN test rig, with a smoke test in `DEBUG` mode (BDN refuses to run in `DEBUG`, so nothing lost).

## tests/ServiceStack.Redis.Benchmark/ServiceStack.Redis.Benchmark.csproj

BDN test project

## tests/ServiceStack.Redis.Tests/AsyncImplementationsTests.cs

Incomplete, but ideally everything that currently offers one of the intended sync APIs should now offer the async twin;
this helps us track that.

## tests/ServiceStack.Redis.Tests/ServiceStack.Redis.Tests.csproj

Just makes it async aware.

Obviously a *lot* more tests are needed as the actual implementation code appears.



