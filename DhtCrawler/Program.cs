using DhtCrawler.DHT;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DhtCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            testPing();
        }

        static void testPing()
        {
            //IPEndPoint remote = new IPEndPoint(IPAddress.Any, 36555);
            //var udpClient = new UdpClient(remote);
            //udpClient.BeginReceive(e =>
            //{
            //    try
            //    {
            //        Console.WriteLine("receive");
            //        var client = (UdpClient)e.AsyncState;
            //        byte[] receiveBytes = client.EndReceive(e, ref remote);
            //        string receiveString = Encoding.ASCII.GetString(receiveBytes);
            //        Console.WriteLine("Received: {0}", receiveString);
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine(ex);
            //    }

            //}, udpClient);
            //var sendData = Encoding.ASCII.GetBytes("{\"t\":\"aa\", \"y\":\"q\", \"q\":\"ping\", \"a\":{\"id\":\"abcdefghij0123456789\"}}");
            //udpClient.Send(sendData, sendData.Length, "67.215.246.10", 6881);
            //Console.WriteLine("11111111111111111111111");
            //Console.ReadKey();
            //var bytes = Encoding.ASCII.GetBytes("i42e");
            //var msg = new DhtMessage();
            ////message.Add("y", "q");
            //msg.MesageType = MessageType.Request;
            ////message.Add("t", "aa");
            //msg.MessageId = "aa";
            ////message.Add("q", "ping");
            //msg.CommandType = CommandType.Ping;
            //msg.Data.Add("id", "abcdefghij0123456789");

            //var data = new Dictionary<string, object>(1);
            //data.Add("id", "abcdefghij0123456789");
            //message.Add("a", data);

            //var encodeStr = msg.BEncode();
            //Console.WriteLine(encodeStr);
            //Console.WriteLine(string.Equals(encodeStr, "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe", StringComparison.InvariantCulture));
            //int index = 0;
            //var obj = BEncoder.Decode(Encoding.ASCII.GetBytes(encodeStr), ref index);
            //Console.WriteLine(obj);             dotnet add package BencodeNET --version 2.2.22

            var dhtClient = new DHTClient(53383);
            dhtClient.OnReceiveMessage += DhtClient_OnReceiveMessage;
            dhtClient.Run();
            Console.ReadKey();
        }

        private static void DhtClient_OnReceiveMessage(DhtMessage obj)
        {
            foreach (var kv in obj.Data)
            {
                if (kv.Key == "nodes")
                {
                    var bytes = Encoding.ASCII.GetBytes(kv.Value.ToString());
                    var sb = new StringBuilder();
                    for (var i = 20; i < 24; i++)
                    {
                        sb.Append(bytes[i]).Append(".");
                    }
                    Console.WriteLine(sb.ToString());
                }
                Console.WriteLine(kv.Key + "|||||||||" + kv.Value);
            }
        }
    }
}
