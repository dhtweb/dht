using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using DhtCrawler.Encode;

namespace DhtCrawler.DHT
{
    public class DhtClient
    {
        private static byte[] GenerateRandomNodeId()
        {
            var random = new Random();
            var ids = new byte[20];
            random.NextBytes(ids);
            return ids;
        }

        private static readonly IDictionary<CommandType, string> CommandMap = new Dictionary<CommandType, string>()
        {
            { CommandType.Ping, "pg" }, { CommandType.Find_Node, "fn" }, { CommandType.Get_Peers, "gp" }, { CommandType.Announce_Peer, "ap" }
        };
        private static readonly IDictionary<string, CommandType> MsgIdMap = new Dictionary<string, CommandType>()
        {
            { "pg",CommandType.Ping }, {  "fn",CommandType.Find_Node }, { "gp",CommandType.Get_Peers }, { "ap",CommandType.Announce_Peer }
        };
        private static readonly DhtNode[] bootstrapNodes = new[] { new DhtNode() { Host = "router.bittorrent.com", Port = 6881 }, new DhtNode() { Host = "dht.transmissionbt.com", Port = 6881 }, new DhtNode() { Host = "router.utorrent.com", Port = 6881 } };
        private readonly UdpClient _client;
        private readonly IPEndPoint _endPoint;
        private volatile bool _running = false;
        private readonly DhtNode _node;

        private readonly ConcurrentQueue<DhtNode> _nodeQueue;
        private readonly ConcurrentQueue<InfoHash> _downQueue;
        private readonly ConcurrentQueue<DhtData> _recvMessageQueue;
        private readonly ConcurrentQueue<DhtData> _sendMessageQueue;


        private byte[] GetNeighborNodeId(byte[] targetId)
        {
            var selfId = _node.NodeId;
            if (targetId == null)
                targetId = _node.NodeId;
            return targetId.Take(10).Concat(selfId.Skip(10)).ToArray();
        }

        public DhtClient(ushort port = 0)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, port);
            _client = new UdpClient(_endPoint);
            _client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            _client.Ttl = byte.MaxValue;
            _node = new DhtNode() { Host = "0.0.0.0", Port = port, NodeId = GenerateRandomNodeId() };

