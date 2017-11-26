using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using DhtCrawler.Common.Utils;
using DhtCrawler.Store;

namespace DhtCrawler.DHT
{
    public class InfoHash : IStoreEntity
    {
        public InfoHash() { }
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
        public bool IsDown { get; set; }

        public void WriteToStream(Stream strem)
        {
            strem.Write(Bytes, 0, Bytes.Length);
            var peerSize = BitConverter.GetBytes(Peers.Count);
            strem.Write(peerSize, 0, peerSize.Length);
            foreach (var peer in Peers)
            {
                byte[] ipArray = peer.Address.GetAddressBytes(), portArray = BitConverter.GetBytes(peer.Port);
                strem.Write(ipArray, 0, ipArray.Length);
                strem.Write(portArray, 0, portArray.Length);
            }
        }

        public async void WriteToStreamAsync(Stream strem)
        {
            await strem.WriteAsync(Bytes, 0, Bytes.Length);
            var peerSize = BitConverter.GetBytes(Peers.Count);
            await strem.WriteAsync(peerSize, 0, peerSize.Length);
            foreach (var peer in Peers)
            {
                byte[] ipArray = peer.Address.GetAddressBytes(), portArray = BitConverter.GetBytes(peer.Port);
                await strem.WriteAsync(ipArray, 0, ipArray.Length);
                await strem.WriteAsync(portArray, 0, portArray.Length);
            }
        }

        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            {
                WriteToStream(stream);
                return stream.ToArray();
            }
        }

        public void ReadBytes(byte[] data)
        {
            Bytes = data.CopyArray(0, 20);
            var peerByteArray = data.CopyArray(20, 4);
            var peerSize = BitConverter.ToInt32(peerByteArray, 0);
            if (Peers == null)
            {
                Peers = new HashSet<IPEndPoint>(peerSize);
            }
            if (peerSize <= 0)
            {
                return;
            }
            var peerArray = data.CopyArray(24, data.Length - 24);
            for (var i = 0; i < peerArray.Length; i += 8)
            {
                var ip = new IPAddress(peerArray.CopyArray(i, 4));
                var port = BitConverter.ToInt32(peerArray.CopyArray(i + 4, 4), 0);
                Peers.Add(new IPEndPoint(ip, port));
            }
        }
    }
}