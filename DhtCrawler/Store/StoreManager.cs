using System;
using System.Collections.Generic;
using System.IO;

namespace DhtCrawler.Store
{
    public class StoreManager<T> : IDisposable where T : IStoreEntity, new()
    {
        private const uint IntSize = 4;
        private const uint LongSize = 8;
        private readonly BinaryWriter writer;
        private readonly BinaryReader reader;
        private long _readIndex;
        private long _writeIndex;
        private long _count;
        public StoreManager(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.OpenOrCreate);
            writer = new BinaryWriter(fileStream);
            reader = new BinaryReader(fileStream);
            if (fileStream.Length <= 0)
            {
                _readIndex = _writeIndex = 24;
                writer.Write(_count);
                writer.Write(_readIndex);
                writer.Write(_writeIndex);
            }
            else
            {
                _count = reader.ReadInt64();
                _readIndex = reader.ReadInt64();
                _writeIndex = reader.ReadInt64();
            }
        }


        private void SetIndex(long wIndex, long rIndex)
        {
            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            writer.Write(_count);
            writer.Write(rIndex);
            writer.Write(wIndex);
            this._writeIndex = wIndex;
            this._readIndex = rIndex;
            writer.Flush();
        }

        public long Count
        {
            get
            {
                lock (this)
                {
                    return _count;
                }
            }
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

        public bool CanRead => this._writeIndex != 24;

        public void Add(IStoreEntity item)
        {
            var byteArray = item.ToBytes();
            lock (this)
            {
                _count++;
                writer.BaseStream.Seek(_writeIndex, SeekOrigin.Begin);
                writer.Write(_readIndex);
                writer.Write(byteArray.Length);
                writer.Write(byteArray);
                #region 设置文件头
                long wIndex = Position, rIndex = wIndex - LongSize - IntSize - byteArray.Length;
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
                _count--;
                reader.BaseStream.Seek(_readIndex, SeekOrigin.Begin);
                var preReadIndex = reader.ReadInt64();
                var length = reader.ReadInt32();
                var byteArray = new byte[length];
                var index = 0;
                while (index < length)
                {
                    index += reader.Read(byteArray, index, length);
                }

                #region 设置文件头

                long rIndex = preReadIndex, wIndex = Position - LongSize - IntSize - byteArray.Length;
                SetIndex(wIndex, rIndex);

                #endregion

                var result = new T();
                result.ReadBytes(byteArray);
                return result;
            }
        }

        public IList<T> ReadLast(int num)
        {
            lock (this)
            {
                if (!CanRead)
                    return new T[0];
                var items = new List<T>();
                for (int i = 0; i < num && CanRead; i++)
                {
                    _count--;
                    reader.BaseStream.Seek(_readIndex, SeekOrigin.Begin);
                    var preReadIndex = reader.ReadInt64();
                    var length = reader.ReadInt32();
                    var byteArray = new byte[length];
                    var index = 0;
                    while (index < length)
                    {
                        index += reader.Read(byteArray, index, length);
                    }

                    #region 设置文件头

                    long rIndex = preReadIndex, wIndex = Position - LongSize - IntSize - byteArray.Length;
                    SetIndex(wIndex, rIndex);

                    #endregion

                    var result = new T();
                    result.ReadBytes(byteArray);
                    items.Add(result);
                }
                return items;
            }
        }

        public void Dispose()
        {
            writer?.Dispose();
            reader?.Dispose();
        }
    }
}
