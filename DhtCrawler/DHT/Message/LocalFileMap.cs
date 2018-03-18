using System;
using System.IO;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;
using DhtCrawler.Common.Utils;

namespace DhtCrawler.DHT.Message
{
    public class LocalFileMap : AbstractMessageMap
    {
        public static readonly LocalFileMap Default = new LocalFileMap("msgs");
        private int _index;
        private BPlusTree<ulong, byte[]> _treeStore;
        private long _lastClearTime;
        private readonly long _clearDuration = TimeSpan.FromMinutes(30).Ticks;
        public LocalFileMap(string storePath)
        {
            var dirPath = Path.GetDirectoryName(storePath);
            Directory.CreateDirectory(dirPath);
            var treeOption = new BPlusTree<ulong, byte[]>.OptionsV2(PrimitiveSerializer.UInt64, PrimitiveSerializer.Bytes) { FileName = storePath, CreateFile = CreatePolicy.Always };
            _treeStore = new BPlusTree<ulong, byte[]>(treeOption);
            _lastClearTime = DateTime.Now.Ticks;
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeId = (ulong)node.CompactEndPoint().ToInt64();
            lock (this)
            {
                _index++;
                if (_index >= _bucketArray.Length)
                    _index = 0;
            }
            var nowTick = DateTime.Now.Ticks;
            if (nowTick - _lastClearTime >= _clearDuration)
            {
                lock (this)
                {
                    if (nowTick - _lastClearTime >= _clearDuration)
                    {
                        _treeStore.Clear();
                        _lastClearTime = nowTick;
                    }
                }
            }
            msgId = _bucketArray[_index];
            var path = nodeId << 16 | (uint)(msgId[0] << 8) | msgId[1];
            return _treeStore.TryAdd(path, infoHash);
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            var nodeId = (ulong)node.CompactEndPoint().ToInt64();
            var path = nodeId << 16 | (uint)(msgId[0] << 8) | msgId[1];
            return _treeStore.TryGetValue(path, out infoHash);
        }
    }
}
