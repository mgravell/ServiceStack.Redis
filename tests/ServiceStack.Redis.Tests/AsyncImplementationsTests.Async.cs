// Copyright (c) Service Stack LLC. All Rights Reserved.
// License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt

using NUnit.Framework;
using ServiceStack.Caching;
using ServiceStack.Data;
using ServiceStack.Model;
using ServiceStack.Redis.Generic;
using ServiceStack.Redis.Pipeline;
using ServiceStack.Redis.Support.Locking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
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

        private string Log(string message)
        {
            TestContext.Out.WriteLine(message);
            return message;
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
        [TestCase(typeof(IRedisSubscription), typeof(IRedisSubscriptionAsync))]
        [TestCase(typeof(IRedisHash), typeof(IRedisHashAsync))]
        [TestCase(typeof(IRedisSortedSet), typeof(IRedisSortedSetAsync))]
        [TestCase(typeof(IRedisSet), typeof(IRedisSetAsync))]

        [TestCase(typeof(IRedisList), typeof(IRedisListAsync))]
        [TestCase(typeof(IRedisHash<,>), typeof(IRedisHashAsync<,>))]
        [TestCase(typeof(IRedisSortedSet<>), typeof(IRedisSortedSetAsync<>))]
        [TestCase(typeof(IRedisSet<>), typeof(IRedisSetAsync<>))]
        [TestCase(typeof(IRedisList<>), typeof(IRedisListAsync<>))]

        [TestCase(typeof(IRedisTypedPipeline<>), typeof(IRedisTypedPipelineAsync<>))]
        [TestCase(typeof(IRedisTypedQueueableOperation<>), typeof(IRedisTypedQueueableOperationAsync<>))]
        [TestCase(typeof(IRedisTypedTransaction<>), typeof(IRedisTypedTransactionAsync<>))]

        public void TestSameAPI(Type syncInterface, Type asyncInterface)
        {
            TestContext.Out.WriteLine($"Comparing '{GetCSharpTypeName(syncInterface)}' and '{GetCSharpTypeName(asyncInterface)}'...");

            var actual = new List<string>();
            foreach (var method in asyncInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var tok = new MethodToken(method);
                actual.Add(GetSignature(tok));
            }

            var expected = new List<string>();
            ParameterToken cancellationToken = new ParameterToken("cancellationToken", typeof(CancellationToken), ParameterAttributes.Optional);
            foreach (var method in syncInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                AddExpected(method);
            }
            if (asyncInterface == typeof(IRedisSortedSetAsync)
                || asyncInterface == typeof(IRedisSetAsync)
                || asyncInterface == typeof(IRedisListAsync))
            {
                AddFrom(typeof(ICollection<string>), nameof(ICollection<string>.Clear));
                AddFrom(typeof(ICollection<string>), nameof(ICollection<string>.Add));
                AddFrom(typeof(ICollection<string>), nameof(ICollection<string>.Remove));
                AddFrom(typeof(ICollection<string>), nameof(ICollection<string>.Contains));
                AddFrom(typeof(ICollection<string>), "get_" + nameof(ICollection<string>.Count), true);

                if (asyncInterface == typeof(IRedisListAsync))
                {
                    AddFrom(typeof(IList<string>), nameof(IList<string>.IndexOf));
                    AddFrom(typeof(IList<string>), nameof(IList<string>.RemoveAt));
                    AddFrom(typeof(IList<string>), "set_Item", true);
                    AddFrom(typeof(IList<string>), "get_Item", true);
                }
            }
            else if (asyncInterface == typeof(IRedisSortedSetAsync<>)
                || asyncInterface == typeof(IRedisSetAsync<>)
                || asyncInterface == typeof(IRedisListAsync<>))
            {
                AddFrom(typeof(ICollection<>), nameof(ICollection<string>.Clear));
                AddFrom(typeof(ICollection<>), nameof(ICollection<string>.Add));
                AddFrom(typeof(ICollection<>), nameof(ICollection<string>.Remove));
                AddFrom(typeof(ICollection<>), nameof(ICollection<string>.Contains));
                AddFrom(typeof(ICollection<>), "get_" + nameof(ICollection<string>.Count), true);

                if (asyncInterface == typeof(IRedisListAsync<>))
                {
                    AddFrom(typeof(IList<>), nameof(IList<string>.IndexOf));
                    AddFrom(typeof(IList<>), nameof(IList<string>.RemoveAt));
                    AddFrom(typeof(IList<>), "set_Item", true);
                    AddFrom(typeof(IList<>), "get_Item", true);
                }
            }

            void AddFrom(Type syncInterface, string name, bool fromPropertyToMethod = false)
                => AddExpected(syncInterface.GetMethod(name), fromPropertyToMethod);

            void AddExpected(MethodInfo method, bool fromPropertyToMethod = false)
            {
                if (method is null) return;
                var tok = new MethodToken(method);

                ParameterToken[] parameters = tok.GetParameters();

                // think about the return type
                Type returnType;
                if (tok.ReturnType == typeof(void))
                {
                    returnType = typeof(ValueTask);
                }
                else if (tok.ReturnType == typeof(IDisposable))
                {
                    returnType = typeof(IAsyncDisposable);
                }
                else if (tok.ReturnType.IsGenericType && tok.ReturnType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    returnType = typeof(IAsyncEnumerable<>).MakeGenericType(tok.ReturnType.GetGenericArguments());
                }
                else
                {
                    returnType = typeof(ValueTask<>).MakeGenericType(SwapForAsyncIfNeedeed(tok.ReturnType));
                }
                string name = tok.Name + "Async";
                bool addCancellation = true;
                // sniff to see if this is a delegate hook
                if (parameters.Length == 0 && typeof(Delegate).IsAssignableFrom(tok.ReturnType) && name.StartsWith("get_"))
                {
                    // property getter; replace with event add
                    returnType = typeof(void);
                    name = "add_" + name.Substring(4);
                    parameters = new[] { new ParameterToken("value", ActionDelegateToFunc(tok.ReturnType), default) };

                }
                else if (parameters.Length == 1 && tok.ReturnType == typeof(void) && name.StartsWith("set_")
                    && typeof(Delegate).IsAssignableFrom(parameters[0].ParameterType))
                {
                    // property setter; replace with event remove
                    returnType = typeof(void);
                    name = "remove_" + name.Substring(4);
                    ref ParameterToken p = ref parameters[0];
                    p = p.WithParameterType(ActionDelegateToFunc(p.ParameterType));
                }

                if (name.StartsWith("get_") || name.StartsWith("set_") || name.StartsWith("add_") || name.StartsWith("remove_"))
                {
                    if (fromPropertyToMethod)
                    {
                        name = name switch
                        {
                            "get_ItemAsync" => "ElementAtAsync",
                            "set_ItemAsync" => "SetValueAsync",
                            _ => name.Substring(4), // don't worry about the remove, that isn't in this catchment
                        };
                    }
                    else
                    {
                        addCancellation = false;
                    }
                }

                static Type ActionDelegateToFunc(Type type)
                {
                    if (type.IsGenericType)
                    {
                        var genDef = type.GetGenericTypeDefinition();
                        var targs = type.GetGenericArguments();
                        Array.Resize(ref targs, targs.Length + 1);
                        targs[targs.Length - 1] = typeof(ValueTask);
                        return Expression.GetFuncType(targs);
                    }
                    return type;
                }

                if (asyncInterface == typeof(IRedisQueueCompletableOperationAsync) && parameters.Length == 1)
                {
                    // very unusual case; Func<Foo> => Func<CancellationToken, ValueTask<Foo>>
                    returnType = typeof(void);
                    ref ParameterToken p = ref parameters[0];
                    if (p.ParameterType == typeof(Action))
                    {
                        p = p.WithParameterType(typeof(Func<CancellationToken, ValueTask>));
                    }
                    else
                    {
                        p = p.WithParameterType(typeof(Func<,>).MakeGenericType(
                            typeof(CancellationToken), typeof(ValueTask<>).MakeGenericType(p.ParameterType.GetGenericArguments())));
                    }
                    tok = new MethodToken(name, returnType, parameters, tok.IsGenericMethod, tok.IsGenericMethodDefinition, tok.GetGenericArguments(), tok.AllAttributes());
                    expected.Add(GetSignature(tok));
                }
                else if (asyncInterface == typeof(IRedisQueueableOperationAsync) || asyncInterface == typeof(IRedisTypedQueueableOperationAsync<>))
                {
                    // very unusual case; Func<Foo> => Func<CancellationToken, ValueTask<Foo>>
                    if (parameters.Length != 3) return; // move to optionals rather than overloads
                    ref ParameterToken p = ref parameters[0]; // fixup the delegate type
                    if (p.ParameterType.IsGenericType)
                    {
                        var genDef = p.ParameterType.GetGenericTypeDefinition();
                        Type[] funcTypes = p.ParameterType.GetGenericArguments();
                        funcTypes[0] = SwapForAsyncIfNeedeed(funcTypes[0]);

                        if (genDef == typeof(Action<>))
                        {
                            Array.Resize(ref funcTypes, funcTypes.Length + 1);
                            funcTypes[funcTypes.Length - 1] = typeof(ValueTask);
                        }
                        else
                        {
                            funcTypes[funcTypes.Length - 1] = typeof(ValueTask<>)
                                .MakeGenericType(funcTypes[funcTypes.Length - 1]);
                        }

                        p = p.WithParameterType(typeof(Func<,>).MakeGenericType(funcTypes));
                    }

                    // make the other parameters optional
                    p = ref parameters[1];
                    p = p.WithAttributes(p.Attributes | ParameterAttributes.Optional);
                    p = ref parameters[2];
                    p = p.WithAttributes(p.Attributes | ParameterAttributes.Optional);
                    returnType = typeof(void);
                    name = method.Name; // retain the original name

                    tok = new MethodToken(name, returnType, parameters, tok.IsGenericMethod, tok.IsGenericMethodDefinition, tok.GetGenericArguments(), tok.AllAttributes());
                    expected.Add(GetSignature(tok));
                }
                else
                {
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (name == "PopulateWithUnionOfAsync")
                        {
                            Debugger.Break();
                        }
                        ref ParameterToken p = ref parameters[i];
                        Type type = p.ParameterType, swapped = SwapForAsyncIfNeedeed(type);
                        if (type != swapped)
                        {
                            p = p.WithParameterType(swapped);
                        }
                    }

                    static bool IsParams(in MethodToken tok)
                    {
                        var ps = tok.GetParameters();
                        if (ps is null || ps.Length == 0) return false;
                        return ps.Last().IsDefined(typeof(ParamArrayAttribute));
                    }

                    if (IsParams(tok))
                    {
                        // include it with params but without cancellationToken
                        tok = new MethodToken(name, returnType, parameters, tok.IsGenericMethod, tok.IsGenericMethodDefinition, tok.GetGenericArguments(), tok.AllAttributes());
                        expected.Add(GetSignature(tok));

                        // and now remove the params so we can get with cancellationToken
                        ref ParameterToken p = ref parameters[parameters.Length - 1];
                        p = p.WithAllAttributes(p.AllAttributes().Where(a => !(a is ParamArrayAttribute)).ToArray());
                    }

                    if (asyncInterface == typeof(IDistributedLockAsync) && name == nameof(IDistributedLockAsync.LockAsync))
                    {
                        // can't use "out", so uses a new LockState type instead
                        returnType = typeof(ValueTask<LockState>);
                        parameters = RemoveByRef(parameters);

                        static ParameterToken[] RemoveByRef(ParameterToken[] parameters)
                        {
                            if (parameters.Any(x => x.ParameterType.IsByRef))
                            {
                                parameters = parameters.Where(x => !x.ParameterType.IsByRef).ToArray();
                            }
                            return parameters;
                        }
                    }

                    if (asyncInterface == typeof(IRedisSubscriptionAsync) && tok.Name == "get_" + nameof(IRedisSubscription.SubscriptionCount))
                    {
                        // this is a purely client value; don't treat as async
                        name = tok.Name;
                        returnType = tok.ReturnType;
                    }

                    // append optional cancellationToken
                    if (addCancellation)
                    {
                        Array.Resize(ref parameters, parameters.Length + 1);
                        parameters[parameters.Length - 1] = cancellationToken;
                    }
                    tok = new MethodToken(name, returnType, parameters, tok.IsGenericMethod, tok.IsGenericMethodDefinition, tok.GetGenericArguments(), tok.AllAttributes());
                    expected.Add(GetSignature(tok));
                }
            }

            actual.Sort();
            expected.Sort();
            int extra = 0, missing = 0, match = 0;
            Log($"actual: {actual.Count}, expected: {expected.Count}");
            foreach (var method in actual.Except(expected))
            {
                Log($"+ {method}");
                extra++;
            }
            foreach (var method in expected.Except(actual))
            {
                Log($"- {method}");
                missing++;
            }
            foreach (var method in expected.Intersect(actual))
            {
                Log($"= {method}");
                match++;
            }
            Assert.True(extra == 0 && missing == 0, $"signature mismatch on {GetCSharpTypeName(asyncInterface)}; missing: {missing}, extra: {extra}, match: {match}");


            static Type SwapForAsyncIfNeedeed(Type type)
            {
                if (type.IsArray)
                {
                    var t = type.GetElementType();
                    var swapped = SwapForAsyncIfNeedeed(t);
                    if (t != swapped)
                    {
                        var rank = type.GetArrayRank();
                        return swapped.MakeArrayType(rank);
                    }
                    return type;
                }
                if (type == typeof(IRedisClient)) return typeof(IRedisClientAsync);
                if (type == typeof(ICacheClient)) return typeof(ICacheClientAsync);
                if (type == typeof(IRedisPipeline)) return typeof(IRedisPipelineAsync);
                if (type == typeof(IRedisPipelineShared)) return typeof(IRedisPipelineSharedAsync);
                if (type == typeof(IDisposable)) return typeof(IAsyncDisposable);
                if (type == typeof(IRedisList)) return typeof(IRedisListAsync);
                if (type == typeof(IRedisSet)) return typeof(IRedisSetAsync);
                if (type == typeof(IRedisSortedSet)) return typeof(IRedisSortedSetAsync);
                if (type == typeof(IRedisHash)) return typeof(IRedisHashAsync);

                if (type.IsGenericType)
                {
                    var genDef = type.GetGenericTypeDefinition();
                    var targs = type.GetGenericArguments();
                    for (int i = 0; i < targs.Length; i++)
                        targs[i] = SwapForAsyncIfNeedeed(targs[i]);

                    if (genDef == typeof(IRedisTypedClient<>)) return typeof(IRedisTypedClientAsync<>).MakeGenericType(targs);
                    if (genDef == typeof(IRedisList<>)) return typeof(IRedisListAsync<>).MakeGenericType(targs);
                    if (genDef == typeof(IRedisSet<>)) return typeof(IRedisSetAsync<>).MakeGenericType(targs);
                    if (genDef == typeof(IRedisSortedSet<>)) return typeof(IRedisSortedSetAsync<>).MakeGenericType(targs);
                    if (genDef == typeof(IHasNamed<>)) return typeof(IHasNamed<>).MakeGenericType(targs);
                }
                
                return type;
            }

            //    Type[] expectedSignature;
            //    Type expectedReturnType;
            //    string expectedName = method.Name + "Async";
            //    var parameterTypes = Array.ConvertAll(method.GetParameters(), p => p.ParameterType);
            //    if (method.IsSpecialName)
            //    {
            //        const string GET = "get_", SET = "set_";

            //        string movedToAsyncGetter = default, movedToAsyncSetter = default;
            //        if (syncInterface == typeof(IRedisClient))
            //        {
            //            expectedName = method.Name switch
            //            {
            //                // these are renamed to avoid "SaveAsync" and "SaveAsyncAsync" which would be hella confusing
            //                nameof(IRedisClient.Save) => nameof(IRedisClientAsync.ForegroundSaveAsync),
            //                nameof(IRedisClient.SaveAsync) => nameof(IRedisClientAsync.BackgroundSaveAsync),
            //                // this is renamed for consistency with BackgroundSaveAsync
            //                nameof(IRedisClient.RewriteAppendOnlyFileAsync) => nameof(IRedisClientAsync.BackgroundRewriteAppendOnlyFileAsync),
            //                _ => expectedName,
            //            };

            //            movedToAsyncGetter = method.Name switch
            //            {
            //                GET + nameof(IRedisClient.DbSize) => nameof(IRedisClientAsync.DbSizeAsync),
            //                GET + nameof(IRedisClient.LastSave) => nameof(IRedisClientAsync.LastSaveAsync),
            //                GET + nameof(IRedisClient.Info) => nameof(IRedisClientAsync.InfoAsync),
            //                _ => default,
            //            };
            //            movedToAsyncSetter = method.Name switch
            //            {
            //                SET + nameof(IRedisClient.Db) => nameof(IRedisClientAsync.ChangeDbAsync),
            //                _ => default,
            //            };
            //        }
            //        else if (syncInterface == typeof(IRedisNativeClient))
            //        {
            //            movedToAsyncGetter = method.Name switch
            //            {
            //                GET + nameof(IRedisNativeClient.DbSize) => nameof(IRedisNativeClientAsync.DbSizeAsync),
            //                GET + nameof(IRedisNativeClient.LastSave) => nameof(IRedisNativeClientAsync.LastSaveAsync),
            //                GET + nameof(IRedisNativeClient.Info) => nameof(IRedisNativeClientAsync.InfoAsync),
            //                _ => default,
            //            };
            //            movedToAsyncSetter = method.Name switch
            //            {
            //                SET + nameof(IRedisNativeClient.Db) => nameof(IRedisNativeClientAsync.SelectAsync),
            //                _ => default,
            //            };
            //        }

            //        if (movedToAsyncGetter is object)
            //        {
            //            expectedName = movedToAsyncGetter;
            //            expectedReturnType = typeof(ValueTask<>).MakeGenericType(method.ReturnType);
            //            expectedSignature = new[] { typeof(CancellationToken) };
            //        }
            //        else if (movedToAsyncSetter is object)
            //        {
            //            expectedName = movedToAsyncSetter;
            //            expectedReturnType = typeof(ValueTask);
            //            expectedSignature = new[] { parameterTypes.Single(), typeof(CancellationToken) };
            //        }
            //        else
            //        {
            //            // default is for properties and indexers etc to remain "as-is"
            //            expectedName = method.Name;
            //            expectedReturnType = method.ReturnType;
            //            expectedSignature = parameterTypes;
            //        }
            //    }

            //    else if (syncInterface == typeof(IRedisClient) && method.Name == nameof(IRedisClient.AcquireLock))
            //    {
            //        // we're merging the two overloads into one nullable
            //        if (parameterTypes.Length == 1) continue;
            //        expectedReturnType = typeof(ValueTask<IAsyncDisposable>);
            //        parameterTypes[1] = typeof(TimeSpan?); // make it optional
            //        expectedSignature = parameterTypes;
            //    }

            //    else if (syncInterface == typeof(IDistributedLock) && method.Name == nameof(IDistributedLock.Lock))
            //    {
            //        // different API shape due to "out: not being supported in async
            //        expectedReturnType = typeof(ValueTask<LockState>);
            //        expectedSignature = new[] { typeof(string), typeof(int), typeof(int), typeof(IRedisClientAsync), typeof(CancellationToken) };
            //    }
            //    else if (syncInterface == typeof(IRedisQueueableOperation))
            //    {
            //        if (parameterTypes.Length < 3) continue; // not propagating the x3 overloads; use optional parameters instead
            //        expectedReturnType = typeof(void);
            //        Type arg0;
            //        if (parameterTypes[0] == typeof(Action<IRedisClient>))
            //        {
            //            arg0 = typeof(Func<IRedisClientAsync, ValueTask>);
            //        }
            //        else // Func<IRedisClient, T> - replace with Func<IRedisClientAsync, ValueTask<T>>
            //        {
            //            arg0 = typeof(Func<,>).MakeGenericType(
            //                typeof(IRedisClientAsync),
            //                typeof(ValueTask<>).MakeGenericType(parameterTypes[0].GetGenericArguments()[1]));
            //        }
            //        expectedSignature = new[] { arg0, parameterTypes[1], parameterTypes[2] };
            //        expectedName = method.Name;
            //    }
            //    else if (syncInterface == typeof(IRedisQueueCompletableOperation))
            //    {
            //        expectedReturnType = typeof(void);
            //        expectedSignature = new Type[1];
            //        if (parameterTypes[0] == typeof(Action))
            //        {
            //            expectedSignature[0] = typeof(Func<CancellationToken, ValueTask>);
            //        }
            //        else
            //        {   // Func<T> for some T - replace with Func<CancellationToken, ValueTask<T>>
            //            expectedSignature[0] = typeof(Func<,>).MakeGenericType(
            //                typeof(CancellationToken),
            //                typeof(ValueTask<>).MakeGenericType(parameterTypes[0].GetGenericArguments()));
            //        }
            //    }
            //    else
            //    {
            //        // default interpretation
            //        expectedSignature = new Type[parameterTypes.Length + 1];
            //        for (int i = 0; i < parameterTypes.Length; i++)
            //        {
            //            expectedSignature[i] = SwapForAsyncIfNeedeed(parameterTypes[i]);
            //        }
            //        expectedSignature[parameterTypes.Length] = typeof(CancellationToken);

            //        if (method.ReturnType == typeof(void))
            //        {
            //            expectedReturnType = typeof(ValueTask);
            //        }
            //        else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            //        {
            //            expectedReturnType = typeof(IAsyncEnumerable<>).MakeGenericType(method.ReturnType.GetGenericArguments());
            //        }
            //        else
            //        {
            //            expectedReturnType = typeof(ValueTask<>).MakeGenericType(SwapForAsyncIfNeedeed(method.ReturnType));
            //        }
            //    }

            //    expected.Add(GetSignature(expectedReturnType, expectedName, method.IsGenericMethodDefinition ? method.GetGenericArguments() : default, expectedSignature));
            //}
            //expected.Sort();
            //actual.Sort();
            //int missing = 0, extra = 0;
            //TestContext.Out.WriteLine($"Total: {expected.Count} expected ('{GetCSharpTypeName(syncInterface)}'), {actual.Count} actual ('{GetCSharpTypeName(asyncInterface)}')");
            //foreach (var method in actual.Except(expected))
            //{
            //    TestContext.Out.WriteLine($"+ {method}");
            //    extra++;
            //}
            //foreach (var method in expected.Except(actual))
            //{
            //    TestContext.Out.WriteLine($"- {method}");
            //    missing++;
            //}
            //Assert.AreEqual(0, missing + extra, $"'{GetCSharpTypeName(asyncInterface)}' missing: {missing}; extra: {extra}; expected: {expected.Count}; actual: {actual.Count}");
            //static Type SwapForAsyncIfNeedeed(Type type)
            //{
            //    if (type == typeof(IRedisClient)) return typeof(IRedisClientAsync);
            //    if (type == typeof(ICacheClient)) return typeof(ICacheClientAsync);
            //    if (type == typeof(IRedisPipeline)) return typeof(IRedisPipelineAsync);
            //    if (type == typeof(IRedisPipelineShared)) return typeof(IRedisPipelineSharedAsync);
            //    if (type == typeof(IDisposable)) return typeof(IAsyncDisposable);
            //    return type;
            //}

            //static string GetSignature(Type returnType, string name, Type[] genericParameters, Type[] parameters)
            //{
            //    genericParameters ??= Type.EmptyTypes;
            //    parameters ??= Type.EmptyTypes;
            //    var sb = new StringBuilder();
            //    AppendCSharpTypeName(returnType, sb);
            //    sb.Append(" ").Append(name);
            //    if (genericParameters.Length != 0)
            //    {
            //        sb.Append("<");
            //        for (int i = 0; i < genericParameters.Length; i++)
            //        {
            //            if (i != 0) sb.Append(", ");
            //            AppendCSharpTypeName(genericParameters[i], sb);
            //        }
            //        sb.Append(">");
            //    }
            //    sb.Append("(");
            //    for (int i = 0; i < parameters.Length; i++)
            //    {
            //        if (i != 0) sb.Append(", ");
            //        AppendCSharpTypeName(parameters[i], sb);
            //    }
            //    return sb.Append(")").ToString();
            //}
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
            if (type == typeof(bool)) return "bool";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(short)) return "short";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(string)) return "string";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(object)) return "object";

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
        static string GetSignature(MethodToken method)
        {
            var sb = new StringBuilder();
            AppendCSharpTypeName(method.ReturnType, sb);
            sb.Append(' ').Append(method.Name);
            if (method.IsGenericMethodDefinition)
            {
                sb.Append('<');
                var args = method.GetGenericArguments();
                for (int i = 0; i < args.Length; i++)
                {
                    if (i != 0) sb.Append(", ");
                    sb.Append(args[i].Name);
                }
                sb.Append('>');
            }
            sb.Append('(');
            var ps = method.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (i != 0) sb.Append(", ");
                if (p.IsDefined(typeof(ParamArrayAttribute)))
                {
                    sb.Append("params ");
                }
                if (p.ParameterType.IsByRef)
                {
                    const ParameterAttributes InOut = ParameterAttributes.In | ParameterAttributes.Out;
                    sb.Append((p.Attributes & InOut) switch
                    {
                        ParameterAttributes.In => "in",
                        ParameterAttributes.Out => "out",
                        _ => "ref"
                    }).Append(' ');
                    AppendCSharpTypeName(p.ParameterType.GetElementType(), sb);
                }
                else
                {
                    AppendCSharpTypeName(p.ParameterType, sb);
                }
                sb.Append(' ').Append(p.Name);
                if ((p.Attributes & ParameterAttributes.Optional) == ParameterAttributes.Optional)
                {
                    sb.Append(" = ");
                    switch (p.DefaultValue)
                    {
                        case null:
                        case DBNull _: // used for delegates, honest!
                            sb.Append("default");
                            break;
                        case string s:
                            sb.Append(@"""").Append(s.Replace(@"""", @"""""")).Append(@"""");
                            break;
                        case object o:
                            sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture));
                            break;
                    }
                }
            }
            return sb.Append(')').ToString();
        }

        readonly struct ParameterToken
        {
            public bool IsDefined(Type attributeType)
                => _allAttributes.Any(a => attributeType.IsAssignableFrom(a.GetType()));
            public object DefaultValue { get; }
            public ParameterAttributes Attributes { get; }
            public string Name { get; }
            public Type ParameterType { get; }
            private readonly object[] _allAttributes;
            public object[] AllAttributes() => MethodToken.Clone(_allAttributes);

            internal ParameterToken WithAllAttributes(params object[] allAttributes)
                => new ParameterToken(Name, ParameterType, Attributes, DefaultValue, allAttributes);

            internal ParameterToken WithParameterType(Type parameterType)
                => new ParameterToken(Name, parameterType, Attributes, DefaultValue, _allAttributes);

            internal ParameterToken WithAttributes(ParameterAttributes attributes)
                => new ParameterToken(Name, ParameterType, attributes, DefaultValue, _allAttributes);

            public ParameterToken(ParameterInfo source)
            {
                Name = source.Name;
                ParameterType = source.ParameterType;
                Attributes = source.Attributes;
                DefaultValue = source.DefaultValue;
                _allAttributes = source.AllAttributes();
            }

            public ParameterToken(string name, Type parameterType, ParameterAttributes attributes, object defaultValue = default, params object[] allAttributes)
            {
                Name = name;
                ParameterType = parameterType;
                Attributes = attributes;
                DefaultValue = defaultValue;
                _allAttributes = allAttributes ?? Array.Empty<object>();
            }
        }

        readonly struct MethodToken
        {
            private readonly ParameterToken[] _parameters;
            private readonly Type[] _genericArguments;
            private readonly object[] _allAttributes;
            public bool IsDefined(Type attributeType)
                => _allAttributes.Any(a => attributeType.IsAssignableFrom(a.GetType()));
            internal static T[] Clone<T>(T[] source)
            {
                if (source is null) return null;
                var result = new T[source.Length];
                source.CopyTo(result, 0);
                return result;
            }
            public ParameterToken[] GetParameters() => Clone(_parameters);
            public Type[] GetGenericArguments() => Clone(_genericArguments);
            public string Name { get; }
            public bool IsGenericMethodDefinition { get; }
            public bool IsGenericMethod { get; }
            public Type ReturnType { get; }
            public object[] AllAttributes() => Clone(_allAttributes);
            public MethodToken(MethodInfo source)
            {
                Name = source.Name;
                IsGenericMethod = source.IsGenericMethod;
                IsGenericMethodDefinition = source.IsGenericMethodDefinition;
                ReturnType = source.ReturnType;
                _genericArguments = (source.IsGenericMethod || source.IsGenericMethodDefinition)
                    ? source.GetGenericArguments() : null;
                var ps = source.GetParameters();
                _parameters = ps is null ? null : Array.ConvertAll(ps, p => new ParameterToken(p));
                _allAttributes = source.AllAttributes();
            }

            public MethodToken(string name, Type returnType, ParameterToken[] parameters,
                bool isGenericMethod, bool isGenericMethodDefinition, Type[] genericArguments,
                params object[] allAttributes)
            {
                Name = name;
                ReturnType = returnType;
                IsGenericMethod = isGenericMethod;
                IsGenericMethodDefinition = isGenericMethodDefinition;
                _genericArguments = genericArguments;
                _parameters = parameters ?? Array.Empty<ParameterToken>();
                _allAttributes = allAttributes ?? Array.Empty<object>();
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
        [TestCase(typeof(IRedisSubscription), typeof(IRedisSubscriptionAsync))]
        [TestCase(typeof(IRedisHash), typeof(IRedisHashAsync))]
        [TestCase(typeof(IRedisSortedSet), typeof(IRedisSortedSetAsync))]
        [TestCase(typeof(IRedisSet), typeof(IRedisSetAsync))]

        [TestCase(typeof(IRedisList), typeof(IRedisListAsync))]
        [TestCase(typeof(IRedisHash<,>), typeof(IRedisHashAsync<,>))]
        [TestCase(typeof(IRedisSortedSet<>), typeof(IRedisSortedSetAsync<>))]
        [TestCase(typeof(IRedisSet<>), typeof(IRedisSetAsync<>))]
        [TestCase(typeof(IRedisList<>), typeof(IRedisListAsync<>))]

        [TestCase(typeof(IRedisTypedPipeline<>), typeof(IRedisTypedPipelineAsync<>))]
        [TestCase(typeof(IRedisTypedQueueableOperation<>), typeof(IRedisTypedQueueableOperationAsync<>))]
        [TestCase(typeof(IRedisTypedTransaction<>), typeof(IRedisTypedTransactionAsync<>))]
        public void TestFullyImplemented(Type syncInterface, Type asyncInterface)
        {
            HashSet<Type> except = new HashSet<Type>();
#if NET472 // only exists there!
            if (syncInterface == typeof(IRedisClientsManager))
            {
                except.Add(typeof(ServiceStack.Redis.Support.Diagnostic.TrackingRedisClientsManager));
            }
#endif

            var syncTypes = AllTypes.Except(except).Where(x => Implements(x, syncInterface)).ToArray();
            DumpTypes(syncInterface, syncTypes);

            var asyncTypes = AllTypes.Except(except).Where(x => Implements(x, asyncInterface)).ToArray();
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