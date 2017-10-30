using DhtCrawler.DHT;
using System;

namespace DhtCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            //while (args.Length == 0)
            //{
            //    Console.WriteLine("please enter port!");
            //    args = Console.ReadLine().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            //}
            var dhtClient = new DhtClient(53386);
            dhtClient.Run();
            Console.ReadKey();
        }
    }
}
