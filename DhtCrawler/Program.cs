using DhtCrawler.DHT;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitTorrent.Listeners;
using DhtCrawler.BitTorrent;
using DhtCrawler.Utils;
using log4net;
using log4net.Config;
using Tancoder.Torrent.BEncoding;
using DhtCrawler.Common;
using DhtCrawler.Common.Collections;
using DhtCrawler.Store;
using BitTorrentClient = BitTorrent.Listeners.BitTorrentClient;

namespace DhtCrawler
{
    class Program
    {
        private static readonly BlockingCollection<string> InfoHashQueue = new BlockingCollection<string>();
        private static readonly ConcurrentStack<InfoHash> DownLoadQueue = new ConcurrentStack<InfoHash>();
        private static readonly ConcurrentHashSet<string> DownlaodedSet = new ConcurrentHashSet<string>();
        private static readonly ConcurrentHashSet<long> BadAddress = new ConcurrentHashSet<long>();
        private static readonly StoreManager<InfoHash> InfoStore = new StoreManager<InfoHash>("infohash.store");
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        private static readonly ILog watchLog = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private const int DownMetaMaxSize = 1024 * 30;
        private const string TorrentPath = "torrent";
        private const string InfoPath = "info";
        private static readonly string DownloadInfoPath = Path.Combine(TorrentPath, "downloaded.txt");
        static void Main(string[] args)
        {
            Init();
            var locker = new ManualResetEvent(false);
            var config = File.Exists("dhtconfig.json") ? File.ReadAllText("dhtconfig.json").ToObject<DhtConfig>() : new DhtConfig();
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
                var parallel = new ParallelOptions() { MaxDegreeOfParallelism = 16, TaskScheduler = TaskScheduler.Current };
                while (true)
                {
                    try
                    {

                        while (true)
                        {
                            if (!DownLoadQueue.TryPop(out var info))
                            {
                                if (InfoStore.CanRead)
                                {
                                    info = InfoStore.ReadLast();
                                }
                            }
                            if (info == null)
                                break;
                            if (DownlaodedSet.Contains(info.Value))
                                continue;
                            list.Add(info);
                            if (list.Count > downSize)
                            {
                                break;
                            }
                        }
                        if (list.Count <= 0)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                        var rand = new Random();
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
                         }).OrderBy(it => it.Bytes[rand.Next(it.Bytes.Length)]);
                        Parallel.ForEach(uniqueItems, parallel, item =>
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
                                         lock (DownLoadQueue)
                                         {
                                             File.WriteAllText(Path.Combine(TorrentPath, item.Value + ".json"), torrent.ToJson());
                                         }
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
                    watchLog.Info($"收到消息数:{dhtClient.ReceviceMessageCount},发送消息数:{dhtClient.SendMessageCount},响应消息数:{dhtClient.ResponseMessageCount},待查找节点数:{dhtClient.FindNodeCount},待下载InfoHash数:{DownLoadQueue.Count},堆积的infoHash数:{InfoStore.Count}");
                    await Task.Delay(60 * 1000);
                }
            }, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var count = 0;
                    var content = new StringBuilder();
                    while (InfoHashQueue.TryTake(out var info) && count < 1000)
                    {
                        content.Append(info).Append(Environment.NewLine);
                        count++;
                    }
                    if (count > 0)
                    {
                        await File.AppendAllTextAsync(Path.Combine(InfoPath, DateTime.Now.ToString("yyyy-MM-dd") + ".txt"), content.ToString());
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }
            }, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var directory = new DirectoryInfo(TorrentPath);
                    var files = directory.GetFiles("*.json");
                    foreach (var fg in files.GroupBy(f => f.LastWriteTime.Date))
                    {
                        var subdirectory = directory.CreateSubdirectory(fg.Key.ToString("yyyy-MM-dd"));
                        foreach (var fileInfo in fg)
                        {
                            fileInfo.MoveTo(Path.Combine(subdirectory.FullName, fileInfo.Name));
                            await File.AppendAllTextAsync(DownloadInfoPath, Path.GetFileNameWithoutExtension(fileInfo.Name) + Environment.NewLine);
                        }
                    }
                    await Task.Delay(TimeSpan.FromHours(1));
                }
            }, TaskCreationOptions.LongRunning);
            locker.WaitOne();
        }

        private static void Init()
        {
            Directory.CreateDirectory(TorrentPath);
            Directory.CreateDirectory(InfoPath);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => { watchLog.Error(e.ExceptionObject); };
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            var queue = new Queue<string>(new[] { TorrentPath });
            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                foreach (var directory in Directory.GetDirectories(dir))
                {
                    queue.Enqueue(directory);
                }
                foreach (var file in Directory.GetFiles(dir))
                {
                    DownlaodedSet.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            if (!File.Exists(DownloadInfoPath))
                return;
            foreach (var line in File.ReadAllLines(DownloadInfoPath))
            {
                DownlaodedSet.Add(line);
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

        private static Task DhtClient_OnReceiveInfoHash(InfoHash infoHash)
        {
            infoHash.IsDown = DownlaodedSet.Contains(infoHash.Value);
            InfoHashQueue.Add(infoHash.Value);
            return Task.CompletedTask;
        }

        private static Task DhtClient_OnFindPeer(InfoHash arg)
        {
            if (DownlaodedSet.Contains(arg.Value))
                return Task.CompletedTask;
            if (DownLoadQueue.Count > DownMetaMaxSize)
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
                torrent.Files = new List<TorrentFile>();
                for (int j = 0; j < files.Count; j++)
                {
                    var file = (BEncodedDictionary)files[j];
                    var filePaths = ((BEncodedList)file["path"]).Select(path => ((BEncodedString)path).Text).ToArray();
                    var fileSize = ((BEncodedNumber)file["length"]).Number;
                    if (filePaths.Length > 1)
                    {
                        var directory = torrent.Files.FirstOrDefault(f => f.Name == filePaths[0]);
                        if (directory == null)
                        {
                            directory = new TorrentFile() { Name = filePaths[0], Files = new List<TorrentFile>() };
                            torrent.Files.Add(directory);
                        }
                        for (int i = 1, l = filePaths.Length - 1; i < filePaths.Length; i++)
                        {
                            var path = filePaths[i];
                            if (i == l)
                            {
                                var fileItem = new TorrentFile() { Name = path, FileSize = fileSize };
                                directory.Files.Add(fileItem);
                            }
                            else
                            {
                                var newDirectory = directory.Files.FirstOrDefault(f => f.Name == path);
                                if (newDirectory == null)
                                {
                                    newDirectory = new TorrentFile() { Name = path, Files = new List<TorrentFile>() };
                                    directory.Files.Add(newDirectory);
                                }
                                directory = newDirectory;
                            }
                        }
                    }
                    else
                    {
                        var item = new TorrentFile
                        {
                            FileSize = fileSize,
                            Name = filePaths[0]
                        };
                        torrent.Files.Add(item);
                    }
                }
            }
            return torrent;
        }

    }
}
