#if ASYNC_REDIS
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

		public static ValueTask<IRedisClientAsync> GetClientAsync(this IRedisClientsManager redisManager, CancellationToken cancellationToken = default)
		{
			return redisManager is IRedisClientsManagerAsync asyncManager
				? asyncManager.GetClientAsync(cancellationToken)
				: new ValueTask<IRedisClientAsync>(redisManager.GetClient() as IRedisClientAsync ?? Throw(redisManager));

			static IRedisClientAsync Throw(IRedisClientsManager redisManager)
				=> throw new NotSupportedException($"The client returned from '{redisManager?.GetType().FullName}' does not implement {nameof(IRedisClientAsync)}");
		}

		public static async Task ExecAsync(this IRedisClientsManager redisManager, Func<IRedisClientAsync, Task> lambda)
		{
			using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				await lambda(redis).ConfigureAwait(false);
			}
		}

		public static async Task<T> ExecAsync<T>(this IRedisClientsManager redisManager, Func<IRedisClientAsync, Task<T>> lambda)
		{
			using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
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

		public static async Task ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, Task> lambda)
		{
			using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}

		public static async Task<T> ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, Task<T>> lambda)
		{
			using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				return await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}

		public static async Task<IList<T>> ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, Task<IList<T>>> lambda)
		{
			using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				return await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}

		public static async Task<List<T>> ExecAsAsync<T>(this IRedisClientsManager redisManager, Func<IRedisTypedClientAsync<T>, Task<List<T>>> lambda)
		{
			using (var redis = await redisManager.GetClientAsync().ConfigureAwait(false))
			{
				return await lambda(redis.As<T>()).ConfigureAwait(false);
			}
		}
	}

}
#endif