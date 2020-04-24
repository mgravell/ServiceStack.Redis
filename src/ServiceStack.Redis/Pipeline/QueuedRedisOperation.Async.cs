#if ASYNC_REDIS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis.Pipeline
{
    internal partial class QueuedRedisOperation
    {
        public virtual ValueTask ExecuteAsync(IRedisClientAsync client) => default;

        private Delegate _asyncResultProcessor;
        public void SetAsyncResultProcessor(Delegate value)
        {
            if (_asyncResultProcessor is object && _asyncResultProcessor != value)
                throw new InvalidOperationException("Only a single async result processor can be assigned");
            _asyncResultProcessor = value;
        }
        public Func<CancellationToken, ValueTask> VoidReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask>;
            set => SetAsyncResultProcessor(value);
        }

        public Func<CancellationToken, ValueTask<int>> IntReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<int>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<long>> LongReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<long>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<bool>> BoolReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<bool>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<byte[]>> BytesReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<byte[]>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<byte[][]>> MultiBytesReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<byte[][]>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<string>> StringReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<string>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<List<string>>> MultiStringReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<List<string>>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<Dictionary<string, string>>> DictionaryStringReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<Dictionary<string, string>>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<double>> DoubleReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<double>>;
            set => SetAsyncResultProcessor(value);
        }
        public Func<CancellationToken, ValueTask<RedisData>> RedisDataReadCommandAsync
        {
            get => _asyncResultProcessor as Func<CancellationToken, ValueTask<RedisData>>;
            set => SetAsyncResultProcessor(value);
        }

        public async ValueTask ProcessResultAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (VoidReadCommandAsync != null)
                {
                    await VoidReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessVoidCallback?.Invoke();
                }
                else if (IntReadCommandAsync != null)
                {
                    var result = await IntReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessIntCallback?.Invoke(result);
                    OnSuccessLongCallback?.Invoke(result);
                    OnSuccessBoolCallback?.Invoke(result == RedisNativeClient.Success);
                    OnSuccessVoidCallback?.Invoke();
                }
                else if (LongReadCommandAsync != null)
                {
                    var result = await LongReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessIntCallback?.Invoke((int)result);
                    OnSuccessLongCallback?.Invoke(result);
                    OnSuccessBoolCallback?.Invoke(result == RedisNativeClient.Success);
                    OnSuccessVoidCallback?.Invoke();
                }
                else if (DoubleReadCommandAsync != null)
                {
                    var result = await DoubleReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessDoubleCallback?.Invoke(result);
                }
                else if (BytesReadCommandAsync != null)
                {
                    var result = await BytesReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    if (result != null && result.Length == 0)
                        result = null;

                    OnSuccessBytesCallback?.Invoke(result);
                    OnSuccessStringCallback?.Invoke(result != null ? Encoding.UTF8.GetString(result) : null);
                    OnSuccessTypeCallback?.Invoke(result != null ? Encoding.UTF8.GetString(result) : null);
                    OnSuccessIntCallback?.Invoke(result != null ? int.Parse(Encoding.UTF8.GetString(result)) : 0);
                    OnSuccessBoolCallback?.Invoke(result != null && Encoding.UTF8.GetString(result) == "OK");
                }
                else if (StringReadCommandAsync != null)
                {
                    var result = await StringReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessStringCallback?.Invoke(result);
                    OnSuccessTypeCallback?.Invoke(result);
                }
                else if (MultiBytesReadCommandAsync != null)
                {
                    var result = await MultiBytesReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessMultiBytesCallback?.Invoke(result);
                    OnSuccessMultiStringCallback?.Invoke(result != null ? result.ToStringList() : null);
                    OnSuccessMultiTypeCallback?.Invoke(result.ToStringList());
                    OnSuccessDictionaryStringCallback?.Invoke(result.ToStringDictionary());
                }
                else if (MultiStringReadCommandAsync != null)
                {
                    var result = await MultiStringReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessMultiStringCallback?.Invoke(result);
                }
                else if (RedisDataReadCommandAsync != null)
                {
                    var data = await RedisDataReadCommandAsync(cancellationToken).ConfigureAwait(false);
                    OnSuccessRedisTextCallback?.Invoke(data.ToRedisText());
                    OnSuccessRedisDataCallback?.Invoke(data);
                }
                else
                {
                    ThrowIfSync();
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
            if (_asyncResultProcessor is object)
            {
                throw new InvalidOperationException("An async processor was present, but the queued operation is being processed synchronously");
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
                throw new InvalidOperationException("A sync processor was present, but the queued operation is being processed asynchronously");
            }
        }
    }
}
#endif