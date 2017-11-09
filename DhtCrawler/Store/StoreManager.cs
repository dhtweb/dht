using System;
using System.IO;

namespace DhtCrawler.Store
{
    public class StoreManager<T> : IDisposable where T : IStoreEntity, new()
    {
        private const uint DataByteSize = 4;
        private const uint IndexByteSize = 8;
        private BinaryWriter writer;
        private BinaryReader reader;
        private long readIndex;
        private long writeIndex;
        public StoreManager(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.OpenOrCreate);
            writer = new BinaryWriter(fileStream);
            reader = new BinaryReader(fileStream);
            if (fileStream.Length <= 0)
            {
                readIndex = writeIndex = 16;
                writer.Write(readIndex);
                writer.Write(writeIndex);
            }
            else
            {
                readIndex = reader.ReadInt64();
                writeIndex = reader.ReadInt64();
            }
        }


        private void SetIndex(long wIndex, long rIndex)
        {
            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            writer.Write(wIndex);
            writer.Write(rIndex);
            this.writeIndex = wIndex;
            this.readIndex = rIndex;
            writer.Flush();
        }

        public long Position
        {
            get
            {
                lock (this)
                {
                    return writer.BaseStream.Position;
                }
            }
        }

        public bool CanRead => this.writeIndex != 16;

        public void Add(IStoreEntity item)
        {
            var byteArray = item.ToBytes();
            lock (this)
            {
                writer.BaseStream.Seek(writeIndex, SeekOrigin.Begin);
                writer.Write(readIndex);
                writer.Write(byteArray.Length);
                writer.Write(byteArray);
                #region 设置文件头
                long wIndex = Position, rIndex = wIndex - IndexByteSize - DataByteSize - byteArray.Length;
                SetIndex(wIndex, rIndex);
                #endregion
            }
        }


        public T ReadLast()
        {
            lock (this)
            {
                if (!CanRead)
                    return default(T);
                reader.BaseStream.Seek(readIndex, SeekOrigin.Begin);
                var preReadIndex = reader.ReadInt64();
                var length = reader.ReadInt32();
                var byteArray = new byte[length];
                var index = 0;
                while (index < length)
                {
                    index += reader.Read(byteArray, index, length);
                }

                #region 设置文件头

                long rIndex = preReadIndex, wIndex = Position - IndexByteSize - DataByteSize - byteArray.Length;
                SetIndex(wIndex, rIndex);

                #endregion

                var result = new T();
                result.ReadBytes(byteArray);
                return result;
            }
        }

        public void Dispose()
        {
            writer?.Dispose();
            reader?.Dispose();
        }
    }
}
