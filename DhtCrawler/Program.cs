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
using DhtCrawler.Collections;
using DhtCrawler.Utils;
using log4net;
using log4net.Config;
using Tancoder.Torrent.BEncoding;
using Tancoder.Torrent.Client;
using DhtCrawler.Common;

namespace DhtCrawler
{
    class Program
    {
        private static ConcurrentStack<InfoHash> downLoadQueue = new ConcurrentStack<InfoHash>();
        private static ConcurrentHashSet<string> downlaodedSet = new ConcurrentHashSet<string>();
        private static ConcurrentHashSet<long> badAddress = new ConcurrentHashSet<long>();

        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            var locker = new ManualResetEvent(false);
            var dhtClient = new DhtClient(53386);
            dhtClient.OnFindPeer += DhtClient_OnFindPeer;
            dhtClient.OnReceiveInfoHash += DhtClient_OnReceiveInfoHash;
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
                var downSize = 64;
                var list = new List<InfoHash>(downSize);
                var tasks = new LinkedList<Task>();
                while (true)
                {
                    if (!downLoadQueue.TryPop(out InfoHash info) || downlaodedSet.Contains(info.Value))
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    list.Add(info);
                    if (list.Count < downSize)
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
                    foreach (var infoItem in uniqueItems)
                    {
                        tasks.AddLast(DownloadBitTorrent(infoItem));
                        if (tasks.Count < 16)
                            continue;
                        await Task.WhenAll(tasks);
                        tasks.Clear();
                    }
                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks);
                        tasks.Clear();
                    }
                    list.Clear();
                }
            }, TaskCreationOptions.LongRunning);
            locker.WaitOne();
        }

        private static async Task DownloadBitTorrent(InfoHash infoItem)
        {
            if (downlaodedSet.Contains(infoItem.Value))
                return;
            foreach (var peer in infoItem.Peers)
            {
                try
                {
                    var longPeer = peer.ToInt64();
                    if (badAddress.Contains(longPeer))
                    {
                        continue;
                    }
                    using (var client = new WireClient(peer))
                    {
                        var meta = await client.GetMetaData(new Tancoder.Torrent.InfoHash(infoItem.Bytes));
                        if (meta == null)
                        {
                            badAddress.Add(longPeer);
                            continue;
                        }
                        downlaodedSet.Add(infoItem.Value);
                        var torrent = ParseBitTorrent(meta);
                        Console.WriteLine($"download {infoItem.Value} success");
                        await File.WriteAllTextAsync(Path.Combine("torrent", infoItem.Value + ".json"), torrent.ToJson());
                        return;
                    }
                }
                catch (Exception)
                {
                }
            }
        }


        private static async Task DhtClient_OnReceiveInfoHash(InfoHash infoHash)
        {
            infoHash.IsDown = downlaodedSet.Contains(infoHash.Value);
            await File.AppendAllTextAsync($"hash{Thread.CurrentThread.ManagedThreadId}.txt", infoHash.Value + Environment.NewLine);
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

        private static Torrent ParseBitTorrent(BEncodedDictionary metaData)
        {
            var torrent = new Torrent();
            if (metaData.ContainsKey("name"))
            {
                torrent.Name = ((BEncodedString)metaData["name"]).Text;
            }
            if (metaData.ContainsKey("length"))
            {
                torrent.FileSize = ((BEncodedNumber)metaData["length"]).Number;
            }
            if (metaData.ContainsKey("files"))
            {
                var files = (BEncodedList)metaData["files"];
                torrent.Files = new TorrentFile[files.Count];
                for (int j = 0; j < files.Count; j++)
                {
                    var file = (BEncodedDictionary)files[j];
                    var item = new TorrentFile
                    {
                        FileSize = ((BEncodedNumber)file["length"]).Number,
                        Name = string.Join('/', ((BEncodedList)file["path"]).Select(path => ((BEncodedString)path).Text))
                    };
                    torrent.Files[j] = item;
                }
            }
            return torrent;
        }

    }


    class Torrent
    {
        public string InfoHash { get; set; }
        public string Name { get; set; }
        public long FileSize { get; set; }
        public IList<TorrentFile> Files { get; set; }
    }

    class TorrentFile
    {
        public long FileSize { get; set; }
        public string Name { get; set; }
    }
}
