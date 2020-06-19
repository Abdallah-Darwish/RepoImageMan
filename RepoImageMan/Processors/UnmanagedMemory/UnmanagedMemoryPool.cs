using Serilog.Data;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RepoImageMan.Processors.UnmanagedMemory
{
    class UnmanagedMemoryPool : MemoryPool<byte>
    {
        private readonly IntPtr[] _buffers;
        private readonly UnmanagedMemoryManager?[] _rentedManagers;
        private readonly Channel<int> _availableBuffers;
        public void Return(int idx)
        {
            _rentedManagers[idx] = null;
            _availableBuffers.Writer.TryWrite(idx);
        }
        public override int MaxBufferSize { get; }
        public override IMemoryOwner<byte> Rent(int minBufferSize = -1) => RentManager(minBufferSize);
        public UnmanagedMemoryManager RentManager(int minBufferSize = -1)
        {
            if (minBufferSize == -1) { minBufferSize = MaxBufferSize; }
            if (minBufferSize < 0 || minBufferSize > MaxBufferSize) { throw new ArgumentOutOfRangeException(nameof(minBufferSize)); }
            int idx = _availableBuffers.Reader.ReadAsync().Result;
            return new UnmanagedMemoryManager(this, idx, _buffers[idx], MaxBufferSize);
        }

        public async Task<UnmanagedMemoryManager> RentManagerAsync(int minBufferSize = -1)
        {
            if (minBufferSize == -1) { minBufferSize = MaxBufferSize; }
            if (minBufferSize < 0 || minBufferSize > MaxBufferSize) { throw new ArgumentOutOfRangeException(nameof(minBufferSize)); }
            int idx = await _availableBuffers.Reader.ReadAsync().ConfigureAwait(false);
            return new UnmanagedMemoryManager(this, idx, _buffers[idx], MaxBufferSize);
        }
        public UnmanagedMemoryPool(int bufferSize, int size)
        {
            if (bufferSize <= 0) { throw new ArgumentOutOfRangeException(nameof(bufferSize)); }
            if (size <= 0) { throw new ArgumentOutOfRangeException(nameof(size)); }
            MaxBufferSize = bufferSize;
            _buffers = new IntPtr[size];
            _rentedManagers = new UnmanagedMemoryManager?[size];
            _availableBuffers = Channel.CreateBounded<int>(new BoundedChannelOptions(size)
            {
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = true,
                SingleReader = false,
                SingleWriter = false,
            });
            for (int i = 0; i < size; i++)
            {
                _buffers[i] = Marshal.AllocCoTaskMem(bufferSize);
                _availableBuffers.Writer.TryWrite(i);
            }

        }
        protected override void Dispose(bool disposing)
        {
            foreach (var man in _rentedManagers)
            {
                (man as IDisposable)?.Dispose();
            }
            foreach (var buf in _buffers)
            {
                Marshal.FreeCoTaskMem(buf);
            }
            _availableBuffers.Writer.Complete();
        }
    }
}
