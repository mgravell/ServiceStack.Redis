using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Caching;
using ServiceStack.Redis.Generic;

namespace ServiceStack.Redis
{
	/// <summary>
	/// Useful wrapper IRedisClientsManager to cut down the boiler plate of most IRedisClient access
	/// </summary>
	public static partial class RedisClientsManagerExtensions
	{
		///// <summary>
		///// Creates a PubSubServer that uses a background thread to listen and process for
		///// Redis Pub/Sub messages published to the specified channel. 
		///// Use optional callbacks to listen for message, error and life-cycle events.
		///// Callbacks can be assigned later, then call Start() for PubSubServer to start listening for messages
		///// </summary>
		//public static IRedisPubSubServer CreatePubSubServer(this IRedisClientsManager redisManager, 
		//    string channel,
		//    Action<string, string> onMessage = null,
		//    Action<Exception> onError = null,
		//    Action onInit = null,
		//    Action onStart = null,
		//    Action onStop = null)
		//{
		//    return new RedisPubSubServer(redisManager, channel)
		//    {
		//        OnMessage = onMessage,
		//        OnError = onError,
		//        OnInit = onInit,
		//        OnStart = onStart,
		//        OnStop = onStop,
		//    };
		//}

		private static T InvalidAsyncClient<T>(IRedisClientsManager manager, string method) where T : class
			=> throw new NotSupportedException($"The client returned from '{manager?.GetType().FullName ?? "(null)"}.{method}()' does not implement {typeof(T).Name}");

		public static ValueTask<IRedisClientAsync> GetClientAsync(this IRedisClientsManager redisManager, CancellationToken cancellationToken = default)
		{
			return redisManager is IRedisClientsManagerAsync asyncManager
				? asyncManager.GetClientAsync(cancellationToken)
				: new ValueTask<IRedisClientAsync>(redisManager.GetClient() as IRedisClientAsync ?? InvalidAsyncClient<IRedisClientAsync>(redisManager, nameof(redisManager.GetClient)));
		}

		public static ValueTask<IRedisClientAsync> GetReadOnlyClientAsync(this IRedisClientsManager redisManager, CancellationToken cancellationToken = default)
		{
			return redisManager is IRedisClientsManagerAsync asyncManager
				? asyncManager.GetReadOnlyClientAsync(cancellationToken)
				: new ValueTask<IRedisClientAsync>(redisManager.GetReadOnlyClient() as IRedisClientAsync ?? InvalidAsyncClient<IRedisClientAsync>(redisManager, nameof(redisManager.GetReadOnlyClient)));
		}

		public static ValueTask<ICacheClientAsync> GetCacheClientAsync(this IRedisClientsManager redisManager, CancellationToken cancellationToken = default)
		{
			return redisManager is IRedisClientsManagerAsync asyncManager
				? asyncManager.GetCacheClientAsync(cancellationToken)
				: new ValueTask<ICacheClientAsync>(redisManager.GetCacheClient() as ICacheClientAsync ?? InvalidAsyncClient<ICacheClientAsync>(redisManager, nameof(redisManager.GetCacheClient)));
		}

		public static ValueTask<ICacheClientAsync> GetReadOnlyCacheClientAsync(this IRedisClientsManager redisManager, CancellationToken cancellationToken = default)
		{
			return redisManager is IRedisClientsManagerAsync asyncManager
				? asyncManager.GetReadOnlyCacheClientAsync(cancellationToken)
				: new ValueTask<ICacheClientAsync>(redisManager.GetReadOnlyCacheClient() as ICacheClientAsync ?? InvalidAsyncClient<ICacheClientAsync>(redisManager, nameof(redisManager.GetCacheClient)));
		}


		public static async ValueTask ExecAsync(this IRedisClientsManager redisManager, Func<IRedisClientAsync, ValueTask> lambda)
		{
			await using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				await lambda(redis).ConfigureAwait(false);
			}
		}

		public static async ValueTask<T> ExecAsync<T>(this IRedisClientsManager redisManager, Func<IRedisClientAsync, ValueTask<T>> lambda)
		{
			await using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				return await lambda(redis).ConfigureAwait(false);
			}
		}

		//public static void ExecTrans(this IRedisClientsManager redisManager, Action<IRedisTransaction> lambda)
		//{
		//	using (var redis = redisManager.GetClient())
		//	using (var trans = redis.CreateTransaction())
		//	{
		//		lambda(trans);

		//		trans.Commit();
		//	}
		//}

		public static async ValueTask ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, ValueTask> lambda)
		{
			await using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}

		public static async ValueTask<T> ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, ValueTask<T>> lambda)
		{
			await using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				return await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}

		public static async ValueTask<IList<T>> ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, ValueTask<IList<T>>> lambda)
		{
			await using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				return await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}

		public static async ValueTask<List<T>> ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, ValueTask<List<T>>> lambda)
		{
			await using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				return await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}
	}

}