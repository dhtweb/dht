using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using DhtCrawler.DHT.Message;
using DhtCrawler.Encode;
using DhtCrawler.Encode.Exception;
using log4net;


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

        private static readonly DhtNode[] bootstrapNodes = { new DhtNode() { Host = "router.bittorrent.com", Port = 6881 }, new DhtNode() { Host = "dht.transmissionbt.com", Port = 6881 }, new DhtNode() { Host = "router.utorrent.com", Port = 6881 } };

        private readonly ILog logger = LogManager.GetLogger(typeof(DhtClient));

        private readonly UdpClient _client;
        private readonly IPEndPoint _endPoint;
        private readonly DhtNode _node;
        private readonly RouteTable _kTable;

        private readonly BlockingCollection<DhtNode> _nodeQueue;
        private readonly BlockingCollection<DhtData> _recvMessageQueue;
        private readonly BlockingCollection<DhtData> _sendMessageQueue;
        private readonly BlockingCollection<DhtData> _responseMessageQueue;

        private IList<Task> _tasks;
        private byte[] GetNeighborNodeId(byte[] targetId)
        {
            var selfId = _node.NodeId;
            if (targetId == null)
                targetId = _node.NodeId;
            return targetId.Take(10).Concat(selfId.Skip(10)).ToArray();
        }


        #region 事件

        public event Func<InfoHash, Task> OnFindPeer;

        #endregion

        public DhtClient(ushort port = 0, int nodeQueueSize = 1024 * 20, int receiveQueueSize = 1024 * 20, int sendQueueSize = 1024 * 10)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, port);
            _client = new UdpClient(_endPoint);
            _client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            _client.Ttl = byte.MaxValue;
            _node = new DhtNode() { Host = "0.0.0.0", Port = port, NodeId = GenerateRandomNodeId() };
            _kTable = new RouteTable(2048);

            _nodeQueue = new BlockingCollection<DhtNode>(nodeQueueSize);
            _recvMessageQueue = new BlockingCollection<DhtData>(receiveQueueSize);
            _sendMessageQueue = new BlockingCollection<DhtData>(sendQueueSize);
            _responseMessageQueue = new BlockingCollection<DhtData>();

            _tasks = new List<Task>();
        }


        private void Recevie_Data(IAsyncResult asyncResult)
        {
            var client = (UdpClient)asyncResult.AsyncState;
            try
            {
                var remotePoint = _endPoint;
                var data = client.EndReceive(asyncResult, ref remotePoint);
                _recvMessageQueue.Add(new DhtData() { Data = data, RemoteEndPoint = remotePoint });
                if (_recvMessageQueue.IsAddingCompleted)
                    return;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
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

        #region 处理收到消息

        private async Task ProcessRequestAsync(DhtMessage msg, IPEndPoint remotePoint)
        {
            var response = new DhtMessage
            {
                MessageId = msg.MessageId,
                MesageType = MessageType.Response
            };
            var requestNode = new DhtNode() { NodeId = (byte[])msg.Data["id"], Host = remotePoint.Address.ToString(), Port = (ushort)remotePoint.Port };
            _kTable.AddOrUpdateNode(requestNode);
            response.Data.Add("id", GetNeighborNodeId(requestNode.NodeId));
            switch (msg.CommandType)
            {
                case CommandType.Find_Node:
                    var targetNodeId = (byte[])msg.Data["target"];
                    response.Data.Add("nodes", _kTable.FindNodes(targetNodeId).SelectMany(n => n.CompactNode()).ToArray());
                    break;
                case CommandType.Get_Peers:
                case CommandType.Announce_Peer:
                    var infoHash = new InfoHash((byte[])msg.Data["info_hash"]);
                    Console.WriteLine(infoHash.Value);
                    await File.AppendAllTextAsync("hash.txt", infoHash.Value + Environment.NewLine);
                    if (msg.CommandType == CommandType.Get_Peers)
                    {
                        var nodes = _kTable.FindNodes(infoHash.Bytes);
                        response.Data.Add("nodes", nodes.SelectMany(n => n.CompactNode()).ToArray());
                        response.Data.Add("token", infoHash.Value.Substring(0, 2));
                        foreach (var node in nodes)
                        {
                            GetPeers(node, infoHash.Bytes);
                        }
                    }
                    else
                    {
                        var peer = remotePoint;
                        if (!msg.Data.Keys.Contains("implied_port") || 0.Equals(msg.Data["implied_port"]))//implied_port !=0 则端口使用port  
                        {
                            peer.Port = Convert.ToInt32(msg.Data["port"]);
                        }
                        infoHash.Peers = new HashSet<IPEndPoint>(1) { peer };
                        if (OnFindPeer != null)
                        {
                            await OnFindPeer(infoHash);
                        }
                    }
                    break;
                case CommandType.Ping:
                    break;
                default:
                    return;
            }
            var sendData = new DhtData
            {
                RemoteEndPoint = remotePoint,
                Data = response.BEncodeBytes()
            };
            _responseMessageQueue.Add(sendData);
        }

        private async Task ProcessResponseAsync(DhtMessage msg, IPEndPoint remotePoint)
        {
            if (!MessageMap.RequireRegisteredInfo(msg))
            {
                return;
            }
            var responseNode = new DhtNode() { NodeId = (byte[])msg.Data["id"], Host = remotePoint.Address.ToString(), Port = (ushort)remotePoint.Port };
            _kTable.AddOrUpdateNode(responseNode);
            object nodeInfo;
            ISet<DhtNode> nodes;
            switch (msg.CommandType)
            {
                case CommandType.Find_Node:
                    if (!msg.Data.TryGetValue("nodes", out nodeInfo))
                        break;
                    nodes = DhtNode.ParseNode((byte[])nodeInfo);
                    foreach (var node in nodes)
                    {
                        _nodeQueue.TryAdd(node);
                        _kTable.AddNode(node);
                    }
                    break;
                case CommandType.Get_Peers:
                    var hashByte = msg.Get<byte[]>("info_hash");
                    var infoHash = new InfoHash(hashByte);
                    if (msg.Data.TryGetValue("values", out nodeInfo))
                    {
                        var peerInfo = (IList<object>)nodeInfo;
                        var peers = new HashSet<IPEndPoint>(peerInfo.Count);
                        foreach (var t in peerInfo)
                        {
                            var peer = (byte[])t;
                            peers.Add(DhtNode.ParsePeer(peer, 0));
                        }
                        if (peers.Count > 0)
                        {
                            infoHash.Peers = peers;
                            if (OnFindPeer != null)
                            {
                                await OnFindPeer(infoHash);
                            }
                            return;
                        }
                    }
                    if (msg.Data.TryGetValue("nodes", out nodeInfo))
                    {
                        nodes = DhtNode.ParseNode((byte[])nodeInfo);
                        foreach (var node in nodes)
                        {
                            _kTable.AddNode(node);
                            GetPeers(node, infoHash.Bytes);
                        }
                    }
                    break;
            }
        }

        private async Task ProcessMsgData()
        {
            while (!_recvMessageQueue.IsCompleted)
            {
                if (!_recvMessageQueue.TryTake(out DhtData dhtData))
                {
                    await Task.Delay(500);
                    continue;
                }
                try
                {
                    var dic = (Dictionary<string, object>)BEncoder.Decode(dhtData.Data);
                    var msg = new DhtMessage(dic);
                    switch (msg.MesageType)
                    {
                        case MessageType.Request:
                            await ProcessRequestAsync(msg, dhtData.RemoteEndPoint);
                            break;
                        case MessageType.Response:
                            await ProcessResponseAsync(msg, dhtData.RemoteEndPoint);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"ErrorData:{BitConverter.ToString(dhtData.Data)}", ex);
                    var response = new DhtMessage
                    {
                        MesageType = MessageType.Exception,
                        MessageId = new byte[] { 0, 0 }
                    };
                    if (ex is DecodeException)
                    {
                        response.Errors.Add(203);
                        response.Errors.Add("Error Protocol");
                    }
                    else
                    {
                        response.Errors.Add(202);
                        response.Errors.Add("Server Error:" + ex.Message);
                    }
                    dhtData.Data = response.BEncodeBytes();
                    _sendMessageQueue.TryAdd(dhtData);

                }
            }
        }

        #endregion

        #region 发送请求

        private void SendMsg(CommandType command, IDictionary<string, object> data, DhtNode node)
        {
            var msg = new DhtMessage
            {
                CommandType = command,
                MesageType = MessageType.Request,
                Data = new SortedDictionary<string, object>(data)
            };
            if (!MessageMap.RegisterMessage(msg))
            {
                return;
            }
            msg.Data.Add("id", GetNeighborNodeId(node.NodeId));
            MessageEnqueue(msg, node);
        }

        private void MessageEnqueue(DhtMessage msg, DhtNode node)
        {
            if (_sendMessageQueue.IsAddingCompleted)
                return;
            var bytes = msg.BEncodeBytes();
            var dhtItem = new DhtData() { Data = bytes, Node = node };
            if (msg.CommandType == CommandType.Get_Peers)
            {
                _sendMessageQueue.Add(dhtItem);
            }
            else
            {
                _sendMessageQueue.TryAdd(dhtItem);
            }
        }

        private async Task LoopSendMsg()
        {
            while (true)
            {
                var queue = _responseMessageQueue.Count <= 0 ? _sendMessageQueue : _responseMessageQueue;
                if (queue.IsAddingCompleted)
                {
                    return;
                }
                if (!queue.TryTake(out DhtData dhtData))
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
                    queue.TryAdd(dhtData);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        private async Task LoopFindNodes()
        {
            int limitNode = 1024 * 10;
            var nodeSet = new HashSet<DhtNode>();
            while (!_nodeQueue.IsAddingCompleted)
            {
                if (_nodeQueue.Count <= 0)
                {
                    foreach (var dhtNode in bootstrapNodes.Union(_kTable))
                    {
                        if (_nodeQueue.IsAddingCompleted)
                            return;
                        if (!_nodeQueue.TryAdd(dhtNode))
                            break;
                    }
                    if (_nodeQueue.Count <= bootstrapNodes.Length)
                        await Task.Delay(5000);
                }
                while (_nodeQueue.TryTake(out var node) && nodeSet.Count <= limitNode)
                {
                    nodeSet.Add(node);
                }
                foreach (var node in nodeSet)
                {
                    FindNode(node);
                }
                nodeSet.Clear();
            }
        }

        #endregion

        #region dht协议命令
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
            var data = new Dictionary<string, object> { { "info_hash", infoHash }, { "port", port }, { "token", token } };
            SendMsg(CommandType.Announce_Peer, data, node);
        }
        #endregion

        public void Run()
        {
            _client.BeginReceive(Recevie_Data, _client);
            _tasks.Add(ProcessMsgData());
            _tasks.Add(LoopFindNodes());
            _tasks.Add(LoopSendMsg());
            logger.Info("starting");
        }

        public void ShutDown()
        {
            var disposeItems = new LinkedList<IDisposable>();
            disposeItems.AddLast(_nodeQueue);
            disposeItems.AddLast(_recvMessageQueue);
            disposeItems.AddLast(_sendMessageQueue);
            disposeItems.AddLast(_responseMessageQueue);
            disposeItems.AddLast(_client);
            _nodeQueue.CompleteAdding();
            _recvMessageQueue.CompleteAdding();
            _sendMessageQueue.CompleteAdding();
            _responseMessageQueue.CompleteAdding();
            logger.Info("shuting down");
            Task.WhenAll(_tasks.ToArray()).ContinueWith(t =>
            {
                logger.Info("closed");
            }).Wait();
            foreach (var disposable in disposeItems)
            {
                disposable.Dispose();
            }
        }


    }
}
