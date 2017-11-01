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
        }
        public byte[] Bytes { get; private set; }
        public string Value => BitConverter.ToString(Bytes).Replace("-", "");
        public ISet<IPEndPoint> Peers { get; set; }
    }
}