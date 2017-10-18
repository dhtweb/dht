using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DhtCrawler.DHT
{
    public class DhtClient
    {
        private static byte[] GenerateRandomNodeId()
        {
            return Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N").Substring(0, 20));
        }
        private static readonly IDictionary<CommandType, string> CommandMap = new Dictionary<CommandType, string>()
        {
            { CommandType.Ping, "pg" }, { CommandType.Find_Node, "fn" }, { CommandType.Get_Peers, "gp" }, { CommandType.Announce_Peer, "ap" }
        };
        private static readonly IDictionary<string, CommandType> MsgIdMap = new Dictionary<string, CommandType>()
        {
            { "pg",CommandType.Ping }, {  "fn",CommandType.Find_Node }, { "gp",CommandType.Get_Peers }, { "ap",CommandType.Announce_Peer }
        };
        private UdpClient _client;
        private IPEndPoint _endPoint;
        private volatile bool _running = false;

        private ISet<DhtNode> _bootstrap_node;
        private DhtNode _node;


        public DhtClient(ushort port = 0)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, port);
            _client = new UdpClient(_endPoint);
            _bootstrap_node = new HashSet<DhtNode>(new[] { new DhtNode() { Host = "router.bittorrent.com", Port = 6881 }, new DhtNode() { Host = "dht.transmissionbt.com", Port = 6881 }, new DhtNode() { Host = "router.utorrent.com", Port = 6881 } });
            _node = new DhtNode() { Host = "0.0.0.0", Port = port, NodeId = GenerateRandomNodeId() };
        }

        private void Recevie_Data(IAsyncResult asyncResult)
        {
            var client = (UdpClient)asyncResult.AsyncState;
            var data = client.EndReceive(asyncResult, ref _endPoint);
            ProcessMsgData(data);
            client.BeginReceive(Recevie_Data, client);
        }

        private void ProcessMsgData(byte[] data)
        {
            try
            {
                var dic = (Dictionary<string, object>)BEncoder.Decode(data);
                var msg = new DhtMessage(dic);
                msg.CommandType = MsgIdMap[msg.MessageId];
                if (msg.MesageType == MessageType.Request)
                {
                    OnReceiveRequest?.Invoke(this, msg);
                }
                else if (msg.MesageType == MessageType.Response)
                {
                    OnReceiveResponse?.Invoke(this, msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void SendMsg(CommandType command, IDictionary<string, object> data, DhtNode node)
        {
            var msg = new DhtMessage
            {
                CommandType = command,
                MessageId = CommandMap[command],
                MesageType = MessageType.Request,
                Data = data
            };
            msg.Data.Add("id", _node.NodeId);
            var bytes = msg.BEncodeBytes();
            _client.Send(bytes, bytes.Length, node.Host, node.Port);
        }

        public void FindNode(DhtNode node)
        {
            var data = new Dictionary<string, object> { { "target", _node.NodeId } };
            SendMsg(CommandType.Find_Node, data, node);
        }

        public void Ping(DhtNode node)
        {
            SendMsg(CommandType.Ping, null, node);
        }
        public void GetPeers(DhtNode node, byte[] infoHash)
        {
            var data = new Dictionary<string, object> { { "info_hash", infoHash } };
            SendMsg(CommandType.Get_Peers, data, node);
        }

        public void AnnouncePeer(DhtNode node, byte[] infoHash, ushort port, string token)
        {
            var data = new Dictionary<string, object> { { "info_hash", infoHash }, { "port", port }, { "port", port } };
            SendMsg(CommandType.Announce_Peer, data, node);
        }

        public event Action<DhtClient, DhtMessage> OnReceiveRequest;

        public event Action<DhtClient, DhtMessage> OnReceiveResponse;

        public void Run()
        {
            _client.BeginReceive(Recevie_Data, _client);
            foreach (var node in _bootstrap_node)
            {
                FindNode(node);
            }
        }
    }
}
