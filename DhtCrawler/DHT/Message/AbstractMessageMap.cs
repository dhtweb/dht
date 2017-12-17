using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DhtCrawler.Common.Utils;
using log4net;

namespace DhtCrawler.DHT.Message
{
    public abstract class AbstractMessageMap
    {
        protected static readonly ILog Log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private static readonly IDictionary<CommandType, TransactionId> TypeMapTransactionId;
        private static readonly IDictionary<TransactionId, CommandType> TransactionIdMapType;
        protected static TransactionId[] _bucketArray;

        static AbstractMessageMap()
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
            _bucketArray = list.ToArray();
        }

        protected abstract bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId);

        protected virtual Task<(bool IsOk, TransactionId MsgId)> RegisterGetPeersMessageAsync(byte[] infoHash, DhtNode node)
        {
            var result = RegisterGetPeersMessage(infoHash, node, out var msgId);
            return Task.FromResult((result, msgId));
        }

        protected abstract bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash);

        protected virtual Task<(bool IsOk, byte[] InfoHash)> RequireGetPeersRegisteredInfoAsync(TransactionId msgId, DhtNode node)
        {
            var result = RequireGetPeersRegisteredInfo(msgId, node, out var infoHash);
            return Task.FromResult((result, infoHash));
        }

        public virtual bool IsFull { get; } = false;

        public bool RegisterMessage(DhtMessage message, DhtNode node)
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
            if (RegisterGetPeersMessage(message.Get<byte[]>("info_hash"), node, out var msgId))
            {
                message.MessageId = msgId;
                return true;
            }
            return false;
        }

        public async Task<bool> RegisterMessageAsync(DhtMessage message, DhtNode node)
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
            var result = await RegisterGetPeersMessageAsync(message.Get<byte[]>("info_hash"), node);
            if (result.IsOk)
            {
                message.MessageId = result.MsgId;
                return true;
            }
            return false;
        }

        public bool RequireRegisteredInfo(DhtMessage message, DhtNode node)
        {
            if (TransactionIdMapType.ContainsKey(message.MessageId))
            {
                message.CommandType = TransactionIdMapType[message.MessageId];
                return true;
            }
            message.CommandType = CommandType.Get_Peers;
            if (RequireGetPeersRegisteredInfo(message.MessageId, node, out var infoHash))
            {
                message.Data["info_hash"] = infoHash;
                return true;
            }
            return false;
        }

        public async Task<bool> RequireRegisteredInfoAsync(DhtMessage message, DhtNode node)
        {
            if (TransactionIdMapType.ContainsKey(message.MessageId))
            {
                message.CommandType = TransactionIdMapType[message.MessageId];
                return true;
            }
            message.CommandType = CommandType.Get_Peers;
            var result = await RequireGetPeersRegisteredInfoAsync(message.MessageId, node);
            if (result.IsOk)
            {
                message.Data["info_hash"] = result.InfoHash;
                return true;
            }
            return false;
        }
    }
}
