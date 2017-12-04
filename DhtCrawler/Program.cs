using DhtCrawler.DHT;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitTorrent.MonoTorrent.BEncoding;
using DhtCrawler.BitTorrent;
using log4net;
using log4net.Config;
using DhtCrawler.Common;
using DhtCrawler.Common.Collections;
using DhtCrawler.Common.Utils;
using DhtCrawler.Configuration;
using DhtCrawler.Store;
using BitTorrentClient = BitTorrent.Listeners.BitTorrentClient;

namespace DhtCrawler
{
    class Program
    {
        private class DownInfoHash
        {
            public string Value { get; set; }
            public byte[] Bytes { get; set; }
            public IPEndPoint Peer { get; set; }
        }

        private static ConcurrentQueue<string> InfoHashQueue;
        private static ConcurrentQueue<Torrent> WriteTorrentQueue;
        private static BlockingCollection<InfoHash> DownLoadQueue;
        private static ConcurrentHashSet<string> DownlaodedSet;
        private static ConcurrentDictionary<long, DateTime> BadAddress;
        private static StoreManager<InfoHash> InfoStore;
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        private static readonly ILog watchLog = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private const string TorrentPath = "torrent";
        private const string InfoPath = "info";
        private static readonly string DownloadInfoPath = Path.Combine(TorrentPath, "downloaded.txt");
        private static volatile bool running = true;
        static void Main(string[] args)
        {
            InitDownInfo();
            EnsureDirectory();
            InitLog();
            LoadDownInfoHash();
            RunSpider();
            RunDown();
            RunWriteTorrent();
            RunRecordInfoHash();
            WaitComplete();
        }

        private static IEnumerable<DownInfoHash> GetDownInfo(int batchSize)
        {
            var list = new List<InfoHash>();
            while (true)
            {
                while (true)
                {
                    if (!DownLoadQueue.TryTake(out var info))
                    {
                        if (InfoStore.CanRead)
                        {
                            info = InfoStore.ReadLast();
                        }
                    }
                    if (info == null)
                    {
                        if (list.Count <= 0)
                        {
                            info = DownLoadQueue.Take();
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (DownlaodedSet.Contains(info.Value))
                        continue;
                    list.Add(info);
                    if (list.Count > batchSize)
                    {
                        break;
                    }
                }
                var downItems = list.GroupBy(l => l.Value).SelectMany(gl =>
                {
                    var result = gl.First();
                    foreach (var hash in gl.Skip(1))
                    {
                        foreach (var point in hash.Peers)
                        {
                            result.Peers.Add(point);
                        }
                    }
                    return result.Peers.Where(point =>
                    {
                        if (!point.Address.IsPublic())
                            return false;
                        var longPeer = point.Address.ToInt64();
                        if (BadAddress.TryGetValue(longPeer, out var expireTime))
                        {
                            if (expireTime > DateTime.Now)
                                return false;
                            BadAddress.TryRemove(longPeer, out expireTime);
                        }
                        return true;
                    }).Select(p => new DownInfoHash { Bytes = result.Bytes, Value = result.Value, Peer = p });
                }).OrderBy(t => t.Peer.Address.ToInt64());
                foreach (var downItem in downItems)
                {
                    yield return downItem;
                }
            }
        }

        private static IList<DownInfoHash> GetBatchDownInfo(int batchSize)
        {
            var list = new List<InfoHash>();
            while (true)
            {
                if (!DownLoadQueue.TryTake(out var info))
                {
                    if (InfoStore.CanRead)
                    {
                        info = InfoStore.ReadLast();
                    }
                }
                if (info == null)
                {
                    break;
                }
                if (DownlaodedSet.Contains(info.Value))
                    continue;
                list.Add(info);
                if (list.Count > batchSize)
                {
                    break;
                }
            }
            if (list.Count <= 0)
            {
                return new DownInfoHash[0];
            }
            return list.GroupBy(l => l.Value).SelectMany(gl =>
            {
                var result = gl.First();
                foreach (var hash in gl.Skip(1))
                {
                    foreach (var point in hash.Peers)
                    {
                        result.Peers.Add(point);
                    }
                }
                return result.Peers.Where(point =>
                {
                    if (!point.Address.IsPublic())
                        return false;
                    var longPeer = point.Address.ToInt64();
                    if (BadAddress.TryGetValue(longPeer, out var expireTime))
                    {
                        if (expireTime > DateTime.Now)
                            return false;
                        BadAddress.TryRemove(longPeer, out expireTime);
                    }
                    return true;
                }).Select(p => new DownInfoHash { Bytes = result.Bytes, Value = result.Value, Peer = p });
            }).OrderBy(t => t.Peer.Address.ToInt64()).ToArray();
        }

        static async Task LoopDownload(int taskId)
        {
            while (running)
            {
                try
                {
                    if (!DownLoadQueue.TryTake(out var info))
                    {
                        if (InfoStore.CanRead)
                        {
                            info = InfoStore.ReadLast();
                        }
                    }
                    if (info == null)
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    if (DownlaodedSet.Contains(info.Value))
                        continue;
                    Console.WriteLine($"task {taskId} downloading {info.Value}");
                    foreach (var peer in info.Peers)
                    {
                        if (!running)
                            return;
                        await DownTorrentAsync(new DownInfoHash() { Peer = peer, Bytes = info.Bytes, Value = info.Value });
                    }
                }
                catch (Exception ex)
                {
                    log.Error("并行下载时错误", ex);
                }
            }
        }

        static async Task DownTorrentAsync(DownInfoHash infoHash)
        {
            var longPeer = infoHash.Peer.ToInt64();
            try
            {
                if (BadAddress.TryGetValue(longPeer, out var expireTime))
                {
                    if (expireTime > DateTime.Now)
                    {
                        return;
                    }
                    BadAddress.TryRemove(longPeer, out expireTime);
                }
                using (var client = new BitTorrentClient(infoHash.Peer))
                {
                    var meta = await client.GetMetaDataAsync(new global::BitTorrent.InfoHash(infoHash.Bytes));
                    if (meta.Item1 == null)
                    {
                        if (meta.Item2)
                        {
                            BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddHours(1),
                                (ip, before) => DateTime.Now.AddHours(1));
                        }
                        return;
                    }
                    DownlaodedSet.Add(infoHash.Value);
                    var torrent = ParseBitTorrent(meta.Item1);
                    torrent.InfoHash = infoHash.Value;
                    WriteTorrentQueue.Enqueue(torrent);
                }
            }
            catch (SocketException)
            {
                BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddHours(1), (ip, before) => DateTime.Now.AddHours(1));
            }
            catch (IOException)
            {
                BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddHours(1), (ip, before) => DateTime.Now.AddHours(1));
            }
            catch (Exception ex)
            {
                log.Error("下载失败", ex);
            }

        }

