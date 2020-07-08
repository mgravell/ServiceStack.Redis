using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    partial class RedisSubscription
        : IRedisSubscriptionAsync
    {
        private IRedisSubscriptionAsync AsAsync() => this;
        private IRedisNativeClientAsync NativeAsync
        {
            get
            {
                return redisClient as IRedisNativeClientAsync ?? NotAsync();
                static IRedisNativeClientAsync NotAsync() => throw new InvalidOperationException("The underlying client is not async");
            }
        }

        private async ValueTask UnSubscribeFromAllChannelsMatchingAnyPatternsAsync(CancellationToken cancellationToken = default)
        {
            if (activeChannels.Count == 0) return;

            var multiBytes = await NativeAsync.PUnSubscribeAsync(Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
            ParseSubscriptionResults(multiBytes);

            this.activeChannels = new List<string>();
        }

        ValueTask IAsyncDisposable.DisposeAsync() => IsPSubscription
                ? UnSubscribeFromAllChannelsMatchingAnyPatternsAsync()
                : AsAsync().UnSubscribeFromAllChannelsAsync();

        async ValueTask IRedisSubscriptionAsync.SubscribeToChannelsAsync(string[] channels, CancellationToken cancellationToken)
        {
            var multiBytes = await NativeAsync.SubscribeAsync(channels, cancellationToken).ConfigureAwait(false);
            ParseSubscriptionResults(multiBytes);

            while (this.SubscriptionCount > 0)
            {
                multiBytes = await NativeAsync.ReceiveMessagesAsync(cancellationToken).ConfigureAwait(false);
                ParseSubscriptionResults(multiBytes);
            }
        }

        async ValueTask IRedisSubscriptionAsync.SubscribeToChannelsMatchingAsync(string[] patterns, CancellationToken cancellationToken)
        {
            var multiBytes = await NativeAsync.PSubscribeAsync(patterns, cancellationToken).ConfigureAwait(false);
            ParseSubscriptionResults(multiBytes);

            while (this.SubscriptionCount > 0)
            {
                multiBytes = await NativeAsync.ReceiveMessagesAsync(cancellationToken).ConfigureAwait(false);
                ParseSubscriptionResults(multiBytes);
            }
        }

        async ValueTask IRedisSubscriptionAsync.UnSubscribeFromAllChannelsAsync(CancellationToken cancellationToken)
        {
            if (activeChannels.Count == 0) return;

            var multiBytes = await NativeAsync.UnSubscribeAsync(Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
            ParseSubscriptionResults(multiBytes);

            this.activeChannels = new List<string>();
        }

        async ValueTask IRedisSubscriptionAsync.UnSubscribeFromChannels(string[] channels, CancellationToken cancellationToken)
        {
            var multiBytes = await NativeAsync.UnSubscribeAsync(channels, cancellationToken).ConfigureAwait(false);
            ParseSubscriptionResults(multiBytes);
        }

        async ValueTask IRedisSubscriptionAsync.UnSubscribeFromChannelsMatching(string[] patterns, CancellationToken cancellationToken)
        {
            var multiBytes = await NativeAsync.PUnSubscribeAsync(patterns, cancellationToken).ConfigureAwait(false);
            ParseSubscriptionResults(multiBytes);
        }
    }
}