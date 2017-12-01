using System;
using System.IO;
using STSdb4.Database;

namespace DhtCrawler.DHT.Message
{
    public class StsdbMessageMap : AbstractMessageMap, IDisposable
    {
        private class Record
        {
            public byte[] InfoHash { get; set; }
            public DateTime AddTime { get; set; }
        }
        private readonly IStorageEngine _storageEngine;
        private readonly ITable<byte[], Record> _table;
        private readonly object _syncRoot = new object();
        private int _bucketIndex;
        private int _operateSize;
        public StsdbMessageMap(string storePath)
        {
            var fileInfo = new FileInfo(storePath);
            if (!fileInfo.Directory.Exists)
            {
                fileInfo.Directory.Create();
            }
            _storageEngine = STSdb.FromStream(fileInfo.Open(FileMode.OpenOrCreate));
            _table = _storageEngine.OpenXTable<byte[], Record>("dhtMsg");
        }
        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeKey = node.CompactEndPoint();
            var key = new byte[nodeKey.Length + 2];
            Array.Copy(nodeKey, 0, key, 2, nodeKey.Length);
            var tryTimes = 0;
            while (true)
            {
                if (tryTimes > 10)
                    break;
                lock (this)
                {
                    _bucketIndex++;
                    if (_bucketIndex >= _bucketArray.Length)
                        _bucketIndex = 0;
                }
                msgId = _bucketArray[_bucketIndex];
                key[0] = msgId[0];
                key[1] = msgId[1];
                lock (_syncRoot)
                {
                    var old = _table.Find(msgId);
                    if (old == null || old.AddTime.AddMinutes(30) < DateTime.Now)
                    {
                        _operateSize++;
                        _table[key] = new Record() { InfoHash = infoHash, AddTime = DateTime.Now };
                        if (_operateSize > 10000)
                        {
                            _storageEngine.Commit();
                            _operateSize = 0;
                        }
                        return true;
                    }
                }
                tryTimes++;
            }
            msgId = null;
            return false;
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            var nodeKey = node.CompactEndPoint();
            var key = new byte[nodeKey.Length + 2];
            Array.Copy(nodeKey, 0, key, 2, nodeKey.Length);
            key[0] = msgId[0];
            key[1] = msgId[1];
            lock (_syncRoot)
            {
                var old = _table.Find(key);
                if (old == null)
                {
                    infoHash = null;
                    return false;
                }
                infoHash = old.InfoHash;
                _operateSize++;
                _table.Delete(key);
                if (_operateSize > 10000)
                {
                    _storageEngine.Commit();
                    _operateSize = 0;
                }
                return true;
            }
        }

        public void Dispose()
        {
            _storageEngine.Commit();
            _storageEngine?.Dispose();
        }
    }
}
