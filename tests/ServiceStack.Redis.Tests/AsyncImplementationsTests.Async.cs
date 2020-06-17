// Copyright (c) Service Stack LLC. All Rights Reserved.
// License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt

using NUnit.Framework;
using NUnit.Framework.Internal.Execution;
using ServiceStack.Caching;
using ServiceStack.Data;
using ServiceStack.FluentValidation.Validators;
using ServiceStack.Redis.Generic;
using ServiceStack.Redis.Pipeline;
using ServiceStack.Redis.Support.Locking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Tests
{
    // verify that anything that implements IFoo also implements IFooAsync
    public class AsyncImplementations
    {
        private static readonly Type[] AllTypes
            = typeof(RedisClient).Assembly.GetTypes()
            .Concat(typeof(AsyncImplementations).Assembly.GetTypes())
            .Where(x => x.IsClass)
            .OrderBy(x => x.FullName).ToArray();

        [TestCase(typeof(ICacheClient), typeof(ICacheClientAsync))]
        [TestCase(typeof(ICacheClientExtended), typeof(ICacheClientExtendedAsync))]
        [TestCase(typeof(IEntityStore), typeof(IEntityStoreAsync))]
        [TestCase(typeof(IEntityStore<>), typeof(IEntityStoreAsync<>))]
        [TestCase(typeof(IRedisClient), typeof(IRedisClientAsync))]

        [TestCase(typeof(IRedisClientsManager), typeof(IRedisClientsManagerAsync))]
        [TestCase(typeof(IRedisNativeClient), typeof(IRedisNativeClientAsync))]
        [TestCase(typeof(IRedisPipeline), typeof(IRedisPipelineAsync))]
        [TestCase(typeof(IRedisPipelineShared), typeof(IRedisPipelineSharedAsync))]
        [TestCase(typeof(IRedisQueueableOperation), typeof(IRedisQueueableOperationAsync))]

        [TestCase(typeof(IRedisQueueCompletableOperation), typeof(IRedisQueueCompletableOperationAsync))]
        [TestCase(typeof(IRedisTransaction), typeof(IRedisTransactionAsync))]
        [TestCase(typeof(IRedisTransactionBase), typeof(IRedisTransactionBaseAsync))]
        [TestCase(typeof(IRedisTypedClient<>), typeof(IRedisTypedClientAsync<>))]
        [TestCase(typeof(IRemoveByPattern), typeof(IRemoveByPatternAsync))]
        [TestCase(typeof(IDistributedLock), typeof(IDistributedLockAsync))]
        public void TestSameAPI(Type syncInterface, Type asyncInterface)
        {
            int missing = 0, extra = 0;
            var syncMethods = syncInterface.GetMethods();
            var asyncMethods = asyncInterface.GetMethods();
            foreach (var method in syncMethods)
            {
                try
                {
                    Type[] expectedSignature;
                    Type expectedReturnType;
                    string expectedName = method.Name + "Async";
                    var parameters = method.GetParameters();
                    if (syncInterface == typeof(IDistributedLock) && method.Name == nameof(IDistributedLock.Lock))
                    {
                        continue; // different API due to "out: not being supported in async
                    }

                    if (syncInterface == typeof(IRedisQueueableOperation))
                    {
                        if (parameters.Length < 3) continue; // not propagating the x3 overloads; use optional parameters instead
                        expectedReturnType = typeof(void);
                        Type arg0;
                        if (parameters[0].ParameterType == typeof(Action<IRedisClient>))
                        {
                            arg0 = typeof(Func<IRedisClientAsync, ValueTask>);
                        }
                        else // Func<IRedisClient, T> - replace with Func<IRedisClientAsync, ValueTask<T>>
                        {
                            arg0 = typeof(Func<,>).MakeGenericType(
                                typeof(IRedisClientAsync),
                                typeof(ValueTask<>).MakeGenericType(parameters[0].ParameterType.GetGenericArguments()[1]));
                        }
                        expectedSignature = new[] { arg0, parameters[1].ParameterType, parameters[2].ParameterType };
                        expectedName = method.Name;

                    }
                    else if (syncInterface == typeof(IRedisQueueCompletableOperation))
                    {
                        expectedReturnType = typeof(void);
                        expectedSignature = new Type[1];
                        if (parameters[0].ParameterType == typeof(Action))
                        {
                            expectedSignature[0] = typeof(Func<CancellationToken, ValueTask>);
                        }
                        else
                        {   // Func<T> for some T - replace with Func<CancellationToken, ValueTask<T>>
                            expectedSignature[0] = typeof(Func<,>).MakeGenericType(
                                typeof(CancellationToken),
                                typeof(ValueTask<>).MakeGenericType(parameters[0].ParameterType.GetGenericArguments()));
                        }
                    }
                    else
                    {
                        // default interpretation
                        expectedSignature = new Type[parameters.Length + 1];
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            expectedSignature[i] = SwapForAsyncIfNeedeed(parameters[i].ParameterType);
                        }
                        expectedSignature[parameters.Length] = typeof(CancellationToken);

                        if (method.ReturnType == typeof(void))
                        {
                            expectedReturnType = typeof(ValueTask);
                        }
                        else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                        {
                            expectedReturnType = typeof(IAsyncEnumerable<>).MakeGenericType(method.ReturnType.GetGenericArguments());
                        }
                        else
                        {
                            expectedReturnType = typeof(ValueTask<>).MakeGenericType(SwapForAsyncIfNeedeed(method.ReturnType));
                        }
                    }

                    var found = asyncInterface.GetMethod(expectedName, expectedSignature);
                    if (found == null || expectedReturnType != found.ReturnType)
                    {
                        missing++;
                        TestContext.Out.WriteLine($"Not found: {expectedReturnType} {expectedName}({string.Join<Type>(",", expectedSignature)})");
                    }
                }
                catch (Exception ex)
                {
                    TestContext.Out.WriteLine($"{ex.Message}: {method}");
                    missing++;
                }
            }
            Assert.True(missing == 0 && extra == 0, $"{asyncInterface.Name} missing: {missing} of {syncMethods.Length}; extra: {extra} of {asyncMethods.Length} (extra not implemented yet)");
            static Type SwapForAsyncIfNeedeed(Type type)
            {
                if (type == typeof(IRedisClient)) return typeof(IRedisClientAsync);
                if (type == typeof(ICacheClient)) return typeof(ICacheClientAsync);
                return type;
            }
        }

        [TestCase(typeof(ICacheClient), typeof(ICacheClientAsync))]
        [TestCase(typeof(ICacheClientExtended), typeof(ICacheClientExtendedAsync))]
        [TestCase(typeof(IEntityStore), typeof(IEntityStoreAsync))]
        [TestCase(typeof(IEntityStore<>), typeof(IEntityStoreAsync<>))]
        [TestCase(typeof(IRedisClient), typeof(IRedisClientAsync))]

        [TestCase(typeof(IRedisClientsManager), typeof(IRedisClientsManagerAsync))]
        [TestCase(typeof(IRedisNativeClient), typeof(IRedisNativeClientAsync))]
        [TestCase(typeof(IRedisPipeline), typeof(IRedisPipelineAsync))]
        [TestCase(typeof(IRedisPipelineShared), typeof(IRedisPipelineSharedAsync))]
        [TestCase(typeof(IRedisQueueableOperation), typeof(IRedisQueueableOperationAsync))]

        [TestCase(typeof(IRedisQueueCompletableOperation), typeof(IRedisQueueCompletableOperationAsync))]
        [TestCase(typeof(IRedisTransaction), typeof(IRedisTransactionAsync))]
        [TestCase(typeof(IRedisTransactionBase), typeof(IRedisTransactionBaseAsync))]
        [TestCase(typeof(IRedisTypedClient<>), typeof(IRedisTypedClientAsync<>))]
        [TestCase(typeof(IRemoveByPattern), typeof(IRemoveByPatternAsync))]
        [TestCase(typeof(IDistributedLock), typeof(IDistributedLockAsync))]
        public void TestFullyImplemented(Type syncInterface, Type asyncInterface)
        {
            var syncTypes = AllTypes.Where(x => Implements(x, syncInterface)).ToArray();
            DumpTypes(syncInterface, syncTypes);

            var asyncTypes = AllTypes.Where(x => Implements(x, asyncInterface)).ToArray();
            DumpTypes(asyncInterface, asyncTypes);
            Assert.AreEqual(syncTypes, asyncTypes);
        }

        static void DumpTypes(Type @interface, Type[] classes)
        {
            TestContext.Out.WriteLine($"Classes that implement {@interface.Name}: {classes.Length}:");
            foreach (var @class in classes)
            {
                TestContext.Out.WriteLine($"    {@class.FullName}");
            }
            TestContext.Out.WriteLine();
        }

        static bool Implements(Type @class, Type @interface)
        {
            if (@interface.IsGenericTypeDefinition)
            {
                var found = (from iType in @class.GetInterfaces()
                             where iType.IsGenericType
                                 && iType.GetGenericTypeDefinition() == @interface
                             select iType).SingleOrDefault();
                return found != null && found.IsAssignableFrom(@class);
            }
            return @interface.IsAssignableFrom(@class);
        }

    }
}