using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack.Redis
{
    /// <summary>
    /// BufferedReader is a minimal buffer implementation that provides
    /// efficient sync and async access for byte-by-byte consumption;
    /// like BufferedStream, but with the async part
    /// </summary>
    internal sealed class BufferedReader : IDisposable
    {
        private readonly Stream _source;
        readonly byte[] _buffer;
        private int _offset, _available;
        public void Dispose()
        {
            _available = 0;
            _source.Dispose();
        }
        internal void Close()
        {
            _available = 0;
            _source.Close();
        }

        internal BufferedReader(Stream source, int bufferSize)
        {
            _source = source;
            _buffer = new byte[bufferSize];
            _offset = _available = 0;
        }

        internal int ReadByte()
            => _available > 0 ? ReadByteFromBuffer() : ReadByteSlow();

        private int ReadByteFromBuffer()
        {
            --_available;
            return _buffer[_offset++];
        }

        private int ReadByteSlow()
        {
            _available = _source.Read(_buffer, _offset = 0, _buffer.Length);
            return _available > 0 ? ReadByteFromBuffer() : -1;
        }


        private int ReadFromBuffer(byte[] buffer, int offset, int count)
        {
            // we have data in the buffer; hand it back
            if (_available < count) count = _available;
            Buffer.BlockCopy(_buffer, _offset, buffer, offset, count);
            _available -= count;
            _offset += count;
            return count;
        }

        internal int Read(byte[] buffer, int offset, int count)
            => _available > 0
            ? ReadFromBuffer(buffer, offset, count)
            : ReadSlow(buffer, offset, count);

        private int ReadSlow(byte[] buffer, int offset, int count)
        {
            // if they're asking for more than we deal in, just step out of the way
            if (count >= buffer.Length)
                return _source.Read(buffer, offset, count);

            // they're asking for less, so we could still have some left
            _available = _source.Read(_buffer, _offset = 0, _buffer.Length);
            return _available > 0 ? ReadFromBuffer(buffer, offset, count) : 0;
        }

#if ASYNC_REDIS
        internal ValueTask<int> ReadByteAsync(in CancellationToken cancellationToken = default)
            => _available > 0 ? new ValueTask<int>(ReadByteFromBuffer()) : ReadByteSlowAsync(cancellationToken);
        
        private ValueTask<int> ReadByteSlowAsync(in CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _offset = 0;
#if ASYNC_MEMORY
            var pending = _source.ReadAsync(new Memory<byte>(_buffer), cancellationToken);
            if (!pending.IsCompletedSuccessfully)
                return Awaited(this, pending);
#else
            var pending = _source.ReadAsync(_buffer, 0, _buffer.Length, cancellationToken);
            if (pending.Status != TaskStatus.RanToCompletion)
                return Awaited(this, pending);
#endif

            _available = pending.Result;
            return new ValueTask<int>(_available > 0 ? ReadByteFromBuffer() : -1);

#if ASYNC_MEMORY
            static async ValueTask<int> Awaited(BufferedReader @this, ValueTask<int> pending)
            {
                @this._available = await pending.ConfigureAwait(false);
                return @this._available > 0 ? @this.ReadByteFromBuffer() : -1;
            }
#else
            static async ValueTask<int> Awaited(BufferedReader @this, Task<int> pending)
            {
                @this._available = await pending.ConfigureAwait(false);
                return @this._available > 0 ? @this.ReadByteFromBuffer() : -1;
            }
#endif
        }

        internal ValueTask<int> ReadAsync(byte[] buffer, int offset, int count, in CancellationToken cancellationToken = default)
            => _available > 0
            ? new ValueTask<int>(ReadFromBuffer(buffer, offset, count))
            : ReadSlowAsync(buffer, offset, count, cancellationToken);

        private ValueTask<int> ReadSlowAsync(byte[] buffer, int offset, int count, in CancellationToken cancellationToken)
        {
            // if they're asking for more than we deal in, just step out of the way
            if (count >= buffer.Length)
            {
#if ASYNC_MEMORY
                return _source.ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken);
#else
                return new ValueTask<int>(_source.ReadAsync(buffer, offset, count, cancellationToken));
#endif
            }

            // they're asking for less, so we could still have some left
            _offset = 0;
#if ASYNC_MEMORY
            var pending = _source.ReadAsync(new Memory<byte>(_buffer), cancellationToken);
            if (!pending.IsCompletedSuccessfully)
                return Awaited(this, pending, buffer, offset, count);

            _available = pending.Result; // already checked status, this is fine
            return new ValueTask<int>(_available > 0 ? ReadFromBuffer(buffer, offset, count) : 0);

            static async ValueTask<int> Awaited(BufferedReader @this, ValueTask<int> pending, byte[] buffer, int offset, int count)
            {
                @this._available = await pending.ConfigureAwait(false);
                return @this._available > 0 ? @this.ReadFromBuffer(buffer, offset, count) : 0;
            }
#else
            var pending = _source.ReadAsync(_buffer, 0, _buffer.Length, cancellationToken);
            if (pending.Status != TaskStatus.RanToCompletion)
                return Awaited(this, pending, buffer, offset, count);

            _available = pending.Result; // already checked status, this is fine
            return new ValueTask<int>(_available > 0 ? ReadFromBuffer(buffer, offset, count) : 0);
            
            static async ValueTask<int> Awaited(BufferedReader @this, Task<int> pending, byte[] buffer, int offset, int count)
            {
                @this._available = await pending.ConfigureAwait(false);
                return @this._available > 0 ? @this.ReadFromBuffer(buffer, offset, count) : 0;
            }
#endif
        }
#endif
        }
}
