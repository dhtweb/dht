using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DhtCrawler.Common.Compare;

namespace DhtCrawler.DHT.Message
{
    public class DefaultMessageMap : AbstractMessageMap
    {
        private class NodeMapInfo
        {
            public long LastTime { get; set; } = DateTime.Now.Ticks;
            public bool IsExpire => LastTime + ExpireTimeSpan < DateTime.Now.Ticks;
            public ConcurrentDictionary<TransactionId, NodeMapInfoItem> InfoHashMap { get; } = new ConcurrentDictionary<TransactionId, NodeMapInfoItem>();
        }
        private class NodeMapInfoItem
        {
            public byte[] InfoHash { get; set; }
            public long LastTime { get; } = DateTime.Now.Ticks;
            public bool IsExpire => LastTime + ExpireTimeSpan < DateTime.Now.Ticks;
        }
        public static readonly DefaultMessageMap Instance = new DefaultMessageMap();

        private static readonly long ExpireTimeSpan = TimeSpan.FromMinutes(30).Ticks;
        private static readonly IEqualityComparer<byte[]> ByteArrayComparer = new WrapperEqualityComparer<byte[]>((x, y) => x.Length == y.Length && x.SequenceEqual(y), x => x.Sum(b => b));
        private static readonly ConcurrentDictionary<byte[], NodeMapInfo> NodeMap = new ConcurrentDictionary<byte[], NodeMapInfo>(ByteArrayComparer);

        private int _bucketIndex = 0;
        private DateTime _lastClearTime = DateTime.Now;
        private readonly object _syncRoot = new object();

        private static void ClearExpireMessage()
        {
            var startTime = DateTime.Now;
            int removeSize = 0, msgSize = 0;
            foreach (var mapInfo in NodeMap)
            {
                if (mapInfo.Value.InfoHashMap.Count <= 0 || mapInfo.Value.IsExpire)
                {
                    NodeMap.TryRemove(mapInfo.Key, out var rm);
                    removeSize += rm.InfoHashMap.Count;
                }
                else
                {
                    foreach (var infoItem in mapInfo.Value.InfoHashMap)
                    {
                        if (!infoItem.Value.IsExpire)
                            continue;
                        mapInfo.Value.InfoHashMap.TryRemove(infoItem.Key, out var rmItem);
                        removeSize += 1;
                    }
                    msgSize += mapInfo.Value.InfoHashMap.Count;
                }

            }
            GC.Collect();
            Log.Info($"清理过期的注册消息数:{removeSize},现有消息数:{msgSize},用时:{(DateTime.Now - startTime).TotalSeconds}");
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeKey = node.CompactEndPoint();
            var nodeMapInfo = NodeMap.GetOrAdd(nodeKey, new NodeMapInfo());
            if (nodeMapInfo.IsExpire)
            {
                NodeMap.TryRemove(nodeKey, out nodeMapInfo);
                msgId = null;
                return false;
            }
            if ((DateTime.Now - _lastClearTime).Ticks > ExpireTimeSpan)
            {
                lock (_syncRoot)
                {
                    if ((DateTime.Now - _lastClearTime).Ticks > ExpireTimeSpan)
                    {
                        ClearExpireMessage();
                        _lastClearTime = DateTime.Now;
                    }
                }
            }
            var tryTimes = 0;
            var mapItem = new NodeMapInfoItem() { InfoHash = infoHash };
            while (true)
            {
                if (tryTimes > 10)
                    break;
                lock (_bucketArray)
                {
                    _bucketIndex++;
                    if (_bucketIndex >= _bucketArray.Length)
                        _bucketIndex = 0;
                }
                msgId = _bucketArray[_bucketIndex];
                if (nodeMapInfo.InfoHashMap.TryAdd(msgId, mapItem))
                {
                    return true;
                }
                tryTimes++;
            }
            msgId = null;
            return false;
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            infoHash = null;
            var nodeKey = node.CompactEndPoint();
            if (!NodeMap.TryGetValue(nodeKey, out var nodeMap))
            {
                return false;
            }
            var result = false;
            nodeMap.LastTime = DateTime.Now.Ticks;
            if (nodeMap.InfoHashMap.TryRemove(msgId, out var infohash))
            {
                if (infohash?.InfoHash == null)
                {
                    return false;
                }
                infoHash = infohash.InfoHash;
                result = true;
            }
            if (nodeMap.InfoHashMap.Count <= 0)
            {
                if (NodeMap.TryRemove(nodeKey, out var rm) && rm.InfoHashMap.Count > 0)
                {
                    NodeMap.TryAdd(nodeKey, rm);
                }
            }
            return result;
        }
    }
}
