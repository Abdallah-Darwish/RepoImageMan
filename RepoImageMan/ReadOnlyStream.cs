using System;
using System.IO;

namespace RepoImageMan
{
    public class ReadOnlyStream : Stream
    {
        private const string ErrorMessage = "THIS IS A READONLY STREAM";
        private readonly Stream _baseStream;

        public ReadOnlyStream(Stream baseStream)
        {
            if (baseStream?.CanRead != true)
            {
                throw new ArgumentException("The base stream must support reading.", nameof(baseStream));
            }

            _baseStream = baseStream;
        }

        public override void Flush() => throw new InvalidOperationException(ErrorMessage);

        public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

        public override void SetLength(long value) => throw new InvalidOperationException(ErrorMessage);

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException(ErrorMessage);

        public override bool CanRead => true;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _baseStream.Dispose();
        }
    }
}