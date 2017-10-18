using DhtCrawler.DHT;
using System;
using System.Collections.Generic;

namespace DhtCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            var dhtClient = new DhtClient(53383);
            dhtClient.OnReceiveResponse += DhtClient_OnReceiveMessage;
            dhtClient.OnReceiveRequest += DhtClient_OnReceiveRequest;
            dhtClient.Run();
            Console.ReadKey();
        }

        private static void DhtClient_OnReceiveRequest(DhtClient client, DhtMessage msg)
        {
            switch (msg.CommandType)
            {
                case CommandType.Ping:
                    break;
            }
        }

        private static void DhtClient_OnReceiveMessage(DhtClient client, DhtMessage obj)
        {
            DhtNode parseNode(byte[] data, int startIndex)
            {
                byte[] idArray = new byte[20], ipArray = new byte[4], portArray = new byte[2];
                Array.Copy(data, startIndex, idArray, 0, 20);
                Array.Copy(data, startIndex + 20, ipArray, 0, 4);
                Array.Copy(data, startIndex + 24, portArray, 0, 2);
                return new DhtNode() { Host = string.Join(".", ipArray), Port = BitConverter.ToUInt16(portArray, 0), NodeId = idArray };
            }
            foreach (var kv in obj.Data)
            {
                if (kv.Key == "nodes")
                {
                    var bytes = (byte[])kv.Value;
                    var nodes = new List<DhtNode>();
                    for (int i = 0; i < bytes.Length; i += 26)
                    {
                        nodes.Add(parseNode(bytes, i));
                    }
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        Console.WriteLine(nodes[i].Host + ":" + nodes[i].Port);
                    }
                }
                Console.WriteLine(kv.Key);
            }
        }
    }
}
