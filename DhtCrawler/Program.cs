using DhtCrawler.DHT;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DhtCrawler.Utils;
using log4net;
using log4net.Config;
using Tancoder.Torrent.Client;

namespace DhtCrawler
{
    class Program
    {
        private static ConcurrentStack<InfoHash> downLoadQueue = new ConcurrentStack<InfoHash>();
        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            var locker = new ManualResetEvent(false);
            var dhtClient = new DhtClient(53386);
            dhtClient.OnFindPeer += DhtClient_OnFindPeer;
            Console.CancelKeyPress += (sender, e) =>
            {
                dhtClient.ShutDown();
                locker.Set();
                e.Cancel = true;
            };
            dhtClient.Run();
            Task.Factory.StartNew(async () =>
            {
                Directory.CreateDirectory("torrent");
                var list = new List<InfoHash>(64);
                while (true)
                {
                    if (!downLoadQueue.TryPop(out InfoHash info))
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    list.Add(info);
                    if (list.Count < 32)
                    {
                        continue;
                    }
                    var uniqueItems = list.GroupBy(l => l.Value).Select(gl =>
                     {
                         var result = gl.First();
                         foreach (var hash in gl.Skip(1))
                         {
                             foreach (var point in hash.Peers)
                             {
                                 result.Peers.Add(point);
                             }
                         }
                         return result;
                     });
                    Parallel.ForEach(uniqueItems, infoItem =>
                    {
                        foreach (var peer in infoItem.Peers)
                        {
                            try
                            {
                                using (var client = new WireClient(peer))
                                {
                                    var meta = client.GetMetaData(new Tancoder.Torrent.InfoHash(infoItem.Bytes));
                                    if (meta == null)
                                        continue;
                                    var content = new StringBuilder();
                                    foreach (var key in meta.Keys)
                                    {
                                        if (key.Text == "pieces")
                                            continue;
                                        Console.WriteLine(key);
                                        content.Append(key.Text).Append(meta[key]).AppendLine();
                                    }
                                    File.WriteAllText(Path.Combine("torrent", infoItem.Value), content.ToString());
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                    });
                }
            }, TaskCreationOptions.LongRunning);
            locker.WaitOne();
        }

        private static Task DhtClient_OnFindPeer(InfoHash arg)
        {
            downLoadQueue.Push(arg);
            Console.WriteLine($"get {arg.Value} peers success");
            return Task.CompletedTask;
        }

        static void TestNetUtils()
        {
            byte[] portArray = new byte[] { 0, 0, 12, 25 };
            foreach (var b in portArray)
            {
                var sb = new StringBuilder();
                for (int i = 1; i <= 8; i++)
                {
                    sb.Append((b >> (8 - i)) & 1);
                }
                Console.WriteLine(sb);
            }
            var int1 = BitConverter.ToInt32(portArray.Reverse().ToArray(), 0);
            var int3 = BitConverter.ToInt32(portArray, 0);
            var int2 = NetUtils.ToInt32(portArray);
            Console.WriteLine(int2 == int1);
        }
    }
}
