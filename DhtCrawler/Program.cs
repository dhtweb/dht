using DhtCrawler.DHT;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BitTorrent.Listeners;
using DhtCrawler.BitTorrent;
using DhtCrawler.Collections;
using DhtCrawler.Utils;
using log4net;
using log4net.Config;
using Tancoder.Torrent.BEncoding;
using DhtCrawler.Common;
using DhtCrawler.Store;
using BitTorrentClient = BitTorrent.Listeners.BitTorrentClient;

namespace DhtCrawler
{
    class Program
    {
        private static readonly ConcurrentStack<InfoHash> DownLoadQueue = new ConcurrentStack<InfoHash>();
        private static readonly ConcurrentHashSet<string> DownlaodedSet = new ConcurrentHashSet<string>();
        private static readonly ConcurrentHashSet<long> BadAddress = new ConcurrentHashSet<long>();
        private static readonly StoreManager<InfoHash> InfoStore = new StoreManager<InfoHash>("infohash.store");
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        private static readonly ILog watchLog = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        static void Main(string[] args)
        {
            Init();
            var locker = new ManualResetEvent(false);
            var config = File.Exists("dhtconfig.json") ? File.ReadAllText("dhtconfig.json").ToObject<DhtConfig>() : DhtConfig.Default;
            var dhtClient = new DhtClient(config);
            dhtClient.OnFindPeer += DhtClient_OnFindPeer;
            dhtClient.OnReceiveInfoHash += DhtClient_OnReceiveInfoHash;
            dhtClient.Run();
            Console.CancelKeyPress += (sender, e) =>
            {
                dhtClient.ShutDown();
                locker.Set();
                e.Cancel = true;
            };
            Task.Factory.StartNew(async () =>
            {
                var downSize = 256;
                var list = new List<InfoHash>(downSize);
                while (true)
                {
                    try
                    {
                        if (!DownLoadQueue.TryPop(out InfoHash info))
                        {
                            if (InfoStore.CanRead)
                            {
                                info = InfoStore.ReadLast();
                            }
                            else
                            {
                                await Task.Delay(500);
                                continue;
                            }
                        }
                        if (DownlaodedSet.Contains(info.Value))
                            continue;
                        list.Add(info);
                        if (list.Count < downSize)
                        {
                            continue;
                        }
                        var uniqueItems = list.GroupBy(l => l.Value).SelectMany(gl =>
                         {
                             var result = gl.First();
                             foreach (var hash in gl.Skip(1))
                             {
                                 foreach (var point in hash.Peers)
                                 {
                                     result.Peers.Add(point);
                                 }
                             }
                             return result.Peers.Where(point => point.Address.IsPublic() && !BadAddress.Contains(point.ToInt64())).Select(p => new { Bytes = result.Bytes, Value = result.Value, Peer = p });
                         }).OrderBy(it => it.Peer);
                        Parallel.ForEach(uniqueItems, new ParallelOptions() { MaxDegreeOfParallelism = 32, TaskScheduler = TaskScheduler.Current }, item =>
                             {
                                 if (DownlaodedSet.Contains(item.Value))
                                     return;
                                 var longPeer = item.Peer.ToInt64();
                                 try
                                 {
                                     if (BadAddress.Contains(longPeer))
                                     {
                                         return;
                                     }
                                     Console.WriteLine($"downloading {item.Value} from {item.Peer}");
                                     using (var client = new WireClient(item.Peer))
                                     {
                                         var meta = client.GetMetaData(new Tancoder.Torrent.InfoHash(item.Bytes));
                                         if (meta == null)
                                         {
                                             BadAddress.Add(longPeer);
                                             return;
                                         }
                                         DownlaodedSet.Add(item.Value);
                                         var torrent = ParseBitTorrent(meta);
                                         torrent.InfoHash = item.Value;
                                         Console.WriteLine($"download {item.Value} success");
                                         File.WriteAllText(Path.Combine("torrent", item.Value + ".json"), torrent.ToJson());
                                     }
                                 }
                                 catch (SocketException ex)
                                 {
                                     BadAddress.Add(longPeer);
                                     log.Error("下载失败", ex);
                                 }
                                 catch (Exception ex)
                                 {
                                     log.Error("下载失败", ex);
                                 }
                             });
                        list.Clear();
                    }
                    catch (Exception ex)
                    {
                        log.Error("循环下载时错误", ex);
                    }
                }
            }, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    watchLog.Info($"收到消息数：{dhtClient.ReceviceMessageCount},发送消息数:{dhtClient.SendMessageCount},响应消息数:{dhtClient.ResponseMessageCount},待查找节点数:{dhtClient.FindNodeCount},待下载InfoHash数：{DownLoadQueue.Count},堆积的infoHash数：{InfoStore.Count}");
                    ThreadPool.GetAvailableThreads(out var workThreadNum, out var ioThreadNum);
                    watchLog.Info($"工作线程数：{workThreadNum},IO线程数:{ioThreadNum}");
                    await Task.Delay(60 * 1000);
                }
            }, TaskCreationOptions.LongRunning);
            locker.WaitOne();
        }

        private static void Init()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => { watchLog.Error(e.ExceptionObject); };
            Directory.CreateDirectory("torrent");
            Directory.CreateDirectory("info");
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            foreach (var file in Directory.GetFiles("torrent"))
            {
                DownlaodedSet.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        private static async Task DownloadBitTorrent(InfoHash infoItem)
        {
            if (DownlaodedSet.Contains(infoItem.Value))
                return;
            foreach (var peer in infoItem.Peers)
            {
                try
                {
                    var longPeer = peer.ToInt64();
                    if (BadAddress.Contains(longPeer))
                    {
                        continue;
                    }
                    Console.WriteLine($"downloading {infoItem.Value} from {peer}");
                    using (var client = new BitTorrentClient(peer))
                    {
                        var meta = await client.GetMetaDataAsync(new Tancoder.Torrent.InfoHash(infoItem.Bytes));
                        if (meta == null)
                        {
                            BadAddress.Add(longPeer);
                            continue;
                        }
                        DownlaodedSet.Add(infoItem.Value);
                        var torrent = ParseBitTorrent(meta);
                        torrent.InfoHash = infoItem.Value;
                        Console.WriteLine($"download {infoItem.Value} success");
                        await File.WriteAllTextAsync(Path.Combine("torrent", infoItem.Value + ".json"), torrent.ToJson());
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.Error("下载失败", ex);
                }
            }
        }

        private static async Task DhtClient_OnReceiveInfoHash(InfoHash infoHash)
        {
            infoHash.IsDown = DownlaodedSet.Contains(infoHash.Value);
            await File.AppendAllTextAsync(Path.Combine("info", $"hash{Thread.CurrentThread.ManagedThreadId}.txt"), infoHash.Value + Environment.NewLine);
        }

        private static Task DhtClient_OnFindPeer(InfoHash arg)
        {
            if (DownlaodedSet.Contains(arg.Value))
                return Task.CompletedTask;
            if (DownLoadQueue.Count > 1)
            {
                InfoStore.Add(arg);
            }
            else
            {
                DownLoadQueue.Push(arg);
            }
            Console.WriteLine($"get {arg.Value} peers success");
            return Task.CompletedTask;
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
}
