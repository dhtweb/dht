using System;

namespace DhtCrawler.DHT
{
    public class DhtNode
    {
        public byte[] NodeId { get; set; }
        public string Host { get; set; }
        public ushort Port { get; set; }

        public override bool Equals(object obj)
        {
            var node = obj as DhtNode;
            if (node == null)
                return false;
            return (NodeId != null && NodeId == node.NodeId) || (node.Port == this.Port && string.Equals(Host, node.Host, StringComparison.OrdinalIgnoreCase));
        }

        public override int GetHashCode()
        {
            return (Host + ":" + Port).GetHashCode();
        }
    }
}