// Copyright (c) Service Stack LLC. All Rights Reserved.
// License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt

#if ASYNC_REDIS
using NUnit.Framework;
using NUnit.Framework.Internal.Execution;
using ServiceStack.Caching;
using ServiceStack.Data;
using ServiceStack.FluentValidation.Validators;
using ServiceStack.Redis.Generic;
using ServiceStack.Redis.Pipeline;
using System;
using System.Linq;
using System.Reflection;

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
        [TestCase(typeof(IRedisClient), typeof(IRedisClientAsync))]
        [TestCase(typeof(IRedisClientsManager), typeof(IRedisClientsManagerAsync))]
        [TestCase(typeof(IRedisNativeClient), typeof(IRedisNativeClientAsync))]
        [TestCase(typeof(IRedisPipelineShared), typeof(IRedisPipelineSharedAsync))]
        [TestCase(typeof(IRemoveByPattern), typeof(IRemoveByPatternAsync))]
        [TestCase(typeof(IEntityStore<>), typeof(IEntityStoreAsync<>))]
        [TestCase(typeof(IRedisTypedClient<>), typeof(IRedisTypedClientAsync<>))]
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
            foreach(var @class in classes)
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
#endif