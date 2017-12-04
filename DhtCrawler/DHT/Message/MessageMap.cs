using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DhtCrawler.Common.Compare;
using DhtCrawler.Common.Utils;

namespace DhtCrawler.DHT.Message
{
    public class MessageMap : AbstractMessageMap
    {
        private class MapInfo
        {
            public byte[] InfoHash { get; set; }
            public DateTime LastTime { get; set; }
        }
        private class IdMapInfo
        {
            private ISet<long> set;
            public TransactionId TransactionId { get; set; }
            public int Count
            {
                get
                {
                    lock (this)
                    {
                        return set.Count;
                    }
                }
            }

            public IdMapInfo()
            {
                set = new HashSet<long>();
            }

            public bool Add(long key)
            {
                lock (this)
                {
                    return set.Add(key);
                }
            }

            public void Remove(long key)
            {
                lock (this)
                {
                    set.Remove(key);
                }
            }
        }
        private static readonly IEqualityComparer<byte[]> ByteArrayComparer = new WrapperEqualityComparer<byte[]>((x, y) => x.Length == y.Length && x.SequenceEqual(y), x => x.Sum(b => b));

        private readonly ConcurrentDictionary<TransactionId, MapInfo> MappingInfo = new ConcurrentDictionary<TransactionId, MapInfo>();
        private readonly ConcurrentDictionary<byte[], IdMapInfo> IdMappingInfo = new ConcurrentDictionary<byte[], IdMapInfo>(ByteArrayComparer);
        private int _index;

        private void ClearExpireMessage(int batchSize)
        {
            var startTime = DateTime.Now;
            var removeItems = new HashSet<byte[]>(ByteArrayComparer);
            var snapshotMapInfo = MappingInfo.ToArray();
            Array.Sort(snapshotMapInfo, new WrapperComparer<KeyValuePair<TransactionId, MapInfo>>((v1, v2) => (int)(v2.Value.LastTime.Ticks - v1.Value.LastTime.Ticks)));
            foreach (var item in snapshotMapInfo)
            {
                MappingInfo.TryRemove(item.Key, out var rm);
                removeItems.Add(rm.InfoHash);
                if (removeItems.Count > batchSize)
                {
                    break;
                }
            }
            var snapshotIdInfo = IdMappingInfo.ToArray();
            foreach (var mapInfo in snapshotIdInfo)
            {
                if (mapInfo.Value.Count <= 0 || removeItems.Contains(mapInfo.Key))
                    IdMappingInfo.TryRemove(mapInfo.Key, out var rm);
            }
            Log.Info($"清理过期的命令ID数:{batchSize},用时:{(DateTime.Now - startTime).TotalSeconds}");
        }
        public static readonly MessageMap Instance = new MessageMap();
        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId messageId)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            var tryTimes = 0;
            while (true)
            {
                tryTimes++;
                if (IdMappingInfo.TryGetValue(infoHash, out var idMap))
                {
                    if (!idMap.Add(nodeId))
                    {
                        messageId = null;
                        return false;
                    }
                    messageId = idMap.TransactionId;
                    var newMap = new MapInfo() { LastTime = DateTime.Now, InfoHash = infoHash };
                    var newVal = MappingInfo.AddOrUpdate(messageId, newMap, (id, info) =>
                          {
                              info.LastTime = DateTime.Now;
                              return info;
                          });
                    return newMap == newVal || ByteArrayComparer.Equals(newVal.InfoHash, infoHash);
                }
                TransactionId msgId;
                lock (this)
                {
                    _index++;
                    msgId = _bucketArray[_index];
                }
                if (MappingInfo.TryAdd(msgId, new MapInfo()
                {
                    LastTime = DateTime.Now,
                    InfoHash = infoHash
                }))
                {
                    messageId = msgId;
                    var newIdMap = new IdMapInfo() { TransactionId = messageId };
                    newIdMap.Add(nodeId);
                    return IdMappingInfo.TryAdd(infoHash, newIdMap);
                }
                if (MappingInfo.Count >= _bucketArray.Length)
                {
                    lock (this)
                    {
                        if (MappingInfo.Count >= _bucketArray.Length)
                            ClearExpireMessage(1000);
                    }
                }
                if (tryTimes > 3)
                {
                    messageId = null;
                    return false;
                }
            }
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            if (MappingInfo.TryGetValue(msgId, out var mapInfo))
            {
                infoHash = mapInfo.InfoHash;
                if (IdMappingInfo.TryGetValue(mapInfo.InfoHash, out var idMap))
                {
                    var nodeId = node.CompactEndPoint().ToInt64();
                    idMap.Remove(nodeId);
                    if (idMap.Count <= 0)
                    {
                        IdMappingInfo.TryRemove(mapInfo.InfoHash, out var rmIdMap);
                        MappingInfo.TryRemove(msgId, out var rmMap);
                    }
                }
                return true;
            }
            infoHash = null;
            return false;
        }
    }
}
