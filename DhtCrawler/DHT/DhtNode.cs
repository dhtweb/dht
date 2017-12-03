using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DhtCrawler.DHT
{
    public class DhtNode
    {
        public byte[] NodeId { get; set; }
        public IPAddress Host { get; set; }
        public ushort Port { get; set; }
        public DhtNode() { }

        public DhtNode(IPEndPoint ipEndPoint)
        {
            Host = ipEndPoint.Address;
            Port = (ushort)ipEndPoint.Port;
        }

        public DhtNode(byte[] nodeId, IPEndPoint ipEndPoint) : this(ipEndPoint)
        {
            NodeId = nodeId;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DhtNode node))
                return false;
            return node.Port == Port && node.Host.Equals(Host);
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        public override string ToString()
        {
            return Host + ":" + Port;
        }

        public static DhtNode ParseNode(byte[] data, int startIndex)
        {
            byte[] idArray = new byte[20], ipArray = new byte[4], portArray = new byte[2];
            Array.Copy(data, startIndex, idArray, 0, 20);
            Array.Copy(data, startIndex + 20, ipArray, 0, 4);
            Array.Copy(data, startIndex + 24, portArray, 0, 2);
            var port = portArray[0] << 8 | portArray[1];
            return new DhtNode() { Host = new IPAddress(ipArray), Port = (ushort)port, NodeId = idArray };
        }

        public static IPEndPoint ParsePeer(byte[] data, int startIndex)
        {
            byte[] ipArray = new byte[4], portArray = new byte[2];
            Array.Copy(data, startIndex, ipArray, 0, 4);
            Array.Copy(data, startIndex + 4, portArray, 0, 2);
            var port = portArray[0] << 8 | portArray[1];
            return new IPEndPoint(new IPAddress(ipArray), port);
        }

        public static ISet<DhtNode> ParseNode(byte[] nodeBytes)
        {
            if (nodeBytes == null || nodeBytes.Length <= 0)
            {
                return new HashSet<DhtNode>(0);
            }
            var nodes = new HashSet<DhtNode>();
            for (var i = 0; i < nodeBytes.Length; i += 26)
            {
                nodes.Add(ParseNode(nodeBytes, i));
            }
            return nodes;
        }

        public byte[] CompactNode()
        {
            var info = new byte[26];
            Array.Copy(NodeId, info, 20);
            Array.Copy(Host.GetAddressBytes(), 0, info, 20, 4);
            info[24] = (byte)(Port >> 8);
            info[25] = (byte)Port;
            return info;
        }

        public byte[] CompactEndPoint()
        {
            var info = new byte[6];
            Array.Copy(Host.GetAddressBytes(), 0, info, 0, 4);
            info[4] = (byte)(Port >> 8);
            info[5] = (byte)Port;
            return info;
        }
    }
}