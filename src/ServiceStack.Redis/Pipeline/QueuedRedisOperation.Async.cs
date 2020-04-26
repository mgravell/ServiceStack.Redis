using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Pipeline
{
    internal partial class QueuedRedisOperation
    {
        public virtual ValueTask ExecuteAsync(IRedisClientAsync client) => default;

        private Delegate _asyncReadCommand;
        private QueuedRedisOperation SetAsyncReadCommand(Delegate value)
        {
            if (_asyncReadCommand is object && _asyncReadCommand != value)
                throw new InvalidOperationException("Only a single async read command can be assigned");
            _asyncReadCommand = value;
            return this;
        }

        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask> VoidReadCommandAsync)
            => SetAsyncReadCommand(VoidReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<int>> IntReadCommandAsync)
            => SetAsyncReadCommand(IntReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<long>> LongReadCommandAsync)
            => SetAsyncReadCommand(LongReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<bool>> BoolReadCommandAsync)
            => SetAsyncReadCommand(BoolReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<byte[]>> BytesReadCommandAsync)
            => SetAsyncReadCommand(BytesReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<byte[][]>> MultiBytesReadCommandAsync)
            => SetAsyncReadCommand(MultiBytesReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<string>> StringReadCommandAsync)
            => SetAsyncReadCommand(StringReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<List<string>>> MultiStringReadCommandAsync)
            => SetAsyncReadCommand(MultiStringReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<Dictionary<string, string>>> DictionaryStringReadCommandAsync)
            => SetAsyncReadCommand(DictionaryStringReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<double>> DoubleReadCommandAsync)
            => SetAsyncReadCommand(DoubleReadCommandAsync);
        internal QueuedRedisOperation WithAsyncReadCommand(Func<CancellationToken, ValueTask<RedisData>> RedisDataReadCommandAsync)
            => SetAsyncReadCommand(RedisDataReadCommandAsync);
        
        public async ValueTask ProcessResultAsync(CancellationToken cancellationToken)
        {
            try
            {
                switch (_asyncReadCommand)
                {
                    case null:
                        break;
                    case Func<CancellationToken, ValueTask> VoidReadCommandAsync:
                        await VoidReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessVoidCallback?.Invoke();
                        break;
                    case Func<CancellationToken, ValueTask<int>> IntReadCommandAsync:
                        var i32 = await IntReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessIntCallback?.Invoke(i32);
                        OnSuccessLongCallback?.Invoke(i32);
                        OnSuccessBoolCallback?.Invoke(i32 == RedisNativeClient.Success);
                        OnSuccessVoidCallback?.Invoke();
                        break;
                    case Func<CancellationToken, ValueTask<long>> LongReadCommandAsync:
                        var i64 = await LongReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessIntCallback?.Invoke((int)i64);
                        OnSuccessLongCallback?.Invoke(i64);
                        OnSuccessBoolCallback?.Invoke(i64 == RedisNativeClient.Success);
                        OnSuccessVoidCallback?.Invoke();
                        break;
                    case Func<CancellationToken, ValueTask<double>> DoubleReadCommandAsync:
                         var f64 = await DoubleReadCommandAsync(cancellationToken).ConfigureAwait(false);
                         OnSuccessDoubleCallback?.Invoke(f64);
                        break;
                    case Func<CancellationToken, ValueTask<byte[]>> BytesReadCommandAsync:
                        var bytes = await BytesReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        if (bytes != null && bytes.Length == 0) bytes = null;
                        OnSuccessBytesCallback?.Invoke(bytes);
                        OnSuccessStringCallback?.Invoke(bytes != null ? Encoding.UTF8.GetString(bytes) : null);
                        OnSuccessTypeCallback?.Invoke(bytes != null ? Encoding.UTF8.GetString(bytes) : null);
                        OnSuccessIntCallback?.Invoke(bytes != null ? int.Parse(Encoding.UTF8.GetString(bytes)) : 0);
                        OnSuccessBoolCallback?.Invoke(bytes != null && Encoding.UTF8.GetString(bytes) == "OK");
                        break;
                    case Func<CancellationToken, ValueTask<string>> StringReadCommandAsync:
                        var s = await StringReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessStringCallback?.Invoke(s);
                        OnSuccessTypeCallback?.Invoke(s);
                        break;
                    case Func<CancellationToken, ValueTask<byte[][]>> MultiBytesReadCommandAsync:
                        var multiBytes = await MultiBytesReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessMultiBytesCallback?.Invoke(multiBytes);
                        OnSuccessMultiStringCallback?.Invoke(multiBytes != null ? multiBytes.ToStringList() : null);
                        OnSuccessMultiTypeCallback?.Invoke(multiBytes.ToStringList());
                        OnSuccessDictionaryStringCallback?.Invoke(multiBytes.ToStringDictionary());
                        break;
                    case Func<CancellationToken, ValueTask<List<string>>> MultiStringReadCommandAsync:
                        var multiString = await MultiStringReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessMultiStringCallback?.Invoke(multiString);
                        break;
                    case Func<CancellationToken, ValueTask<RedisData>> RedisDataReadCommandAsync:
                        var data = await RedisDataReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessRedisTextCallback?.Invoke(data.ToRedisText());
                        OnSuccessRedisDataCallback?.Invoke(data);
                        break;
                    case Func<CancellationToken, ValueTask<bool>> BoolReadCommandAsync:
                        var b = await BoolReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessBoolCallback?.Invoke(b);
                        break;
                    case Func<CancellationToken, ValueTask<Dictionary<string, string>>> DictionaryStringReadCommandAsync:
                        var dict = await DictionaryStringReadCommandAsync(cancellationToken).ConfigureAwait(false);
                        OnSuccessDictionaryStringCallback?.Invoke(dict);
                        break;
                      default:
                        ThrowIfSync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);

                if (OnErrorCallback != null)
                {
                    OnErrorCallback(ex);
                }
                else
                {
                    throw;
                }
            }
        }
        partial void ThrowIfAsync()
        {
            if (_asyncReadCommand is object)
            {
                throw new InvalidOperationException("An async read command was present, but the queued operation is being processed synchronously");
            }
        }
        private void ThrowIfSync()
        {
            if (VoidReadCommand is object
                || IntReadCommand is object
                || LongReadCommand is object
                || BoolReadCommand is object
                || BytesReadCommand is object
                || MultiBytesReadCommand is object
                || StringReadCommand is object
                || MultiBytesReadCommand is object
                || DictionaryStringReadCommand is object
                || DoubleReadCommand is object
                || RedisDataReadCommand is object)
            {
                throw new InvalidOperationException("A sync read command was present, but the queued operation is being processed asynchronously");
            }
        }
    }
}