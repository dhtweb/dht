using DhtCrawler.BitTorrent.Message;
using DhtCrawler.DHT;
using System;
using System.Collections.Generic;
using System.Linq;
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
            //1.发送握手消息
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
            //2.发送扩展消息
            var extHand = new ExtHandHackMessage().Encode();
            await stream.WriteAsync(extHand, 0, extHand.Length);
            var head = new byte[4];
            await stream.ReadAsync(head, 0, head.Length);
            var payloadLength = BitConverter.ToInt32(BitConverter.IsLittleEndian ? head.Reverse().ToArray() : head, 0);
            var payload = new byte[payloadLength];
            await stream.ReadAsync(payload, 0, payload.Length);

            //
        }
    }
}
