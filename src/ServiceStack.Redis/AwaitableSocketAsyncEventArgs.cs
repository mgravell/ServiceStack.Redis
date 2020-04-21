// implementation inspired by .NET internals:
// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Net.Sockets/src/System/Net/Sockets/Socket.Tasks.cs

#if ASYNC_REDIS
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

#nullable enable // because it is in the original source

namespace ServiceStack.Redis
{
    /// <summary>A SocketAsyncEventArgs that can be awaited to get the result of an operation.</summary>
    internal sealed class AwaitableSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource, IValueTaskSource<int>
    {
        internal static readonly AwaitableSocketAsyncEventArgs Reserved = new AwaitableSocketAsyncEventArgs() { _continuation = null };
        /// <summary>Sentinel object used to indicate that the operation has completed prior to OnCompleted being called.</summary>
        private static readonly Action<object?> s_completedSentinel = new Action<object?>(state => throw new InvalidOperationException(SR.Format(SR.net_sockets_valuetaskmisuse, nameof(s_completedSentinel))));
        /// <summary>Sentinel object used to indicate that the instance is available for use.</summary>
        private static readonly Action<object?> s_availableSentinel = new Action<object?>(state => throw new InvalidOperationException(SR.Format(SR.net_sockets_valuetaskmisuse, nameof(s_availableSentinel))));
        /// <summary>
        /// <see cref="s_availableSentinel"/> if the object is available for use, after GetResult has been called on a previous use.
        /// null if the operation has not completed.
        /// <see cref="s_completedSentinel"/> if it has completed.
        /// Another delegate if OnCompleted was called before the operation could complete, in which case it's the delegate to invoke
        /// when the operation does complete.
        /// </summary>
        private Action<object?>? _continuation = s_availableSentinel;
        private ExecutionContext? _executionContext;
        private object? _scheduler;
        /// <summary>Current token value given to a ValueTask and then verified against the value it passes back to us.</summary>
        /// <remarks>
        /// This is not meant to be a completely reliable mechanism, doesn't require additional synchronization, etc.
        /// It's purely a best effort attempt to catch misuse, including awaiting for a value task twice and after
        /// it's already being reused by someone else.
        /// </remarks>
        private short _token;
        /// <summary>The cancellation token used for the current operation.</summary>
        private CancellationToken _cancellationToken;

        /// <summary>Initializes the event args.</summary>
        public AwaitableSocketAsyncEventArgs() :
            base(/*unsafeSuppressExecutionContextFlow: true*/) // avoid flowing context at lower layers as we only expose ValueTask, which handles it
        {
        }

        public bool WrapExceptionsInIOExceptions { get; set; }

        public bool Reserve() =>
            ReferenceEquals(Interlocked.CompareExchange(ref _continuation, null, s_availableSentinel), s_availableSentinel);

        private void Release()
        {
            _cancellationToken = default;
            _token++;
            Volatile.Write(ref _continuation, s_availableSentinel);
        }

        /// <summary>Initiates a send operation on the associated socket.</summary>
        /// <returns>This instance.</returns>
        public ValueTask<int> SendAsync(Socket socket, CancellationToken cancellationToken)
        {
            Debug.Assert(Volatile.Read(ref _continuation) == null, $"Expected null continuation to indicate reserved for use");

            if (socket.SendAsync(this)) //, cancellationToken))
            {
                _cancellationToken = cancellationToken;
                return new ValueTask<int>(this, _token);
            }

            int bytesTransferred = BytesTransferred;
            SocketError error = SocketError;

            Release();

            return error == SocketError.Success ?
                new ValueTask<int>(bytesTransferred) :
                new ValueTask<int>(Task.FromException<int>(CreateException(error)));
        }

        /// <summary>Initiates a send operation on the associated socket.</summary>
        /// <returns>This instance.</returns>
        public ValueTask<int> ReceiveAsync(Socket socket, CancellationToken cancellationToken)
        {
            Debug.Assert(Volatile.Read(ref _continuation) == null, $"Expected null continuation to indicate reserved for use");

            if (socket.ReceiveAsync(this)) //, cancellationToken))
            {
                _cancellationToken = cancellationToken;
                return new ValueTask<int>(this, _token);
            }

            int bytesTransferred = BytesTransferred;
            SocketError error = SocketError;

            Release();

            return error == SocketError.Success ?
                new ValueTask<int>(bytesTransferred) :
                new ValueTask<int>(Task.FromException<int>(CreateException(error)));
        }

        protected override void OnCompleted(SocketAsyncEventArgs _)
        {
            // When the operation completes, see if OnCompleted was already called to hook up a continuation.
            // If it was, invoke the continuation.
            Action<object?>? c = _continuation;
            if (c != null || (c = Interlocked.CompareExchange(ref _continuation, s_completedSentinel, null)) != null)
            {
                Debug.Assert(c != s_availableSentinel, "The delegate should not have been the available sentinel.");
                Debug.Assert(c != s_completedSentinel, "The delegate should not have been the completed sentinel.");

                object? continuationState = UserToken;
                UserToken = null;
                _continuation = s_completedSentinel; // in case someone's polling IsCompleted

                ExecutionContext? ec = _executionContext;
                if (ec == null)
                {
                    InvokeContinuation(c, continuationState, forceAsync: false, requiresExecutionContextFlow: false);
                }
                else
                {
                    // This case should be relatively rare, as the async Task/ValueTask method builders
                    // use the awaiter's UnsafeOnCompleted, so this will only happen with code that
                    // explicitly uses the awaiter's OnCompleted instead.
                    _executionContext = null;
                    ExecutionContext.Run(ec, runState =>
                    {
                        var t = (Tuple<AwaitableSocketAsyncEventArgs, Action<object?>, object>)runState!;
                        t.Item1.InvokeContinuation(t.Item2, t.Item3, forceAsync: false, requiresExecutionContextFlow: false);
                    }, Tuple.Create(this, c, continuationState));
                }
            }
        }

