using System.IO;
using System.Text;

namespace DhtCrawler.Common.IO
{
    public class BufferArrayStream : Stream
    {
        private Stream _innerStream;
        private MemoryStream _content;
        public BufferArrayStream(Stream innerStream)
        {
            _innerStream = innerStream;
            _content = new MemoryStream();
        }


        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
            _content.Write(buffer, offset, count);
        }

        public byte[] GetBufferContent()
        {
            return _content.ToArray();
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => true;
        public override long Length => _innerStream.Length;

        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            _innerStream.Dispose();
            _content.Dispose();
        }
    }
}
