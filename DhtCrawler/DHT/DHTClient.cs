using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System.Threading;
using DhtCrawler.Common.RateLimit;
using DhtCrawler.DHT.Message;
using DhtCrawler.Encode;
using DhtCrawler.Encode.Exception;
using log4net;


namespace DhtCrawler.DHT
{
    public class DhtClient : IDisposable
    {
        private static byte[] GenerateRandomNodeId()
        {
            var random = new Random();
            var ids = new byte[20];
            random.NextBytes(ids);
            return ids;
        }
        //初始节点 
        private static readonly DhtNode[] BootstrapNodes =
        {
            new DhtNode() { Host = Dns.GetHostAddresses("router.bittorrent.com")[0], Port = 6881 },
            new DhtNode() { Host = Dns.GetHostAddresses("dht.transmissionbt.com")[0], Port = 6881 },
            new DhtNode() { Host = Dns.GetHostAddresses("router.utorrent.com")[0], Port = 6881 },
            new DhtNode() { Host = IPAddress.Parse("82.221.103.244"), Port = 6881 },
            new DhtNode() { Host = IPAddress.Parse("23.21.224.150"), Port = 6881 }
        };
        /// <summary>
        /// 默认入队等待时间（超时丢弃）
        /// </summary>
        private static readonly TimeSpan EnqueueWaitTime = TimeSpan.FromSeconds(10);
        private readonly ILog _logger = LogManager.GetLogger(typeof(DhtClient));

        private readonly UdpClient _client;
        private readonly IPEndPoint _endPoint;
        private readonly DhtNode _node;
        private readonly RouteTable _kTable;

        private readonly BlockingCollection<DhtNode> _nodeQueue;
        private readonly BlockingCollection<DhtData> _recvMessageQueue;
        private readonly BlockingCollection<DhtData> _sendMessageQueue;
        private readonly BlockingCollection<DhtData> _responseMessageQueue;

        private readonly IList<Task> _tasks;
        private readonly IRateLimit _sendRateLimit;
        private readonly IRateLimit _receveRateLimit;
        private readonly int _processThreadNum;
        private volatile bool running = false;

        private byte[] GetNeighborNodeId(byte[] targetId)
        {
            var selfId = _node.NodeId;
            if (targetId == null)
                targetId = _node.NodeId;
            return targetId.Take(10).Concat(selfId.Skip(10)).ToArray();
        }


        #region 事件

        public event Func<InfoHash, Task> OnFindPeer;

        public event Func<InfoHash, Task> OnReceiveInfoHash;

        #endregion

        public int ReceviceMessageCount => _recvMessageQueue.Count;
        public int SendMessageCount => _sendMessageQueue.Count;
        public int ResponseMessageCount => _responseMessageQueue.Count;
        public int FindNodeCount => _nodeQueue.Count;

        public DhtClient() : this(new DhtConfig())
        {

        }

        public DhtClient(ushort port = 0, int nodeQueueSize = 1024 * 20, int receiveQueueSize = 1024 * 20, int sendQueueSize = 1024 * 20, int sendRate = 100, int receiveRate = 100, int threadNum = 1) : this(new DhtConfig() { Port = port, NodeQueueMaxSize = nodeQueueSize, ReceiveQueueMaxSize = receiveQueueSize, SendQueueMaxSize = sendQueueSize, SendRateLimit = sendRate, ReceiveRateLimit = receiveRate, ProcessThreadNum = threadNum })
        {

        }

        public DhtClient(DhtConfig config)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, config.Port);
            _client = new UdpClient(_endPoint) { Ttl = byte.MaxValue };

            //_client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            _node = new DhtNode() { Host = IPAddress.Any, Port = config.Port, NodeId = GenerateRandomNodeId() };
            _kTable = new RouteTable(2048);

            _nodeQueue = new BlockingCollection<DhtNode>(config.NodeQueueMaxSize);
            _recvMessageQueue = new BlockingCollection<DhtData>(config.ReceiveQueueMaxSize);
            _sendMessageQueue = new BlockingCollection<DhtData>(config.SendQueueMaxSize);
            _responseMessageQueue = new BlockingCollection<DhtData>();

