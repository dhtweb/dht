using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DhtCrawler.DHT
{
    public class DHTClient
    {
        private UdpClient _client;
        private IPEndPoint _endPoint;
        private volatile bool _running = false;

        private ISet<DhtNode> _bootstrap_node;
        private DhtNode _node;

        public DHTClient(ushort port = 0)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, port);
            _client = new UdpClient(_endPoint);
            _bootstrap_node = new HashSet<DhtNode>(new[] { new DhtNode() { Host = "router.bittorrent.com", Port = 6881 }, new DhtNode() { Host = "dht.transmissionbt.com", Port = 6881 }, new DhtNode() { Host = "router.utorrent.com", Port = 6881 } });
            _node = new DhtNode() { Host = "0.0.0.0", Port = port, NodeId = Guid.NewGuid().ToString("N").Substring(0, 20) };
        }

        private void recevie_Data(IAsyncResult asyncResult)
        {
            var client = (UdpClient)asyncResult.AsyncState;
            var data = client.EndReceive(asyncResult, ref _endPoint);
            processMsgData(data);
            client.BeginReceive(recevie_Data, client);
        }

        private void processMsgData(byte[] data)
        {
            try
            {
                var dic = (Dictionary<string, object>)BEncoder.Decode(data);
                var msg = new DhtMessage(dic);
                Console.WriteLine(msg.MesageType);
                Console.WriteLine(msg.CommandType);
                if (OnReceiveMessage != null)
                {
                    OnReceiveMessage(msg);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void FindNode(DhtNode searchNode)
        {
            var msg = new DhtMessage();
            msg.CommandType = CommandType.Find_Node;
            msg.MesageType = MessageType.Request;
            msg.MessageId = "fn";
            msg.Data.Add("id", _node.NodeId);
            msg.Data.Add("target", _node.NodeId);
            var bytes = msg.BEncodeBytes();
            _client.Send(bytes, bytes.Length, searchNode.Host, searchNode.Port);
        }

        public event Action<DhtMessage> OnReceiveMessage;

        public void Run()
        {
            _client.BeginReceive(recevie_Data, _client);
            foreach (var node in _bootstrap_node)
            {
                FindNode(node);
            }
        }
    }
}
