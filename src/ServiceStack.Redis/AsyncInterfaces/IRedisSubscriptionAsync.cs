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
        Action<string> OnSubscribe { get; set; }

        /// <summary>
        /// Registered handler called when each message is received
        /// </summary>
        Action<string, string> OnMessage { get; set; }

        /// <summary>
        /// Registered handler called when each message is received
        /// </summary>
        Action<string, byte[]> OnMessageBytes { get; set; }

        /// <summary>
        /// Registered handler called when each channel is unsubscribed
        /// </summary>
        Action<string> OnUnSubscribe { get; set; }

        /// <summary>
        /// Subscribe to channels by name
        /// </summary>
        ValueTask SubscribeToChannelsAsync(string[] channels, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to channels matching the supplied patterns
        /// </summary>
        ValueTask SubscribeToChannelsMatchingAsync(string[] patterns, CancellationToken cancellationToken = default);

        ValueTask UnSubscribeFromAllChannelsAsync(CancellationToken cancellationToken = default);
        ValueTask UnSubscribeFromChannels(string[] channels, CancellationToken cancellationToken = default);
        ValueTask UnSubscribeFromChannelsMatching(string[] patterns, CancellationToken cancellationToken = default);
    }
}