using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BitTorrent.Messages;
using BitTorrent.Messages.Wire;
using BitTorrent.MonoTorrent.BEncoding;

namespace BitTorrent.Listeners
{
    public class BitTorrentClient : IDisposable
    {
        private TcpClient client;
        private NetworkStream stream;
        public IPEndPoint EndPoint { get; private set; }
        public long MaxMetadataSize { get; set; } = 1024 * 1024;
        static readonly long PieceLength = 16 * 1024;

        public BitTorrentClient(IPEndPoint endpoint)
        {
            client = new TcpClient()
            {
                SendTimeout = 10000,
                ReceiveTimeout = 10000,
            };
            EndPoint = endpoint;
        }
        public BitTorrentClient(IPEndPoint endpoint, int localPort)
        {
            client = new TcpClient(new IPEndPoint(IPAddress.Any, localPort))
            {
                SendTimeout = 50000,
                ReceiveTimeout = 50000,
            };
            EndPoint = endpoint;
        }

        public async Task<(BEncodedDictionary, bool)> GetMetaDataAsync(InfoHash hash)
        {
            WireMessage message;
            ExtHandShack exths;
            long metadataSize;
            int piecesNum;
            byte[] metadata;
            byte ut_metadata;

            try
            {
                //连接
                Task connectTask = client.ConnectAsync(EndPoint.Address, EndPoint.Port), waitTask = Task.Delay(6000);
                await Task.WhenAny(waitTask, connectTask);
                if (!connectTask.IsCompleted || connectTask.Status != TaskStatus.RanToCompletion)
                {
                    Trace.WriteLine("Connect Timeout", "Socket");
                    return (null, true);
                }
                stream = client.GetStream();

                //发送握手
                message = new HandShack(hash);
                await SendMessageAsync(message);

                //接受握手
                message = await ReceiveMessageAsync<HandShack>(1);
                if (!message.Legal || !(message as HandShack).SupportExtend)
                {
                    Trace.WriteLine(EndPoint, "HandShack Fail");
                    return (null, true);
                }

                //发送拓展握手
                message = new ExtHandShack() { SupportUtMetadata = true };
                await SendMessageAsync(message);

                //接受拓展
                exths = await ReceiveMessageAsync<ExtHandShack>();
                if (!exths.Legal || !exths.CanGetMetadate || exths.MetadataSize > MaxMetadataSize || exths.MetadataSize <= 0)
                {
                    Trace.WriteLine(EndPoint, "ExtendHandShack Fail");
                    return (null, true);
                }
                metadataSize = exths.MetadataSize;
                ut_metadata = exths.UtMetadata;
                piecesNum = (int)Math.Ceiling(metadataSize / (decimal)PieceLength);

                var metaTask = ReceivePiecesAsync(metadataSize, piecesNum);
                //开始发送piece请求
                for (int i = 0; i < piecesNum; i++)
                {
                    message = new ExtQueryPiece(ut_metadata, i);
                    await SendMessageAsync(message);
                }
                //等待pieces接收完毕
                metadata = await metaTask;
                if (metadata == null)
                    return (null, false);
                //检查hash值是否正确
                using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
                {
                    byte[] infohash = sha1.ComputeHash(metadata);
                    if (!infohash.SequenceEqual(hash.Hash))
                    {
                        Trace.WriteLine(EndPoint, "Hash Wrong");
                        return (null, false);
                    }
                }
                return (BEncodedDictionary.DecodeTorrent(metadata), false);
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
            finally
            {
                client?.Close();
            }
        }

        private async Task<byte[]> ReceivePiecesAsync(long metadataSize, int piecesNum)
        {
            Trace.Assert(metadataSize > 0);
            try
            {
                int tryed = 0;
                byte[] metadata = new byte[metadataSize];
                BitArray flags = new BitArray(piecesNum);
                for (int i = 0; i < piecesNum && tryed < 10;)
                {
                    ExtData data = new ExtData();
                    data = await ReceiveMessageAsync<ExtData>();
                    if (data.Legal)
                    {
                        if (data.Data.Length > 0 && !flags[data.PieceID])
                        {
                            var index = data.PieceID;
                            flags.Set(index, true);

                            data.Data.CopyTo(metadata, index * PieceLength);
                            i++;
                        }
                        else
                        {
                            Trace.WriteLine(EndPoint, "Received Empty Data");
                            return null;
                        }
                    }
                    else
                    {
                        await Task.Delay(100);
                        tryed++;
                    }
                }
                for (int i = 0; i < flags.Length; i++)
                {
                    if (!flags[i])
                    {
                        Trace.WriteLine(EndPoint, "Not Received All pieces");
                        return null;
                    }
                }
                return metadata;
            }
            catch (System.IO.IOException)
            {
                Trace.WriteLine(EndPoint, "Socket Closed");
                return null;
            }
        }

        public async Task SendMessageAsync(WireMessage message)
        {
            byte[] buffer = message.Encode();
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        public async Task<T> ReceiveMessageAsync<T>(int head = 4) where T : WireMessage, new()
        {
            T msg = new T();
            int tmp;
            byte[] buffer;

            while (true)
            {
                //获取数据长度头
                buffer = new byte[head];
                tmp = await stream.ReadAsync(buffer, 0, head);
                if (tmp == 0)//未获得数据长度头 返回
                {
                    Trace.WriteLine(EndPoint, "No Message to Get");
                    return msg;
                }
                msg.OnMessageLength(buffer);//计算数据包大小
                if (msg.Length == 0)
                {
                    Trace.WriteLine(EndPoint, "Get KeepLive");
                    continue;
                }
                if (msg.Length > 32 * 1024 || msg.Length < 0)//数据包不可能过大
                {
                    Trace.WriteLine(EndPoint, "Invaild Length");
                    return msg;
                }
                buffer = new byte[msg.OnMessageLength(buffer)];

                int rlen = buffer.Length;
                while (rlen > 0)//获取数据主体
                {
                    int size = await stream.ReadAsync(buffer, buffer.Length - rlen, rlen);
                    if (size == 0)
                        return msg;
                    rlen -= size;
                }

                if (msg.CheackHead(buffer, 0))//数据包符合要求
                    break;
            }

            msg.Decode(buffer, 0, buffer.Length);
            Trace.WriteLineIf(!msg.Legal, new { Length = msg.Length, ID = buffer[0], Endpoint = EndPoint }, "No Required Message");

            return msg;
        }

        public void Dispose()
        {
            client.Close();
        }
    }
}