            _sendRateLimit = new TokenBucketLimit(config.SendRateLimit * 1024, 1, TimeUnit.Second);
            _receveRateLimit = new TokenBucketLimit(config.ReceiveRateLimit * 1024, 1, TimeUnit.Second);
            _processThreadNum = config.ProcessThreadNum;
            _tasks = new List<Task>();
        }

        #region 处理收到消息

        private void Recevie_Data(IAsyncResult asyncResult)
        {
            var client = (UdpClient)asyncResult.AsyncState;
            try
            {
                var remotePoint = _endPoint;
                var data = client.EndReceive(asyncResult, ref remotePoint);
                while (!_receveRateLimit.Require(data.Length, out var waitTime))
                {
                    Thread.Sleep(waitTime);
                }
                _recvMessageQueue.TryAdd(new DhtData() { Data = data, RemoteEndPoint = remotePoint }, EnqueueWaitTime);
                if (!running)
                    return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
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
                    _logger.Error("begin receive error", ex);
                }
            }
        }


        private async Task ProcessRequestAsync(DhtMessage msg, IPEndPoint remotePoint)
        {
            var response = new DhtMessage
            {
                MessageId = msg.MessageId,
                MesageType = MessageType.Response
            };
            var requestNode = new DhtNode() { NodeId = (byte[])msg.Data["id"], Host = remotePoint.Address, Port = (ushort)remotePoint.Port };
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
                    if (OnReceiveInfoHash != null)
                    {
                        await OnReceiveInfoHash(infoHash);
                    }
                    if (msg.CommandType == CommandType.Get_Peers)
                    {
                        var nodes = _kTable.FindNodes(infoHash.Bytes);
                        response.Data.Add("nodes", nodes.SelectMany(n => n.CompactNode()).ToArray());
                        response.Data.Add("token", infoHash.Value.Substring(0, 2));
                        if (!infoHash.IsDown)
                        {
                            foreach (var node in nodes)
                            {
                                GetPeers(node, infoHash.Bytes);
                            }
                        }
                    }
                    else if (!infoHash.IsDown)
                    {
                        if (!msg.Data.Keys.Contains("implied_port") || 0.Equals(msg.Data["implied_port"]))//implied_port !=0 则端口使用port  
                        {
                            remotePoint.Port = Convert.ToInt32(msg.Data["port"]);
                        }
                        infoHash.Peers = new HashSet<IPEndPoint>(1) { remotePoint };
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
            _responseMessageQueue.TryAdd(sendData, EnqueueWaitTime);
        }

        private async Task ProcessResponseAsync(DhtMessage msg, IPEndPoint remotePoint)
        {
            if (!MessageMap.RequireRegisteredInfo(msg))
            {
                return;
            }
            var responseNode = new DhtNode() { NodeId = (byte[])msg.Data["id"], Host = remotePoint.Address, Port = (ushort)remotePoint.Port };
            _kTable.AddOrUpdateNode(responseNode);
            object nodeInfo;
            ISet<DhtNode> nodes = null;
            switch (msg.CommandType)
            {
                case CommandType.Find_Node:
                    if (!msg.Data.TryGetValue("nodes", out nodeInfo))
                        break;
                    nodes = DhtNode.ParseNode((byte[])nodeInfo);
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
                        if (!(nodeInfo is byte[]))
                            return;
                        nodes = DhtNode.ParseNode((byte[])nodeInfo);
                        foreach (var node in nodes)
                        {
                            _kTable.AddNode(node);
                            GetPeers(node, infoHash.Bytes);
                        }
                    }
                    break;
            }
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    _nodeQueue.TryAdd(node);
                    _kTable.AddNode(node);
                }
            }
        }

        private Task ProcessMsgData()
        {
            return Task.Factory.StartNew(async () =>
             {
                 while (running)
                 {
                     if (!_recvMessageQueue.TryTake(out DhtData dhtData))
                     {
                         await Task.Delay(1000);
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
                         _logger.Error($"ErrorData:{BitConverter.ToString(dhtData.Data)}", ex);
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
             }, TaskCreationOptions.LongRunning);
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
            var bytes = msg.BEncodeBytes();
            var dhtItem = new DhtData() { Data = bytes, RemoteEndPoint = new IPEndPoint(node.Host, node.Port) };
            if (msg.CommandType == CommandType.Get_Peers)
            {
                _sendMessageQueue.TryAdd(dhtItem, EnqueueWaitTime);
            }
            else
            {
                _sendMessageQueue.TryAdd(dhtItem);
            }
        }

        private Task LoopSendMsg()
        {
            return Task.Factory.StartNew(async () =>
            {
                while (running)
                {
                    var queue = _responseMessageQueue.Count <= 0 ? _sendMessageQueue : _responseMessageQueue;
                    if (!queue.TryTake(out DhtData dhtData))
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                    try
                    {
                        while (!_sendRateLimit.Require(dhtData.Data.Length, out var waitTime))
                        {
                            await Task.Delay(waitTime);
                        }
                        await _client.SendAsync(dhtData.Data, dhtData.Data.Length, dhtData.RemoteEndPoint);
                    }
                    catch (SocketException)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private Task LoopFindNodes()
        {
            return Task.Factory.StartNew(async () =>
            {
                int limitNode = 1024 * 10;
                var nodeSet = new HashSet<DhtNode>();
                while (running)
                {
                    if (_nodeQueue.Count <= 0)
                    {
                        foreach (var dhtNode in BootstrapNodes.Union(_kTable))
                        {
                            if (!running)
                                return;
                            if (!_nodeQueue.TryAdd(dhtNode))
                                break;
                        }
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
                    await Task.Delay(60 * 1000);
                }
            }, TaskCreationOptions.LongRunning);
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
            running = true;
            _client.BeginReceive(Recevie_Data, _client);
            foreach (var task in Enumerable.Repeat(0, _processThreadNum).Select(i =>
            {
                var local = i;
                return ProcessMsgData().ContinueWith(
                    t =>
                    {
                        _logger.InfoFormat("ProcessMsg {0} Over", local);
                    });
            }))
            {
                _tasks.Add(task);
            }
            _tasks.Add(LoopFindNodes().ContinueWith(t =>
            {
                _logger.Info("Loop FindNode Over");
            }));
            _tasks.Add(LoopSendMsg().ContinueWith(t =>
            {
                _logger.Info("Loop SendMeg Over");
            }));
            _logger.Info("starting");
        }

        public void ShutDown()
        {
            _logger.Info("shuting down");
            running = false;
            ClearCollection(_nodeQueue);
            ClearCollection(_recvMessageQueue);
            ClearCollection(_sendMessageQueue);
            ClearCollection(_responseMessageQueue);
            Task.WaitAll(_tasks.ToArray());
            _logger.Info("close success");
        }

        private static void ClearCollection<T>(BlockingCollection<T> collection)
        {
            while (collection.Count > 0)
            {
                collection.TryTake(out T remove);
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _nodeQueue?.Dispose();
            _recvMessageQueue?.Dispose();
            _sendMessageQueue?.Dispose();
            _responseMessageQueue?.Dispose();
        }
    }
}
