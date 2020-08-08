using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    public interface IRedisSubscriptionAsync
        : IAsyncDisposable
    {
        /// <summary>
        /// The number of active subscriptions this client has
        /// </summary>
        long SubscriptionCount { get; }

        /// <summary>
        /// Registered handler called after client *Subscribes* to each new channel
        /// </summary>
        event Func<string, ValueTask> OnSubscribeAsync;

        /// <summary>
        /// Registered handler called when each message is received
        /// </summary>
        event Func<string, string, ValueTask> OnMessageAsync;

        /// <summary>
        /// Registered handler called when each message is received
        /// </summary>
        event Func<string, byte[], ValueTask> OnMessageBytesAsync;

        /// <summary>
        /// Registered handler called when each channel is unsubscribed
        /// </summary>
        event Func<string, ValueTask> OnUnSubscribeAsync;

        /// <summary>
        /// Subscribe to channels by name
        /// </summary>
        ValueTask SubscribeToChannelsAsync(string[] channels, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to channels matching the supplied patterns
        /// </summary>
        ValueTask SubscribeToChannelsMatchingAsync(string[] patterns, CancellationToken cancellationToken = default);

        ValueTask UnSubscribeFromAllChannelsAsync(CancellationToken cancellationToken = default);
        ValueTask UnSubscribeFromChannelsAsync(string[] channels, CancellationToken cancellationToken = default);
        ValueTask UnSubscribeFromChannelsMatchingAsync(string[] patterns, CancellationToken cancellationToken = default);
    }
}