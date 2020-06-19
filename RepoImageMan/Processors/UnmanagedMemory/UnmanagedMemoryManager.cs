using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace RepoImageMan.Processors.UnmanagedMemory
{
    class UnmanagedMemoryManager : MemoryManager<byte>
    {
        public IntPtr Address { get; private set; }
        public int Length { get; private set; }
        private readonly UnmanagedMemoryPool _owner;
        private readonly int _idx;


        public UnmanagedMemoryManager(UnmanagedMemoryPool owner, int idx, IntPtr ptr, int len)
        {

            Length = len;
            Address = ptr;
            _idx = idx;
            _owner = owner;
        }

        protected override void Dispose(bool disposing)
        {
            _owner.Return(_idx);
            Address = IntPtr.Zero;
            Length = 0;
        }

        public override Span<byte> GetSpan()
        {
            unsafe
            {
                return new Span<byte>(Address.ToPointer(), Length);
            }
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            unsafe
            {
                return new MemoryHandle((void*)(Address.ToInt64() + elementIndex));
            }
        }

        public override void Unpin() { }

    }
}
