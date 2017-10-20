using System.Net;

namespace DhtCrawler.DHT
{
    public class DhtData
    {
        public byte[] Data { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }
        public DhtNode Node { get; set; }
    }
}