            _nodeQueue = new ConcurrentQueue<DhtNode>(bootstrapNodes);
            _downQueue = new ConcurrentQueue<InfoHash>();
            _recvMessageQueue = new ConcurrentQueue<DhtData>();
            _sendMessageQueue = new ConcurrentQueue<DhtData>();
        }

        private void Recevie_Data(IAsyncResult asyncResult)
        {
            var client = (UdpClient)asyncResult.AsyncState;
            try
            {
                var remotePoint = _endPoint;
                var data = client.EndReceive(asyncResult, ref remotePoint);
                _recvMessageQueue.Enqueue(new DhtData() { Data = data, RemoteEndPoint = remotePoint });
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

        private async void ProcessMsgData()
        {
            while (_running)
            {
                if (!_recvMessageQueue.TryDequeue(out DhtData dhtData))
                {
                    await Task.Delay(500);
                    continue;
                }
                try
                {
                    var dic = (Dictionary<string, object>)BEncoder.Decode(dhtData.Data);
                    var decodeItems = new[] { "t", "y", "q", "r" };
                    foreach (var key in decodeItems)
                    {
                        if (dic.ContainsKey(key) && dic[key] is byte[])
                        {
                            dic[key] = Encoding.ASCII.GetString((byte[])dic[key]);
                        }
                    }
                    var msg = new DhtMessage(dic);
                    switch (msg.MesageType)
                    {
                        case MessageType.Exception:
                            Console.WriteLine(msg.Errors[0] + ":" + Encoding.ASCII.GetString((byte[])msg.Errors[1]));
                            break;
                        case MessageType.Request:
                            var response = new DhtMessage
                            {
                                MessageId = msg.MessageId,
                                MesageType = MessageType.Response
                            };
                            var requestNodeId = (byte[])msg.Data["id"];
                            response.Data.Add("id", GetNeighborNodeId(requestNodeId));
                            switch (msg.CommandType)
                            {
                                case CommandType.Find_Node:
                                    response.Data.Add("nodes", "");
                                    break;
                                case CommandType.Get_Peers:
                                case CommandType.Announce_Peer:
                                    var infoHash = new InfoHash((byte[])msg.Data["info_hash"]);
                                    Console.WriteLine(infoHash.Value);
                                    await File.AppendAllTextAsync("hash.txt", infoHash.Value + Environment.NewLine);
                                    if (msg.CommandType == CommandType.Get_Peers)
                                    {
                                        response.Data.Add("nodes", "");
                                        response.Data.Add("token", infoHash.Value.Substring(0, 2));
                                    }
                                    else
                                    {
                                        var peer = dhtData.RemoteEndPoint;
                                        if (!msg.Data.Keys.Contains("implied_port") || 0.Equals(msg.Data["implied_port"]))//implied_port !=0 则端口使用port  
                                        {
                                            peer.Port = Convert.ToInt32(msg.Data["port"]);
                                        }
                                        infoHash.Peers = new HashSet<IPEndPoint>(1) { peer };
                                        await File.AppendAllTextAsync("dhash.txt", infoHash.Value + "|" + peer.Address.ToString() + ":" + peer.Port + ":" + msg.Data["port"] + Environment.NewLine);
                                    }
                                    _downQueue.Enqueue(infoHash);
                                    break;
                                case CommandType.Ping:
                                    break;
                                default:
                                    return;
                            }
                            dhtData.Data = response.BEncodeBytes();
                            _sendMessageQueue.Enqueue(dhtData);
                            break;
                        case MessageType.Response:
                            msg.CommandType = MsgIdMap[msg.MessageId];
                            switch (msg.CommandType)
                            {
                                case CommandType.Find_Node:
                                    if (!msg.Data.TryGetValue("nodes", out object nodeObj))
                                        break;
                                    var nodeBytes = (byte[])nodeObj;
                                    for (var i = 0; i < nodeBytes.Length; i += 26)
                                    {
                                        _nodeQueue.Enqueue(ParseNode(nodeBytes, i));
                                    }
                                    break;
                                case CommandType.Get_Peers:
                                    if (msg.Data.TryGetValue("values", out object peersObj))
                                    {

                                    }
                                    else if (msg.Data.TryGetValue("nodes", out object nodesObj))
                                    {
                                        //var peerBytes = (byte[])nodesObj;
                                        //var infoHash = new InfoHash((byte[])msg.Data["info_hash"]);
                                        //for (var i = 0; i < peerBytes.Length; i += 26)
                                        //{
                                        //}
                                    }
                                    break;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    var response = new DhtMessage
                    {
                        MesageType = MessageType.Exception,
                        MessageId = "ex"
                    };
                    if (ex is ArgumentException || ex is FormatException)
                    {
                        response.Errors.Add(203);
                        response.Errors.Add("Error Packet");
                    }
                    else
                    {
                        response.Errors.Add(201);
                        response.Errors.Add("Server Error");
                    }
                    dhtData.Data = response.BEncodeBytes();
                    _sendMessageQueue.Enqueue(dhtData);
                }
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

        private async void LoopSendMsg()
        {
            while (_running)
            {
                if (!_sendMessageQueue.TryDequeue(out DhtData dhtData))
                {
                    await Task.Delay(500);
                    continue;
                }
                try
                {
                    if (dhtData.RemoteEndPoint != null)
                        await _client.SendAsync(dhtData.Data, dhtData.Data.Length, dhtData.RemoteEndPoint);
                    else if (dhtData.Node != null)
                        await _client.SendAsync(dhtData.Data, dhtData.Data.Length, dhtData.Node.Host, dhtData.Node.Port);
                }
                catch (SocketException)
                {
                    _sendMessageQueue.Enqueue(dhtData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
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
            msg.Data.Add("id", GetNeighborNodeId(node.NodeId));
            var bytes = msg.BEncodeBytes();
            var dhtItem = new DhtData() { Data = bytes, Node = node };
            _sendMessageQueue.Enqueue(dhtItem);
        }

        public void FindNode(DhtNode node)
        {
            var data = new Dictionary<string, object> { { "target", GenerateRandomNodeId() } };
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

        public void Run()
        {
            _running = true;
            _client.BeginReceive(Recevie_Data, _client);
            Console.WriteLine("start run");
            Task.WaitAll(Task.Run(() =>
            {
                ProcessMsgData();
            }), Task.Run(() =>
            {
                LoopFindNodes();
            }), Task.Run(() =>
            {
                LoopSendMsg();
            }));
        }

        private async void LoopFindNodes()
        {
            while (_running)
            {
                if (!_nodeQueue.TryDequeue(out DhtNode node))
                {
                    foreach (var item in bootstrapNodes)
                    {
                        _nodeQueue.Enqueue(item);
                    }
                    await Task.Delay(5000);
                    continue;
                }
                if (_nodeQueue.Count > 2048 * 3)//_sendMessageQueue.Count > 2048 * 3 ||
                    continue;
                FindNode(node);
            }
        }
    }
}
