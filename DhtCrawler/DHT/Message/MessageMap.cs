﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DhtCrawler.Common.Compare;
using DhtCrawler.Common.Utils;
using log4net;

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
            private HashSet<long> peers;
            public TransactionId TransactionId { get; set; }
            public int Count
            {
                get
                {
                    lock (this)
                    {
                        return peers.Count;
                    }
                }
            }

            public IdMapInfo()
            {
                peers = new HashSet<long>();
            }

            public bool Add(long peer)
            {
                lock (this)
                    return peers.Add(peer);
            }

            public bool Remove(long peer)
            {
                lock (this)
                    return peers.Remove(peer);
            }
        }

        private static readonly ILog log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");

        private static readonly IEqualityComparer<byte[]> ByteArrayComparer =
            new WrapperEqualityComparer<byte[]>((x, y) => x.Length == y.Length && x.SequenceEqual(y),
                x => x.Sum(b => b));

        private readonly BlockingCollection<TransactionId> _bucket = new BlockingCollection<TransactionId>();

        private readonly ConcurrentDictionary<TransactionId, MapInfo> _mappingInfo =
            new ConcurrentDictionary<TransactionId, MapInfo>();

        private readonly ConcurrentDictionary<byte[], IdMapInfo> _idMappingInfo =
            new ConcurrentDictionary<byte[], IdMapInfo>(ByteArrayComparer);
        public static readonly MessageMap Default = new MessageMap();
        public MessageMap()
        {
            InitBucket();
        }

        private void InitBucket()
        {
            foreach (var transactionId in _bucketArray)
            {
                _bucket.Add(transactionId);
            }
        }

        private void ClearExpireMessage(int clearSize = 1000)
        {
            var startTime = DateTime.Now;
            var removeItems = new HashSet<byte[]>(ByteArrayComparer);
            var snapshotMapInfo = _mappingInfo.ToArray();
            Array.Sort(snapshotMapInfo,
                new WrapperComparer<KeyValuePair<TransactionId, MapInfo>>((v1, v2) =>
                    v1.Value.LastTime.CompareTo(v2.Value.LastTime)));
            foreach (var item in snapshotMapInfo)
            {
                var tuple = item.Value;
                if (!((DateTime.Now - tuple.LastTime).TotalSeconds > 600))
                    break;
                _mappingInfo.TryRemove(item.Key, out var rm);
                removeItems.Add(rm.InfoHash);
                _bucket.Add(item.Key);
                if (removeItems.Count >= clearSize)
                {
                    break;
                }
            }
            if (removeItems.Count > 0)
            {
                foreach (var mapInfo in _idMappingInfo)
                {
                    if (mapInfo.Value.Count <= 0 || removeItems.Contains(mapInfo.Key))
                        _idMappingInfo.TryRemove(mapInfo.Key, out var rm);
                }
            }
            log.Info($"清理过期的命令ID,清理后可用命令ID数:{_bucket.Count},用时:{(DateTime.Now - startTime).TotalSeconds}");
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            TransactionId messageId;
            if (_idMappingInfo.TryGetValue(infoHash, out var idMap))
            {
                if (!idMap.Add(nodeId))
                {
                    msgId = null;
                    return false;
                }
                messageId = idMap.TransactionId;
                if (_mappingInfo.TryGetValue(messageId, out var info))
                {
                    info.LastTime = DateTime.Now;
                }
            }
            else
            {
                msgId = null;
                var start = DateTime.Now;
                while (!_bucket.TryTake(out messageId, 1000))
                {
                    if ((DateTime.Now - start).TotalSeconds > 10) //10秒内获取不到就丢弃
                    {
                        return false;
                    }
                    ClearExpireMessage();
                }
                if (!_mappingInfo.TryAdd(messageId, new MapInfo()
                {
                    LastTime = DateTime.Now,
                    InfoHash = infoHash
                }))
                {
                    msgId = null;
                    return false;
                }
                idMap = new IdMapInfo() { TransactionId = messageId };
                idMap.Add(nodeId);
                if (_idMappingInfo.TryAdd(infoHash, idMap))
                {
                    msgId = messageId;
                    return true;
                }
                return false;
            }
            msgId = messageId;
            return true;
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            if (_mappingInfo.TryGetValue(msgId, out var mapInfo))
            {
                if (_idMappingInfo.TryGetValue(mapInfo.InfoHash, out var idMap))
                {
                    lock (idMap)
                    {
                        var nodeId = node.CompactEndPoint().ToInt64();
                        idMap.Remove(nodeId);
                        if (idMap.Count <= 0)
                        {
                            _idMappingInfo.TryRemove(mapInfo.InfoHash, out var rm);
                            if (_mappingInfo.TryRemove(msgId, out var obj))
                                _bucket.Add(msgId);
                        }
                    }
                }
                else
                {
                    if (_mappingInfo.TryRemove(msgId, out var obj))
                        _bucket.Add(msgId);
                }
            }
            if (mapInfo?.InfoHash == null)
            {
                infoHash = null;
                return false;
            }
            infoHash = mapInfo.InfoHash;
            return true;
        }
    }
}
