using DhtCrawler.BitTorrent.Message;
using DhtCrawler.DHT;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DhtCrawler.BitTorrent
{
    public class BitTorrentClient
    {
        private IPEndPoint iPEndPoint;
        private TcpClient tcpClient;
        public BitTorrentClient(IPEndPoint iPEndPoint)
        {
            this.iPEndPoint = iPEndPoint;
            this.tcpClient = new TcpClient();
            //tcpClient.Connect(iPEndPoint);
        }

        public async void DownloadMetadata(InfoHash infoHash)
        {
            await tcpClient.ConnectAsync(this.iPEndPoint.Address, this.iPEndPoint.Port);
            var stream = tcpClient.GetStream();
            var handHack = new HandHackMessage(infoHash).Encode();
            await stream.WriteAsync(handHack, 0, handHack.Length);
            var buffer = new byte[1];
            var offset = await stream.ReadAsync(buffer, 0, 1);
            if (offset <= 0)
            {
                return;
            }
            buffer = new byte[buffer[0] - 1];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            HandHackMessage responseHandHack = buffer;
            if (infoHash.Bytes != responseHandHack.InfoHash)
            {
                return;
            }
        }
    }
}
