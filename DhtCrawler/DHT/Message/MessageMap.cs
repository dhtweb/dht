using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using DhtCrawler.Common.Compare;
using DhtCrawler.Common.Utils;
using log4net;

namespace DhtCrawler.DHT.Message
{
    public class MessageMap
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
        private static readonly long ExpireTimeSpan = TimeSpan.FromMinutes(30).Ticks;
        private static readonly object SyncRoot = new object();
        private static readonly ILog log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private static readonly IDictionary<CommandType, TransactionId> TypeMapTransactionId;
        private static readonly IDictionary<TransactionId, CommandType> TransactionIdMapType;
        private static readonly IEqualityComparer<byte[]> ByteArrayComparer = new WrapperEqualityComparer<byte[]>((x, y) => x.Length == y.Length && x.SequenceEqual(y), x => x.Sum(b => b));
        private static readonly ConcurrentDictionary<byte[], NodeMapInfo> NodeMap = new ConcurrentDictionary<byte[], NodeMapInfo>(ByteArrayComparer);
        private static TransactionId[] BucketArray;
        private static int BucketIndex = 0;
        private static DateTime LastClearTime = DateTime.Now;
        static MessageMap()
        {
            TypeMapTransactionId = new ReadOnlyDictionary<CommandType, TransactionId>(new Dictionary<CommandType, TransactionId>(3)
            {
                { CommandType.Ping, TransactionId.Ping },
                { CommandType.Find_Node, TransactionId.FindNode },
                { CommandType.Announce_Peer, TransactionId.AnnouncePeer },
            });
            TransactionIdMapType = new ReadOnlyDictionary<TransactionId, CommandType>(new Dictionary<TransactionId, CommandType>(3)
            {
                {  TransactionId.Ping,CommandType.Ping },
                {  TransactionId.FindNode,CommandType.Find_Node },
                {  TransactionId.AnnouncePeer,CommandType.Announce_Peer },
            });
            InitBucket();
        }


        private static void InitBucket()
        {
            var id = new byte[2];
            var list = new LinkedList<TransactionId>();
            for (int i = 0; i <= byte.MaxValue; i++)
            {
                id[0] = (byte)i;
                for (int j = 0; j <= byte.MaxValue; j++)
                {
                    id[1] = (byte)j;
                    if (TransactionIdMapType.ContainsKey(id))
                        continue;
                    list.AddLast(id.CopyArray());
                }
            }
            BucketArray = list.ToArray();
        }

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
            log.Info($"清理过期的注册消息数:{removeSize},现有消息数:{msgSize},用时:{(DateTime.Now - startTime).TotalSeconds}");
        }

        public static bool RegisterMessage(DhtMessage message, DhtNode node)
        {
            switch (message.CommandType)
            {
                case CommandType.UnKnow:
                    return false;
                case CommandType.Ping:
                case CommandType.Find_Node:
                case CommandType.Announce_Peer:
                    message.MessageId = TypeMapTransactionId[message.CommandType];
                    return true;
                case CommandType.Get_Peers:
                    break;
                default:
                    return false;
            }
            var nodeKey = node.CompactEndPoint();
            var nodeMapInfo = NodeMap.GetOrAdd(nodeKey, new NodeMapInfo());
            if (nodeMapInfo.IsExpire)
            {
                NodeMap.TryRemove(nodeKey, out nodeMapInfo);
                return false;
            }
            if ((DateTime.Now - LastClearTime).Ticks > ExpireTimeSpan)
            {
                lock (SyncRoot)
                {
                    if ((DateTime.Now - LastClearTime).Ticks > ExpireTimeSpan)
                    {
                        ClearExpireMessage();
                        LastClearTime = DateTime.Now;
                    }
                }
            }
            var tryTimes = 0;
            var infoHash = message.Get<byte[]>("info_hash");
            var mapItem = new NodeMapInfoItem() { InfoHash = infoHash };
            while (true)
            {
                if (tryTimes > 10)
                    break;
                lock (BucketArray)
                {
                    BucketIndex++;
                    if (BucketIndex >= BucketArray.Length)
                        BucketIndex = 0;
                }
                var msgId = BucketArray[BucketIndex];
                if (nodeMapInfo.InfoHashMap.TryAdd(msgId, mapItem))
                {
                    message.MessageId = msgId;
                    return true;
                }
                tryTimes++;
            }
            return false;
        }

        public static bool RequireRegisteredInfo(DhtMessage message, DhtNode node)
        {
            if (TransactionIdMapType.ContainsKey(message.MessageId))
            {
                message.CommandType = TransactionIdMapType[message.MessageId];
                return true;
            }
            message.CommandType = CommandType.Get_Peers;
            var msgId = message.MessageId;
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
                message.Data.Add("info_hash", infohash.InfoHash);
                Console.WriteLine(NodeMap.Count + ",host msgsize:" + nodeMap.InfoHashMap.Count);
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
