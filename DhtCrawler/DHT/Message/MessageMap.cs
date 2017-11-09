using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using DhtCrawler.Utils;
using log4net;

namespace DhtCrawler.DHT.Message
{
    public class MessageMap
    {
        private class MapInfo
        {
            public CommandType Type { get; set; }
            public byte[] InfoHash { get; set; }
            public DateTime LastTime { get; set; }
        }

        private static readonly ILog log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private static readonly BlockingCollection<TransactionId> Bucket = new BlockingCollection<TransactionId>();
        private static readonly ConcurrentDictionary<TransactionId, MapInfo> MappingInfo = new ConcurrentDictionary<TransactionId, MapInfo>();
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
            foreach (var item in MappingInfo)
            {
                var tuple = item.Value;
                if ((DateTime.Now - tuple.LastTime).TotalSeconds > 60)
                {
                    MappingInfo.TryRemove(item.Key, out var rm);
                    Bucket.Add(item.Key);
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
            var start = DateTime.Now;
            while (!Bucket.TryTake(out messageId, 1000))
            {
                lock (Bucket)
                {
                    if (Bucket.TryTake(out messageId))
                        break;
                    if ((DateTime.Now - start).TotalSeconds > 10)//10秒内获取不到就丢弃
                    {
                        return false;
                    }
                    ClearExpireMessage();
                }
            }
            if (!MappingInfo.TryAdd(messageId, new MapInfo()
            {
                Type = CommandType.Get_Peers,
                LastTime = DateTime.Now,
                InfoHash = message.Get<byte[]>("info_hash")
            }))
            {
                return false;
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
            if (MappingInfo.TryRemove(message.MessageId, out var obj))
                Bucket.Add(message.MessageId);
            if (obj?.InfoHash == null)
            {
                return false;
            }
            message.Data.Add("info_hash", obj.InfoHash);
            return true;
        }
    }
}
