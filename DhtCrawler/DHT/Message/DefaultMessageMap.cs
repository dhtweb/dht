using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DhtCrawler.Common.Compare;
using DhtCrawler.Common.Utils;

namespace DhtCrawler.DHT.Message
{
    public class DefaultMessageMap : AbstractMessageMap
    {
        private class NodeMapInfo
        {
            public long LastTime { private get; set; } = DateTime.Now.Ticks;
            public ConcurrentDictionary<TransactionId, NodeMapInfoItem> InfoHashMap { get; } = new ConcurrentDictionary<TransactionId, NodeMapInfoItem>();
            public bool IsExpire(long expireTimeSpan)
            {
                return LastTime + expireTimeSpan < DateTime.Now.Ticks;
            }

            public int RemoveExpire(long expireTimeSpan)
            {
                var size = 0;
                foreach (var item in InfoHashMap)
                {
                    if (!item.Value.IsExpire(expireTimeSpan))
                        continue;
                    if (InfoHashMap.TryRemove(item.Key, out var rm))
                        size++;
                }
                return size;
            }
        }
        private class NodeMapInfoItem
        {
            public byte[] InfoHash { get; set; }
            public long LastTime { private get; set; }
            public bool IsExpire(long expireTimeSpan)
            {
                return LastTime + expireTimeSpan < DateTime.Now.Ticks;
            }
        }
        public static readonly DefaultMessageMap Instance = new DefaultMessageMap(TimeSpan.FromMinutes(20).Ticks);

        private readonly long _expireTimeSpan;
        private static readonly ConcurrentDictionary<long, NodeMapInfo> NodeMap = new ConcurrentDictionary<long, NodeMapInfo>();
        private static readonly ConcurrentDictionary<byte[], HashSet<long>> InfoHashPeers = new ConcurrentDictionary<byte[], HashSet<long>>(new WrapperEqualityComparer<byte[]>((bytes1, bytes2) => bytes1.Length == bytes2.Length && bytes1.SequenceEqual(bytes2), bytes => bytes[0] << 24 | bytes[5] << 16 | bytes[10] << 8 | bytes[15]));

        private int _bucketIndex = 0;
        private DateTime _lastClearTime;
        private readonly object _syncRoot = new object();

        public DefaultMessageMap(long expireTimeSpan)
        {
            _expireTimeSpan = expireTimeSpan;
            _lastClearTime = DateTime.Now.AddTicks(_expireTimeSpan);
        }
        private void ClearExpireMessage()
        {
            var startTime = DateTime.Now;
            int removeSize = 0, msgSize = 0;
            foreach (var mapInfo in NodeMap)
            {
                if (mapInfo.Value.InfoHashMap.Count <= 0 || mapInfo.Value.IsExpire(_expireTimeSpan))
                {
                    NodeMap.TryRemove(mapInfo.Key, out var rm);
                    removeSize += rm.InfoHashMap.Count;
                }
                else
                {
                    removeSize += mapInfo.Value.RemoveExpire(_expireTimeSpan);
                    msgSize += mapInfo.Value.InfoHashMap.Count;
                }
            }
            InfoHashPeers.Clear();
            Log.Info($"清理过期的注册消息数:{removeSize},现有消息数:{msgSize},用时:{(DateTime.Now - startTime).TotalSeconds}");
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var sendPeers = InfoHashPeers.GetOrAdd(infoHash, new HashSet<long>());
            var nodeKey = node.CompactEndPoint().ToInt64();
            lock (sendPeers)
            {
                if (sendPeers.Contains(nodeKey))
                {
                    msgId = null;
                    return false;
                }
                sendPeers.Add(nodeKey);
            }
            var nodeMapInfo = NodeMap.GetOrAdd(nodeKey, new NodeMapInfo());
            if (nodeMapInfo.IsExpire(_expireTimeSpan))
            {
                NodeMap.TryRemove(nodeKey, out nodeMapInfo);
                msgId = null;
                lock (sendPeers)
                {
                    sendPeers.Remove(nodeKey);
                }
                return false;
            }
            if (DateTime.Now >= _lastClearTime)
            {
                lock (_syncRoot)
                {
                    if (DateTime.Now >= _lastClearTime)
                    {
                        ClearExpireMessage();
                        _lastClearTime = DateTime.Now.AddTicks(_expireTimeSpan);
                    }
                }
            }
            var mapItem = new NodeMapInfoItem() { InfoHash = infoHash, LastTime = DateTime.Now.Ticks };
            lock (_bucketArray)
            {
                _bucketIndex++;
                if (_bucketIndex >= _bucketArray.Length)
                    _bucketIndex = 0;
                msgId = _bucketArray[_bucketIndex];
            }
            if (nodeMapInfo.InfoHashMap.TryAdd(msgId, mapItem))
            {
                return true;
            }
            lock (sendPeers)
            {
                sendPeers.Remove(nodeKey);
            }
            msgId = null;
            return false;
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            infoHash = null;
            var nodeKey = node.CompactEndPoint().ToInt64();
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
                    NodeMap.AddOrUpdate(nodeKey, rm, (key, old) =>
                    {
                        foreach (var item in rm.InfoHashMap)
                        {
                            old.InfoHashMap.TryAdd(item.Key, item.Value);
                        }
                        return old;
                    });
                }
            }
            return result;
        }

    }
}
