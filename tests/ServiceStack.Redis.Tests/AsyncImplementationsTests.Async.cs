// Copyright (c) Service Stack LLC. All Rights Reserved.
// License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt

using NUnit.Framework;
using ServiceStack.Caching;
using ServiceStack.Data;
using ServiceStack.Redis.Generic;
using ServiceStack.Redis.Pipeline;
using ServiceStack.Redis.Support.Locking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Tests
{
    // verify that anything that implements IFoo also implements IFooAsync
    [Category("Async")]
    public class AsyncImplementationTests
    {
        private static readonly Type[] AllTypes
            = typeof(RedisClient).Assembly.GetTypes()
            .Concat(typeof(AsyncImplementationTests).Assembly.GetTypes())
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
            TestContext.Out.WriteLine($"Comparing '{GetCSharpTypeName(syncInterface)}' and '{GetCSharpTypeName(asyncInterface)}'...");

            var actual = new List<string>();
            foreach (var method in asyncInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                actual.Add(GetSignature(method.ReturnType, method.Name, method.IsGenericMethodDefinition ? method.GetGenericArguments() : default,
                    Array.ConvertAll(method.GetParameters(), p => p.ParameterType)));
            }
            var expected = new List<string>();
            foreach (var method in syncInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Type[] expectedSignature;
                Type expectedReturnType;
                string expectedName = method.Name + "Async";
                var parameterTypes = Array.ConvertAll(method.GetParameters(), p => p.ParameterType);
                if (method.IsSpecialName)
                {
                    const string GET = "get_", SET = "set_";

                    string movedToAsyncGetter = default, movedToAsyncSetter = default;
                    if (syncInterface == typeof(IRedisClient))
                    {
                        expectedName = method.Name switch
                        {
                            // these are renamed to avoid "SaveAsync" and "SaveAsyncAsync" which would be hella confusing
                            nameof(IRedisClient.Save) => nameof(IRedisClientAsync.ForegroundSaveAsync),
                            nameof(IRedisClient.SaveAsync) => nameof(IRedisClientAsync.BackgroundSaveAsync),
                            // this is renamed for consistency with BackgroundSaveAsync
                            nameof(IRedisClient.RewriteAppendOnlyFileAsync) => nameof(IRedisClientAsync.BackgroundRewriteAppendOnlyFileAsync),
                            _ => expectedName,
                        };

                        movedToAsyncGetter = method.Name switch
                        {
                            GET + nameof(IRedisClient.DbSize) => nameof(IRedisClientAsync.DbSizeAsync),
                            GET + nameof(IRedisClient.LastSave) => nameof(IRedisClientAsync.LastSaveAsync),
                            GET + nameof(IRedisClient.Info) => nameof(IRedisClientAsync.InfoAsync),
                            _ => default,
                        };
                        movedToAsyncSetter = method.Name switch
                        {
                            SET + nameof(IRedisClient.Db) => nameof(IRedisClientAsync.ChangeDbAsync),
                            _ => default,
                        };
                    }
                    else if (syncInterface == typeof(IRedisNativeClient))
                    {
                        movedToAsyncGetter = method.Name switch
                        {
                            GET + nameof(IRedisNativeClient.DbSize) => nameof(IRedisNativeClientAsync.DbSizeAsync),
                            GET + nameof(IRedisNativeClient.LastSave) => nameof(IRedisNativeClientAsync.LastSaveAsync),
                            GET + nameof(IRedisNativeClient.Info) => nameof(IRedisNativeClientAsync.InfoAsync),
                            _ => default,
                        };
                        movedToAsyncSetter = method.Name switch
                        {
                            SET + nameof(IRedisNativeClient.Db) => nameof(IRedisNativeClientAsync.SelectAsync),
                            _ => default,
                        };
                    }

                    if (movedToAsyncGetter is object)
                    {
                        expectedName = movedToAsyncGetter;
                        expectedReturnType = typeof(ValueTask<>).MakeGenericType(method.ReturnType);
                        expectedSignature = new[] { typeof(CancellationToken) };
                    }
                    else if (movedToAsyncSetter is object)
                    {
                        expectedName = movedToAsyncSetter;
                        expectedReturnType = typeof(ValueTask);
                        expectedSignature = new[] { parameterTypes.Single(), typeof(CancellationToken) };
                    }
                    else
                    {
                        // default is for properties and indexers etc to remain "as-is"
                        expectedName = method.Name;
                        expectedReturnType = method.ReturnType;
                        expectedSignature = parameterTypes;
                    }
                }

                else if (syncInterface == typeof(IRedisClient) && method.Name == nameof(IRedisClient.AcquireLock))
                {
                    // we're merging the two overloads into one nullable
                    if (parameterTypes.Length == 1) continue;
                    expectedReturnType = typeof(ValueTask<IAsyncDisposable>);
                    parameterTypes[1] = typeof(TimeSpan?); // make it optional
                    expectedSignature = parameterTypes;
                }

                else if (syncInterface == typeof(IDistributedLock) && method.Name == nameof(IDistributedLock.Lock))
                {
                    // different API shape due to "out: not being supported in async
                    expectedReturnType = typeof(ValueTask<LockState>);
                    expectedSignature = new[] { typeof(string), typeof(int), typeof(int), typeof(IRedisClientAsync), typeof(CancellationToken) };
                }
                else if (syncInterface == typeof(IRedisQueueableOperation))
                {
                    if (parameterTypes.Length < 3) continue; // not propagating the x3 overloads; use optional parameters instead
                    expectedReturnType = typeof(void);
                    Type arg0;
                    if (parameterTypes[0] == typeof(Action<IRedisClient>))
                    {
                        arg0 = typeof(Func<IRedisClientAsync, ValueTask>);
                    }
                    else // Func<IRedisClient, T> - replace with Func<IRedisClientAsync, ValueTask<T>>
                    {
                        arg0 = typeof(Func<,>).MakeGenericType(
                            typeof(IRedisClientAsync),
                            typeof(ValueTask<>).MakeGenericType(parameterTypes[0].GetGenericArguments()[1]));
                    }
                    expectedSignature = new[] { arg0, parameterTypes[1], parameterTypes[2] };
                    expectedName = method.Name;
                }
                else if (syncInterface == typeof(IRedisQueueCompletableOperation))
                {
                    expectedReturnType = typeof(void);
                    expectedSignature = new Type[1];
                    if (parameterTypes[0] == typeof(Action))
                    {
                        expectedSignature[0] = typeof(Func<CancellationToken, ValueTask>);
                    }
                    else
                    {   // Func<T> for some T - replace with Func<CancellationToken, ValueTask<T>>
                        expectedSignature[0] = typeof(Func<,>).MakeGenericType(
                            typeof(CancellationToken),
                            typeof(ValueTask<>).MakeGenericType(parameterTypes[0].GetGenericArguments()));
                    }
                }
                else
                {
                    // default interpretation
                    expectedSignature = new Type[parameterTypes.Length + 1];
                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        expectedSignature[i] = SwapForAsyncIfNeedeed(parameterTypes[i]);
                    }
                    expectedSignature[parameterTypes.Length] = typeof(CancellationToken);

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

                expected.Add(GetSignature(expectedReturnType, expectedName, method.IsGenericMethodDefinition ? method.GetGenericArguments() : default, expectedSignature));
            }
            expected.Sort();
            actual.Sort();
            int missing = 0, extra = 0;
            TestContext.Out.WriteLine($"Total: {expected.Count} expected ('{GetCSharpTypeName(syncInterface)}'), {actual.Count} actual ('{GetCSharpTypeName(asyncInterface)}')");
            foreach (var method in actual.Except(expected))
            {
                TestContext.Out.WriteLine($"+ {method}");
                extra++;
            }
            foreach (var method in expected.Except(actual))
            {
                TestContext.Out.WriteLine($"- {method}");
                missing++;
            }
            Assert.AreEqual(0, missing + extra, $"'{GetCSharpTypeName(asyncInterface)}' missing: {missing}; extra: {extra}; expected: {expected.Count}; actual: {actual.Count}");
            static Type SwapForAsyncIfNeedeed(Type type)
            {
                if (type == typeof(IRedisClient)) return typeof(IRedisClientAsync);
                if (type == typeof(ICacheClient)) return typeof(ICacheClientAsync);
                if (type == typeof(IRedisPipeline)) return typeof(IRedisPipelineAsync);
                if (type == typeof(IRedisPipelineShared)) return typeof(IRedisPipelineSharedAsync);
                if (type == typeof(IDisposable)) return typeof(IAsyncDisposable);
                return type;
            }

            static string GetSignature(Type returnType, string name, Type[] genericParameters, Type[] parameters)
            {
                genericParameters ??= Type.EmptyTypes;
                parameters ??= Type.EmptyTypes;
                var sb = new StringBuilder();
                AppendCSharpTypeName(returnType, sb);
                sb.Append(" ").Append(name);
                if (genericParameters.Length != 0)
                {
                    sb.Append("<");
                    for (int i = 0; i < genericParameters.Length; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        AppendCSharpTypeName(genericParameters[i], sb);
                    }
                    sb.Append(">");
                }
                sb.Append("(");
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i != 0) sb.Append(", ");
                    AppendCSharpTypeName(parameters[i], sb);
                }
                return sb.Append(")").ToString();
            }

            static string GetCSharpTypeName(Type type)
            {
                if (!(type.IsGenericType || type.IsArray))
                {
                    return GetSimpleCSharpTypeName(type);
                }
                var sb = new StringBuilder();
                AppendCSharpTypeName(type, sb);
                return sb.ToString();
            }
            static string GetSimpleCSharpTypeName(Type type)
            {
                if (type == typeof(void)) return "void";
                if (type == typeof(int)) return "int";
                if (type == typeof(bool)) return "bool";
                if (type == typeof(string)) return "string";
                if (type == typeof(double)) return "double";
                if (type == typeof(long)) return "long";
                if (type == typeof(object)) return "object";
                if (type == typeof(byte)) return "byte";
                return type.Name;
            }
            static void AppendCSharpTypeName(Type type, StringBuilder sb)
            {
                if (type.IsArray)
                {
                    // we won't worry about the difference between vector and non-vector rank zero arrays
                    AppendCSharpTypeName(type.GetElementType(), sb);
                    sb.Append("[").Append(',', type.GetArrayRank() - 1).Append("]");
                }
                else if (type.IsGenericParameter)
                {
                    sb.Append(type.Name);
                }
                else if (type.IsGenericType)
                {
                    var nullable = Nullable.GetUnderlyingType(type);
                    if (nullable is object)
                    {
                        AppendCSharpTypeName(nullable, sb);
                        sb.Append("?");
                    }
                    else
                    {
                        var name = type.Name;
                        int i = name.IndexOf('`');
                        if (i < 0)
                        {
                            sb.Append(name);
                        }
                        else
                        {
                            sb.Append(name, 0, i);
                        }
                        sb.Append("<");
                        var targs = type.GetGenericArguments();
                        for (i = 0; i < targs.Length; i++)
                        {
                            if (i != 0) sb.Append(", ");
                            sb.Append(GetCSharpTypeName(targs[i]));
                        }
                        sb.Append(">");
                    }
                }
                else
                {
                    sb.Append(GetSimpleCSharpTypeName(type));
                }
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