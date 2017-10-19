using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

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
            _client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            _client.Ttl = byte.MaxValue;
            //_client.Client.IsBound
            _bootstrap_node = new HashSet<DhtNode>(new[] { new DhtNode() { Host = "router.bittorrent.com", Port = 6881 }, new DhtNode() { Host = "dht.transmissionbt.com", Port = 6881 }, new DhtNode() { Host = "router.utorrent.com", Port = 6881 } });
            _node = new DhtNode() { Host = "0.0.0.0", Port = port, NodeId = GenerateRandomNodeId() };
        }

        private void Recevie_Data(IAsyncResult asyncResult)
        {
            var client = (UdpClient)asyncResult.AsyncState;
            try
            {
                var remotePoint = _endPoint;
                var data = client.EndReceive(asyncResult, ref remotePoint);
                ProcessMsgData(data, remotePoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            while (true)
            {
                try
                {
                    client.BeginReceive(Recevie_Data, client);
                    break;
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("recevice error {0}", ex);
                }
            }
        }

        private void ProcessMsgData(byte[] data, IPEndPoint remotePoint)
        {
            try
            {
                var dic = (Dictionary<string, object>)BEncoder.Decode(data);
                var decodeItems = new[] { "t", "y", "q", "r" };
                foreach (var key in decodeItems)
                {
                    if (dic.ContainsKey(key) && dic[key] is byte[])
                    {
                        dic[key] = Encoding.ASCII.GetString((byte[])dic[key]);
                    }
                }
                var msg = new DhtMessage(dic);
                Console.WriteLine($"receive msg type is {msg.MesageType}");
                if (msg.MesageType == MessageType.Request)
                {
                    var response = new DhtMessage
                    {
                        MessageId = msg.MessageId,
                        MesageType = MessageType.Response
                    };
                    response.Data.Add("id", _node.NodeId);
                    switch (msg.CommandType)
                    {
                        case CommandType.Find_Node:
                            response.Data.Add("nodes", "");
                            break;
                        case CommandType.Get_Peers:
                            var hash = BitConverter.ToString((byte[])msg.Data["info_hash"]).Replace("-", "");
                            lock (this)
                            {
                                File.AppendAllText("hash.txt", hash + "\r\n");
                            }
                            response.Data.Add("nodes", "");
                            response.Data.Add("token", hash.Substring(0, 2));
                            break;
                        case CommandType.Ping:
                        case CommandType.Announce_Peer:
                            break;
                        default:
                            return;
                    }
                    var sendBytes = response.BEncodeBytes();
                    _client.Send(sendBytes, sendBytes.Length, remotePoint);
                }
                else if (msg.MesageType == MessageType.Response)
                {
                    msg.CommandType = MsgIdMap[msg.MessageId];
                    switch (msg.CommandType)
                    {
                        case CommandType.Find_Node:
                            if (!msg.Data.TryGetValue("nodes", out object nodeObj))
                                break;
                            var nodeBytes = (byte[])nodeObj;
                            var nodes = new HashSet<DhtNode>();
                            for (var i = 0; i < nodeBytes.Length; i += 26)
                            {
                                nodes.Add(ParseNode(nodeBytes, i));
                            }
                            foreach (var node in nodes)
                            {
                                FindNode(node);
                            }
                            break;
                    }
                    OnReceiveResponse?.Invoke(this, msg);
                }
            }
            catch (Exception ex)
            {
                var response = new DhtMessage
                {
                    MesageType = MessageType.Exception,
                    MessageId = "ex"
                };
                response.Errors.Add(201);
                response.Errors.Add("Server Error");
                var sendBytes = response.BEncodeBytes();
                _client.Send(sendBytes, sendBytes.Length, remotePoint);
            }
        }

        private DhtNode ParseNode(byte[] data, int startIndex)
        {
            byte[] idArray = new byte[20], ipArray = new byte[4], portArray = new byte[2];
            Array.Copy(data, startIndex, idArray, 0, 20);
            Array.Copy(data, startIndex + 20, ipArray, 0, 4);
            Array.Copy(data, startIndex + 24, portArray, 0, 2);
            return new DhtNode() { Host = string.Join(".", ipArray), Port = BitConverter.ToUInt16(BitConverter.IsLittleEndian ? portArray.Reverse().ToArray() : portArray, 0), NodeId = idArray };
        }

        #region 发送请求

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

        #endregion

        public event Action<DhtClient, DhtMessage> OnReceiveResponse;

        public void Run()
        {
            _running = true;
            _client.BeginReceive(Recevie_Data, _client);
            LoopFindNodes();
        }

        private async void LoopFindNodes()
        {
            while (_running)
            {
                foreach (var node in _bootstrap_node)
                {
                    FindNode(node);
                }
                await Task.Delay(1000 * 10);
            }
        }
    }
}
