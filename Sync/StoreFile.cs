using System;
using System.IO;
using System.Text;

namespace Sync
{
    class StoreFile : IDisposable
    {
        private readonly FileStream _stream;
        private long _writeIndex;
        private long _readIndex;

        public long Index { get; private set; }
        public bool CanRead => _readIndex < _writeIndex;

        public StoreFile(string file)
        {
            _stream = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            Init();
        }

        private void Init()
        {
            if (_stream.Length < 16)
            {
                var indexBytes = BitConverter.GetBytes(0L);
                _stream.Write(indexBytes, 0, 0);
                _stream.Write(indexBytes, 0, 0);
                _writeIndex = 16;
            }
            else
            {
                _stream.Seek(0, SeekOrigin.Begin);
                var bytes = new byte[8];
                _stream.Read(bytes, 0, 8);
                Index = BitConverter.ToInt64(bytes, 0);
                _stream.Read(bytes, 0, 8);
                _writeIndex = BitConverter.ToInt64(bytes, 0);
            }
            _stream.Seek(_writeIndex, SeekOrigin.Begin);
            _readIndex = 16;
        }

        private void Append(byte[] bytes)
        {
            _stream.Seek(_writeIndex, SeekOrigin.Begin);
            _stream.Write(bytes, 0, bytes.Length);
            _writeIndex += bytes.Length;
        }

        private void Write(int index, long num)
        {
            _stream.Seek(index, SeekOrigin.Begin);
            var bytes = BitConverter.GetBytes(num);
            _stream.Write(bytes, 0, bytes.Length);
        }


        public void Append(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }
            var bytes = Encoding.UTF8.GetBytes(data);
            Append(bytes.Length);
            Append(bytes);
        }

        public void Append(int num)
        {
            var bytes = BitConverter.GetBytes(num);
            Append(bytes);
        }

        public string ReadString()
        {
            _stream.Seek(_readIndex, SeekOrigin.Begin);
            var lengthBytes = new byte[4];
            _stream.Read(lengthBytes, 0, 4);
            var strLength = BitConverter.ToInt32(lengthBytes, 0);
            var strBytes = new byte[strLength];
            _stream.Read(strBytes, 0, strLength);
            _readIndex = _stream.Position;
            return Encoding.UTF8.GetString(strBytes);
        }

        public void SetIndex(long index)
        {
            Write(0, index);
            Index = index;
        }

        public void Commit()
        {
            Write(8, _writeIndex);
            _stream.Flush(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }
            _stream?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