        /// <summary>Gets the status of the operation.</summary>
        public ValueTaskSourceStatus GetStatus(short token)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            return
                !ReferenceEquals(_continuation, s_completedSentinel) ? ValueTaskSourceStatus.Pending :
                base.SocketError == SocketError.Success ? ValueTaskSourceStatus.Succeeded :
                ValueTaskSourceStatus.Faulted;
        }

        /// <summary>Queues the provided continuation to be executed once the operation has completed.</summary>
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext? sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _scheduler = sc;
                }
                else
                {
                    TaskScheduler ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _scheduler = ts;
                    }
                }
            }

            UserToken = state; // Use UserToken to carry the continuation state around
            Action<object>? prevContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);
            if (ReferenceEquals(prevContinuation, s_completedSentinel))
            {
                // Lost the race condition and the operation has now already completed.
                // We need to invoke the continuation, but it must be asynchronously to
                // avoid a stack dive.  However, since all of the queueing mechanisms flow
                // ExecutionContext, and since we're still in the same context where we
                // captured it, we can just ignore the one we captured.
                bool requiresExecutionContextFlow = _executionContext != null;
                _executionContext = null;
                UserToken = null; // we have the state in "state"; no need for the one in UserToken
                InvokeContinuation(continuation, state, forceAsync: true, requiresExecutionContextFlow);
            }
            else if (prevContinuation != null)
            {
                // Flag errors with the continuation being hooked up multiple times.
                // This is purely to help alert a developer to a bug they need to fix.
                ThrowMultipleContinuationsException();
            }
        }

        private void InvokeContinuation(Action<object?> continuation, object? state, bool forceAsync, bool requiresExecutionContextFlow)
        {
            object? scheduler = _scheduler;
            _scheduler = null;

            if (scheduler != null)
            {
                if (scheduler is SynchronizationContext sc)
                {
                    sc.Post(s =>
                    {
                        var t = (Tuple<Action<object>, object>)s!;
                        t.Item1(t.Item2);
                    }, Tuple.Create(continuation, state));
                }
                else
                {
                    Debug.Assert(scheduler is TaskScheduler, $"Expected TaskScheduler, got {scheduler}");
                    Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, (TaskScheduler)scheduler);
                }
            }
            else if (forceAsync)
            {
                var wcb = ToWaitCallback(continuation, ref state);
                if (requiresExecutionContextFlow)
                {
                    ThreadPool.QueueUserWorkItem(wcb, state); //, preferLocal: true);
                }
                else
                {
                    ThreadPool.UnsafeQueueUserWorkItem(wcb, state); //, preferLocal: true);
                }
            }
            else
            {
                continuation(state);
            }
        }

        // needed because BCL doesn't directly expose the required API to us
        WaitCallback ToWaitCallback(Action<object?> continuation, ref object? state)
        {
            if (state is null) // we can cheat and use the continuation
            {  // as the state on a shared delegate instance
                state = continuation;
                return s => ((Action<object?>)s).Invoke(null);
            }
            return Capture(continuation, state);
            static WaitCallback Capture(Action<object?> continuation, object? state)
            {   // do this in a separate method to avoid a capture context instance usually
                return s => continuation.Invoke(s);
            }
        }

        /// <summary>Gets the result of the completion operation.</summary>
        /// <returns>Number of bytes transferred.</returns>
        /// <remarks>
        /// Unlike TaskAwaiter's GetResult, this does not block until the operation completes: it must only
        /// be used once the operation has completed.  This is handled implicitly by await.
        /// </remarks>
        public int GetResult(short token)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            SocketError error = SocketError;
            int bytes = BytesTransferred;
            CancellationToken cancellationToken = _cancellationToken;

            Release();

            if (error != SocketError.Success)
            {
                ThrowException(error, cancellationToken);
            }
            return bytes;
        }

        void IValueTaskSource.GetResult(short token)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            SocketError error = SocketError;
            CancellationToken cancellationToken = _cancellationToken;

            Release();

            if (error != SocketError.Success)
            {
                ThrowException(error, cancellationToken);
            }
        }

        private void ThrowIncorrectTokenException() => throw new InvalidOperationException(SR.InvalidOperation_IncorrectToken);

        private void ThrowMultipleContinuationsException() => throw new InvalidOperationException(SR.InvalidOperation_MultipleContinuations);

        static class SR
        {
            public const string InvalidOperation_IncorrectToken = "Incorrect value-task token; value-tasks should be awaited exactly once";
            public const string InvalidOperation_MultipleContinuations = "Only a single continuation is supported";
            public const string net_io_readfailure = "IO failure: {0}";
            public const string net_sockets_valuetaskmisuse = "value-task misused";

            internal static string Format(string format, string arg0)
                => string.Format(format, arg0);
        }

        private void ThrowException(SocketError error, CancellationToken cancellationToken)
        {
            if (error == SocketError.OperationAborted)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw CreateException(error, forAsyncThrow: false);
        }

        private Exception CreateException(SocketError error, bool forAsyncThrow = true)
        {
            Exception e = new SocketException((int)error);

            if (forAsyncThrow)
            {
                // e = ExceptionDispatchInfo.SetCurrentStackTrace(e);
            }

            return WrapExceptionsInIOExceptions ? (Exception)
                new IOException(SR.Format(SR.net_io_readfailure, e.Message), e) :
                e;
        }
    }
}
#endif