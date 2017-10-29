using DhtCrawler.BitTorrent.Message;
using DhtCrawler.DHT;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DhtCrawler.BitTorrent
{
    public class BitTorrentClient
    {
        private TcpClient tcpClient;
        public BitTorrentClient()
        {
            this.tcpClient = new TcpClient { SendTimeout = 90000, ReceiveTimeout = 90000 };
        }

        private async Task<byte[]> ReadBytesAsync(Stream stream, int length)
        {
            var bytes = new byte[length];
            var size = 0;
            while (size < length)
            {
                size += await stream.ReadAsync(bytes, size, length - size);
            }
            return bytes;
        }

        public async void DownloadMetadata(InfoHash infoHash)
        {
            var random = new Random();
            foreach (var endPoint in infoHash.Peers)
            {
                try
                {
                    var peerId = new byte[20];
                    random.NextBytes(peerId);
                    await tcpClient.ConnectAsync(endPoint.Address, endPoint.Port);
                    var stream = tcpClient.GetStream();
                    //1.发送握手消息
                    var handHack = new HandHackMessage(infoHash.Bytes, peerId).Encode();
                    await stream.WriteAsync(handHack, 0, handHack.Length);
                    var buffer = new byte[1];
                    var offset = await stream.ReadAsync(buffer, 0, 1);
                    if (offset <= 0)
                    {
                        return;
                    }
                    var data = await ReadBytesAsync(stream, buffer[0] - 1);
                    var responseHandHack = HandHackMessage.Decode(data);
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
                    var payload = await ReadBytesAsync(stream, payloadLength);
                    var response = ExtHandHackMessage.Decode(payload);
                    if (!response.SupportUtMetadata)
                    {
                        continue;
                    }
                    //3.请求块下载信息                    
                    for (long i = 0, j = 0; i < response.MetadataSize; i += 1024 * 16, j++)
                    {
                        var pieceRequest = new RequestPieceMessage(response.UtMetadata, (int)j).Encode();
                        await stream.WriteAsync(pieceRequest, 0, pieceRequest.Length);
                        var lengthBytes = await ReadBytesAsync(stream, 4);
                        var length = BitConverter.ToInt32(BitConverter.IsLittleEndian ? lengthBytes.Reverse().ToArray() : lengthBytes, 0);
                        var bodyBytes = await ReadBytesAsync(stream, length);
                    }
                }
                finally
                {
                    if (tcpClient.Connected)
                        tcpClient.Close();
                }

            }
        }
    }
}
