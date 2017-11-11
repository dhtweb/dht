using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using DhtCrawler.Common.Compare;
using DhtCrawler.Utils;
using log4net;

namespace DhtCrawler.DHT.Message
{
    public class MessageMap
    {
        private class MapInfo
        {
            public byte[] InfoHash { get; set; }
            public DateTime LastTime { get; set; }
        }

        private class IdMapInfo
        {
            public TransactionId TransactionId { get; set; }
            public int Count { get; set; }
        }
        private static readonly object SyncRoot = new object();
        private static readonly ILog log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private static readonly BlockingCollection<TransactionId> Bucket = new BlockingCollection<TransactionId>();
        private static readonly ConcurrentDictionary<TransactionId, MapInfo> MappingInfo = new ConcurrentDictionary<TransactionId, MapInfo>();

        private static readonly IEqualityComparer<byte[]> ByteArrayComparer = new WrapperEqualityComparer<byte[]>((x, y) => x.Length == y.Length && x.SequenceEqual(y), x => x.Sum(b => b));
        private static readonly ConcurrentDictionary<byte[], IdMapInfo> IdMappingInfo = new ConcurrentDictionary<byte[], IdMapInfo>(ByteArrayComparer);
        private static readonly IDictionary<CommandType, TransactionId> TypeMapTransactionId;
        private static readonly IDictionary<TransactionId, CommandType> TransactionIdMapType;
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
            for (int i = 0; i <= byte.MaxValue; i++)
            {
                id[0] = (byte)i;
                for (int j = 0; j <= byte.MaxValue; j++)
                {
                    id[1] = (byte)j;
                    if (TransactionIdMapType.ContainsKey(id))
                        continue;
                    Bucket.Add(id.CopyArray());
                }
            }
        }

        private static void ClearExpireMessage()
        {
            var removeItems = new HashSet<byte[]>(ByteArrayComparer);
            foreach (var item in MappingInfo)
            {
                var tuple = item.Value;
                if ((DateTime.Now - tuple.LastTime).TotalSeconds > 60)
                {
                    MappingInfo.TryRemove(item.Key, out var rm);
                    removeItems.Add(rm.InfoHash);
                    Bucket.Add(item.Key);
                }
                foreach (var mapInfo in IdMappingInfo)
                {
                    if (mapInfo.Value.Count <= 0 || removeItems.Contains(mapInfo.Key))
                        IdMappingInfo.TryRemove(mapInfo.Key, out var rm);
                }
            }
            log.Info($"清理过期的命令ID,清理后可用命令ID数：{Bucket.Count}");
        }

        public static bool RegisterMessage(DhtMessage message)
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
            TransactionId messageId;
            var infoHash = message.Get<byte[]>("info_hash");
            lock (SyncRoot)
            {
                if (IdMappingInfo.TryGetValue(infoHash, out var idMap))
                {
                    idMap.Count++;
                    messageId = idMap.TransactionId;
                    if (MappingInfo.TryGetValue(messageId, out var info))
                    {
                        info.LastTime = DateTime.Now;
                    }
                }
                else
                {
                    var start = DateTime.Now;
                    while (!Bucket.TryTake(out messageId, 1000))
                    {
                        if (Bucket.TryTake(out messageId))
                            break;
                        if ((DateTime.Now - start).TotalSeconds > 10)//10秒内获取不到就丢弃
                        {
                            return false;
                        }
                        ClearExpireMessage();
                    }
                    if (!MappingInfo.TryAdd(messageId, new MapInfo()
                    {
                        LastTime = DateTime.Now,
                        InfoHash = infoHash
                    }))
                    {
                        return false;
                    }
                    IdMappingInfo.TryAdd(infoHash, new IdMapInfo() { Count = 1, TransactionId = messageId });
                }
            }
            message.MessageId = messageId;
            return true;
        }

        public static bool RequireRegisteredInfo(DhtMessage message)
        {
            if (TransactionIdMapType.ContainsKey(message.MessageId))
            {
                message.CommandType = TransactionIdMapType[message.MessageId];
                return true;
            }
            message.CommandType = CommandType.Get_Peers;
            MapInfo mapInfo;
            lock (SyncRoot)
            {
                if (MappingInfo.TryGetValue(message.MessageId, out mapInfo))
                {

                    if (IdMappingInfo.TryGetValue(mapInfo.InfoHash, out var idMap))
                    {
                        idMap.Count--;
                        if (idMap.Count <= 0)
                        {
                            IdMappingInfo.TryRemove(mapInfo.InfoHash, out var rm);
                            if (MappingInfo.TryRemove(message.MessageId, out var obj))
                                Bucket.Add(message.MessageId);
                        }
                    }
                    else
                    {
                        if (MappingInfo.TryRemove(message.MessageId, out var obj))
                            Bucket.Add(message.MessageId);
                    }
                }
            }
            if (mapInfo?.InfoHash == null)
            {
                return false;
            }
            message.Data.Add("info_hash", mapInfo.InfoHash);
            return true;
        }
    }
}
