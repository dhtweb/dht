using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using DhtCrawler.Common;
using DhtCrawler.Common.RateLimit;
using DhtCrawler.Common.Utils;
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

        private static readonly DhtNode[] DefaultBootstrapNodes =
        {
            new DhtNode() {Host = IPAddress.Parse("67.215.246.10"), Port = 6881},
            new DhtNode() {Host = IPAddress.Parse("87.98.162.88"), Port = 6881},
            new DhtNode() {Host = IPAddress.Parse("82.221.103.244"), Port = 6881},
            new DhtNode() {Host = IPAddress.Parse("23.21.224.150"), Port = 6881}
        };
        //初始节点 
        private readonly IList<DhtNode> _bootstrapNodes;
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
        private readonly BlockingCollection<Tuple<DhtMessage, IPEndPoint>> _requestQueue;
        private readonly BlockingCollection<Tuple<DhtMessage, IPEndPoint>> _responseQueue;
        private readonly BlockingCollection<Tuple<DhtMessage, DhtNode>> _sendMessageQueue;
        private readonly BlockingCollection<Tuple<DhtMessage, DhtNode>> _replyMessageQueue;//
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly IList<Task> _tasks;
        private readonly IRateLimit _sendRateLimit;
        private readonly IRateLimit _receveRateLimit;
        private readonly int _processResponseThreadNum;
        private readonly int _processRequestThreadNum;
        private volatile bool running = false;

        private byte[] GetNeighborNodeId(byte[] targetId)
        {
            var selfId = _node.NodeId;
            if (targetId == null)
                targetId = _node.NodeId;
            return targetId.Take(10).Concat(selfId.Skip(10)).ToArray();
        }

        protected virtual AbstractMessageMap MessageMap { get; }

        #region 事件

        public event Func<InfoHash, Task> OnFindPeer;

        public event Func<InfoHash, Task> OnAnnouncePeer;

        public event Func<InfoHash, Task> OnReceiveInfoHash;

        #endregion

        public int ReceviceMessageCount => _recvMessageQueue.Count;
        public int RequestMessageCount => _requestQueue.Count;
        public int ResponseMessageCount => _responseQueue.Count;
        public int SendMessageCount => _sendMessageQueue.Count;
        public int ReplyMessageCount => _replyMessageQueue.Count;
        public int FindNodeCount => _nodeQueue.Count;

        public DhtClient() : this(new DhtConfig())
        {

        }

        public DhtClient(ushort port = 0, int nodeQueueSize = 1024 * 20, int receiveQueueSize = 1024 * 20, int sendQueueSize = 1024 * 20, int sendRate = 100, int receiveRate = 100, int threadNum = 1) : this(new DhtConfig() { Port = port, NodeQueueMaxSize = nodeQueueSize, ReceiveQueueMaxSize = receiveQueueSize, SendQueueMaxSize = sendQueueSize, SendRateLimit = sendRate, ReceiveRateLimit = receiveRate, ProcessRequestThreadNum = threadNum, ProcessResponseThreadNum = threadNum })
        {

        }

        public DhtClient(DhtConfig config)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, config.Port);
            _client = new UdpClient(_endPoint) { Ttl = byte.MaxValue };
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    _client.Client.IOControl(-1744830452, new byte[] { 0, 0, 0, 0 }, null);
                    break;
            }

            _node = new DhtNode() { Host = IPAddress.Any, Port = config.Port, NodeId = GenerateRandomNodeId() };
            _kTable = new RouteTable(config.KTableSize);

            _nodeQueue = new BlockingCollection<DhtNode>(config.NodeQueueMaxSize);
            _recvMessageQueue = new BlockingCollection<DhtData>(config.ReceiveQueueMaxSize);
            _requestQueue = new BlockingCollection<Tuple<DhtMessage, IPEndPoint>>(config.RequestQueueMaxSize);
            _responseQueue = new BlockingCollection<Tuple<DhtMessage, IPEndPoint>>(config.ResponseQueueMaxSize);
            _sendMessageQueue = new BlockingCollection<Tuple<DhtMessage, DhtNode>>(config.SendQueueMaxSize);
            _replyMessageQueue = new BlockingCollection<Tuple<DhtMessage, DhtNode>>();

            _sendRateLimit = new TokenBucketLimit(config.SendRateLimit * 1024, 1, TimeUnit.Second);
            _receveRateLimit = new TokenBucketLimit(config.ReceiveRateLimit * 1024, 1, TimeUnit.Second);
            _processResponseThreadNum = config.ProcessResponseThreadNum;
            _processRequestThreadNum = config.ProcessRequestThreadNum;
            _cancellationTokenSource = new CancellationTokenSource();

            _tasks = new List<Task>();
            _bootstrapNodes = new List<DhtNode>(DefaultBootstrapNodes);
            MessageMap = IocContainer.GetService<AbstractMessageMap>();
        }

        #region 处理收到消息

        private void Recevie_Data(IAsyncResult asyncResult)
        {
            var client = (UdpClient)asyncResult.AsyncState;
            try
            {
                var remotePoint = _endPoint;
                var data = client.EndReceive(asyncResult, ref remotePoint);
                while (!_receveRateLimit.Require(data.Length, out var limitTime))
                {
                    Thread.Sleep(limitTime);
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
                        if (OnAnnouncePeer != null)
                        {
                            await OnAnnouncePeer(infoHash);
                        }
                    }
                    break;
                case CommandType.Ping:
                    break;
                default:
                    return;
            }
            _replyMessageQueue.TryAdd(new Tuple<DhtMessage, DhtNode>(response, new DhtNode(remotePoint)));
        }

        private async Task ProcessResponseAsync(DhtMessage msg, IPEndPoint remotePoint)
        {
            if (msg.MessageId.Length != 2)
                return;
            var responseNode = new DhtNode() { NodeId = (byte[])msg.Data["id"], Host = remotePoint.Address, Port = (ushort)remotePoint.Port };
            var flag = MessageMap.RequireRegisteredInfo(msg, responseNode);
            if (!flag)
            {
                return;
            }
            _kTable.AddOrUpdateNode(responseNode);
            object nodeInfo;
            ISet<DhtNode> nodes = null;
            switch (msg.CommandType)
            {
                case CommandType.Find_Node:
                    if (_kTable.IsFull || MessageMap.IsFull || !msg.Data.TryGetValue("nodes", out nodeInfo))
                        return;
                    nodes = DhtNode.ParseNode((byte[])nodeInfo);
                    break;
                case CommandType.Get_Peers:
                    var hashByte = msg.Get<byte[]>("info_hash");
                    var infoHash = new InfoHash(hashByte);
                    if (msg.Data.TryGetValue("values", out nodeInfo))
                    {
                        IList<object> peerInfo;
                        if (nodeInfo is byte[] bytes)
                        {
                            peerInfo = new object[] { bytes };
                        }
                        else
                        {
                            peerInfo = (IList<object>)nodeInfo;
                        }
                        var peers = new HashSet<IPEndPoint>(peerInfo.Count);
                        foreach (var t in peerInfo)
                        {
                            var peer = (byte[])t;
                            var point = DhtNode.ParsePeer(peer, 0);
                            if (point.Address.IsPublic())
                            {
                                peers.Add(point);
                            }
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

        private void ProcessMsgData()
        {
            while (running)
            {
                if (!_recvMessageQueue.TryTake(out DhtData dhtData))
                {
                    Thread.Sleep(1000);
                    continue;
                }
                try
                {
                    var dic = (Dictionary<string, object>)BEncoder.Decode(dhtData.Data);
                    var msg = new DhtMessage(dic);
                    var item = new Tuple<DhtMessage, IPEndPoint>(msg, dhtData.RemoteEndPoint);
                    switch (msg.MesageType)
                    {
                        case MessageType.Request:
                            _requestQueue.TryAdd(item);
                            break;
                        case MessageType.Response:
                            _responseQueue.TryAdd(item);
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
                    _sendMessageQueue.TryAdd(new Tuple<DhtMessage, DhtNode>(response, new DhtNode(dhtData.RemoteEndPoint)));
                }
            }
        }

        private async Task LoopProcessRequestMsg()
        {
            while (running)
            {
                if (!_requestQueue.TryTake(out var msgInfo))
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                    continue;
                }
                try
                {
                    await ProcessRequestAsync(msgInfo.Item1, msgInfo.Item2);
                }
                catch (Exception ex)
                {
                    _logger.Error($"ErrorData:{msgInfo.Item1.ToJson()}", ex);
                }
            }
        }

        private async Task LoopProcessResponseMsg()
        {
            while (running)
            {
                if (!_responseQueue.TryTake(out var msgInfo))
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                    continue;
                }
                try
                {
                    await ProcessResponseAsync(msgInfo.Item1, msgInfo.Item2);
                }
                catch (Exception ex)
                {
                    _logger.Error($"ErrorData:{msgInfo.Item1.ToJson()}", ex);
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
            msg.Data.Add("id", GetNeighborNodeId(node.NodeId));
            var dhtItem = new Tuple<DhtMessage, DhtNode>(msg, node);
            if (msg.CommandType == CommandType.Get_Peers)
            {
                _sendMessageQueue.TryAdd(dhtItem, EnqueueWaitTime);
            }
            else
            {
                _sendMessageQueue.TryAdd(dhtItem);
            }
        }

        private async Task LoopSendMsg()
        {
            while (running)
            {
                var queue = _replyMessageQueue.Count <= 0 ? _sendMessageQueue : _replyMessageQueue;
                if (!queue.TryTake(out var dhtData))
                {
                    await Task.Delay(1000);
                    continue;
                }
                var msg = dhtData.Item1;
                var node = dhtData.Item2;
                try
                {
                    if (queue == _sendMessageQueue)
                    {
                        var regResult = MessageMap.RegisterMessage(msg, node);
                        if (!regResult)
                        {
                            continue;
                        }
                    }
                    var sendBytes = msg.BEncodeBytes();
                    var remotepoint = new IPEndPoint(node.Host, node.Port);
                    if (!remotepoint.Address.IsPublic())
                    {
                        continue;
                    }
                    while (!_sendRateLimit.Require(sendBytes.Length, out var limitTime))
                    {
                        await Task.Delay(limitTime);
                    }
                    await _client.SendAsync(sendBytes, sendBytes.Length, remotepoint);
                }
                catch (Exception ex)
                {
                    if (ex is SocketException || ex is InvalidOperationException)
                    {
                        MessageMap.RequireRegisteredInfo(msg, node);
                    }
                    _logger.Error(ex);
                }
            }
        }

        private async Task LoopFindNodes()
        {
            int limitNode = 1024 * 10;
            var nodeSet = new HashSet<DhtNode>();
            while (running)
            {
                if (_nodeQueue.Count <= 0)
                {
                    foreach (var dhtNode in _kTable)
                    {
                        if (!running)
                            return;
                        if (!_nodeQueue.TryAdd(dhtNode))
                            break;
                    }
                }
                while (running && _nodeQueue.TryTake(out var node) && nodeSet.Count <= limitNode)
                {
                    nodeSet.Add(node);
                }
                using (var nodeEnumerator = _bootstrapNodes.Union(nodeSet).GetEnumerator())
                {
                    while (running && nodeEnumerator.MoveNext())
                    {
                        FindNode(nodeEnumerator.Current);
                    }
                }
                nodeSet.Clear();
                if (!running)
                    return;
                if (nodeSet.Count < 10 || (SendMessageCount > 0 && ReceviceMessageCount > 0))
                    await Task.Delay(60 * 1000, _cancellationTokenSource.Token);
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

        public void GetPeers(byte[] infoHash)
        {
            var nodes = new HashSet<DhtNode>(_kTable.FindNodes(infoHash));
            if (nodes.IsEmpty() || nodes.Count < 8)
            {
                foreach (var node in _bootstrapNodes)
                {
                    nodes.Add(node);
                }
            }
            foreach (var node in nodes)
            {
                GetPeers(node, infoHash);
            }
        }

        public void AnnouncePeer(DhtNode node, byte[] infoHash, ushort port, string token)
        {
            var data = new Dictionary<string, object> { { "info_hash", infoHash }, { "port", port }, { "token", token } };
            SendMsg(CommandType.Announce_Peer, data, node);
        }
        #endregion

        public bool AddBootstrapNode(DhtNode node)
        {
            if (running)
                return false;
            _bootstrapNodes.Add(node);
            return true;
        }

        public void Run()
        {
            running = true;
            _client.BeginReceive(Recevie_Data, _client);
            _tasks.Add(Task.Factory.StartNew(ProcessMsgData, TaskCreationOptions.LongRunning).ContinueWith(t => { _logger.Info("Process Receive Task Completed"); }));
            for (int i = 0; i < _processResponseThreadNum; i++)
            {
                var local = i;
                Task.Run(() =>
                {
                    _tasks.Add(LoopProcessResponseMsg().ContinueWith(t => { _logger.InfoFormat("Process Response Msg Task {0} Completed", local); }));
                });
            }
            for (int i = 0; i < _processRequestThreadNum; i++)
            {
                var local = i;
                Task.Run(() =>
                {
                    _tasks.Add(LoopProcessRequestMsg().ContinueWith(t => { _logger.InfoFormat("Process Request Msg Task {0} Completed", local); }));
                });
            }
            Task.Run(() =>
            {
                _tasks.Add(LoopProcessRequestMsg().ContinueWith(t =>
                {
                    _logger.Info("Process Request Msg Task Completed");
                }));
            });
            Task.Run(() =>
            {
                _tasks.Add(LoopFindNodes().ContinueWith(t =>
                {
                    _logger.Info("Loop FindNode Task Complete");
                }));
            });
            Task.Run(() =>
            {
                _tasks.Add(LoopSendMsg().ContinueWith(t =>
                {
                    _logger.Info("Loop SendMsg Task Completed");
                }));
            });
            _logger.Info("starting");
        }

        public void ShutDown()
        {
            _logger.Info("shuting down");
            running = false;
            _cancellationTokenSource.Cancel(true);
            ClearCollection(_nodeQueue);
            ClearCollection(_recvMessageQueue);
            ClearCollection(_sendMessageQueue);
            ClearCollection(_replyMessageQueue);
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
            _replyMessageQueue?.Dispose();
        }
    }
}
