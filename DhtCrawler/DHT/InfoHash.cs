using System;
using System.Collections.Generic;
using System.Net;

namespace DhtCrawler.DHT
{
    public class InfoHash
    {
        public InfoHash(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 20)
            {
                throw new ArgumentException("argument bytes must be not null and length is 20");
            }
            this.Bytes = bytes;
            this.Value = BitConverter.ToString(bytes).Replace("-", "");
        }
        public byte[] Bytes { get; private set; }
        public string Value { get; private set; }
        public ISet<IPEndPoint> Peers { get; set; }
    }
}