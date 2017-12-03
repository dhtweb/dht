using System;

namespace DhtCrawler.DHT.Message
{
    public class Record
    {
        public byte[] InfoHash { get; set; }
        public DateTime AddTime { get; set; }
    }
}
