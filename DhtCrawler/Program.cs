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
            dhtClient.Run();
            Console.ReadKey();
        }
    }
}