        private static Task DhtClient_OnAnnouncePeer(InfoHash arg)
        {
            if (DownlaodedSet.Contains(arg.Value))
                return Task.CompletedTask;
            if (!DownLoadQueue.TryAdd(arg))
                InfoStore.Add(arg);
            return Task.CompletedTask;
        }

        private static void InitDownInfo()
        {
            InfoHashQueue = new ConcurrentQueue<string>();
            WriteTorrentQueue = new ConcurrentQueue<Torrent>();
            DownLoadQueue = new BlockingCollection<InfoHash>(ConfigurationManager.Default.GetInt("BufferDownSize", 5120));
            DownlaodedSet = new ConcurrentHashSet<string>();
            BadAddress = new ConcurrentDictionary<long, DateTime>();
            InfoStore = new StoreManager<InfoHash>("infohash.store");
        }

        private static void EnsureDirectory()
        {
            Directory.CreateDirectory(TorrentPath);
            Directory.CreateDirectory(InfoPath);
        }

        private static void InitLog()
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => { watchLog.Error(e.ExceptionObject); };
        }

        private static void LoadDownInfoHash()
        {
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

        private static void RunSpider()
        {
            var dhtSection = ConfigurationManager.Default.GetSection("DhtConfig");
            var dhtConfig = new DhtConfig()
            {
                NodeQueueMaxSize = dhtSection.GetInt("NodeQueueMaxSize", 10240),
                Port = (ushort)dhtSection.GetInt("Port"),
                ProcessThreadNum = dhtSection.GetInt("ProcessThreadNum", 1),
                SendQueueMaxSize = dhtSection.GetInt("SendQueueMaxSize", 20480),
                SendRateLimit = dhtSection.GetInt("SendRateLimit", 150),
                ReceiveRateLimit = dhtSection.GetInt("ReceiveRateLimit", 150),
                ReceiveQueueMaxSize = dhtSection.GetInt("ReceiveQueueMaxSize", 20480),
                KTableSize = dhtSection.GetInt("KTableSize", 8192)
            };
            var dhtClient = new DhtClient(dhtConfig);
            dhtClient.OnFindPeer += DhtClient_OnFindPeer;
            dhtClient.OnReceiveInfoHash += DhtClient_OnReceiveInfoHash;
            dhtClient.OnAnnouncePeer += DhtClient_OnAnnouncePeer;
            dhtClient.Run();
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    watchLog.Info($"收到消息数:{dhtClient.ReceviceMessageCount},发送消息数:{dhtClient.SendMessageCount},响应消息数:{dhtClient.ResponseMessageCount},待查找节点数:{dhtClient.FindNodeCount},待下载InfoHash数:{DownLoadQueue.Count},堆积的infoHash数:{InfoStore.Count}");
                    await Task.Delay(60 * 1000);
                }
            }, TaskCreationOptions.LongRunning);
            Console.CancelKeyPress += (sender, e) =>
            {
                dhtClient.ShutDown();
                e.Cancel = true;
            };
        }

        private static void RunDown()
        {
            int parallelDownSize = ConfigurationManager.Default.GetInt("MaxDownThreadNum", 32);//downSize = ConfigurationManager.Default.GetInt("BatchDownSize", 128),
            var downTasks = new Task[parallelDownSize];
            for (int i = 0; i < parallelDownSize; i++)
            {
                var local = i;
                downTasks[local] = Task.Run(async () =>
                {
                    await LoopDownload(local);
                });
            }
            Console.CancelKeyPress += (sender, e) =>
            {
                running = false;
                Task.WaitAll(downTasks);
                while (DownLoadQueue.TryTake(out var item))
                {
                    InfoStore.Add(item);
                }
            };
        }

        private static void RunWriteTorrent()
        {
            Task.Factory.StartNew(async () =>
            {
                var directory = new DirectoryInfo(TorrentPath);
                while (running)
                {
                    while (WriteTorrentQueue.TryDequeue(out var item))
                    {
                        var subdirectory = directory.CreateSubdirectory(DateTime.Now.ToString("yyyy-MM-dd"));
                        var path = Path.Combine(subdirectory.FullName, item.InfoHash + ".json");
                        await File.WriteAllTextAsync(Path.Combine(TorrentPath, path), item.ToJson());
                        await File.AppendAllTextAsync(DownloadInfoPath, item.InfoHash + Environment.NewLine);
                        Console.WriteLine($"download {item.InfoHash} success");
                    }
                    await Task.Delay(500);
                }
                while (WriteTorrentQueue.TryDequeue(out var item))
                {
                    var subdirectory = directory.CreateSubdirectory(DateTime.Now.ToString("yyyy-MM-dd"));
                    var path = Path.Combine(subdirectory.FullName, item.InfoHash + ".json");
                    await File.WriteAllTextAsync(Path.Combine(TorrentPath, path), item.ToJson());
                    await File.AppendAllTextAsync(DownloadInfoPath, item.InfoHash + Environment.NewLine);
                    Console.WriteLine($"download {item.InfoHash} success");
                }
            });
        }

        private static void RunRecordInfoHash()
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var count = 0;
                    var content = new StringBuilder();
                    while (InfoHashQueue.TryDequeue(out var info) && count < 1000)
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
        }

        private static void WaitComplete()
        {
            var locker = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                locker.Set();
                Environment.Exit(0);
            };
            locker.WaitOne();
        }

        private static Task DhtClient_OnReceiveInfoHash(InfoHash infoHash)
        {
            infoHash.IsDown = DownlaodedSet.Contains(infoHash.Value);
            InfoHashQueue.Enqueue(infoHash.Value);
            return Task.CompletedTask;
        }

        private static Task DhtClient_OnFindPeer(InfoHash arg)
        {
            if (DownlaodedSet.Contains(arg.Value))
                return Task.CompletedTask;
            if (!DownLoadQueue.TryAdd(arg))
                InfoStore.Add(arg);
            return Task.CompletedTask;
        }

        private static Torrent ParseBitTorrent(BEncodedDictionary metaData)
        {
            var torrent = new Torrent();
            if (metaData.ContainsKey("name.utf-8"))
            {
                torrent.Name = ((BEncodedString)metaData["name.utf-8"]).Text;
            }
            else if (metaData.ContainsKey("name"))
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
                    var filePaths = file.ContainsKey("path.utf-8") ? ((BEncodedList)file["path.utf-8"]).Select(path => ((BEncodedString)path).Text).ToArray() : ((BEncodedList)file["path"]).Select(path => ((BEncodedString)path).Text).ToArray();
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
                            if (i == l && (!path.StartsWith("_____padding_file_") && !path.EndsWith("或以上版本____")))
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
                    else if (filePaths.Length == 1)
                    {
                        if (filePaths[0].StartsWith("_____padding_file_") && filePaths[0].EndsWith("或以上版本____"))
                            continue;
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
