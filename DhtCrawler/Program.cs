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
using BitTorrent.Listeners;
using BitTorrent.MonoTorrent.BEncoding;
using DhtCrawler.BitTorrent;
using log4net;
using log4net.Config;
using DhtCrawler.Common;
using DhtCrawler.Common.Collections;
using DhtCrawler.Common.Compare;
using DhtCrawler.Common.Utils;
using DhtCrawler.Configuration;
using DhtCrawler.DHT.Message;
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
            Init();
            EnsureDirectory();
            InitLog();
            LoadDownInfoHash();
            RunSpider();
            RunDown();
            RunWriteTorrent();
            RunRecordInfoHash();
            WaitComplete();
        }

        #region BT种子下载
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
                        await Task.Delay(1000);
                        continue;
                    }
                    if (DownlaodedSet.Contains(info.Value))
                        continue;
                    Console.WriteLine($"task {taskId} downloading {info.Value}");
                    foreach (var peer in info.Peers.Where(p => p.Address.IsPublic()))
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
                            BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddDays(1),
                                (ip, before) => DateTime.Now.AddDays(1));
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
                BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddDays(1), (ip, before) => DateTime.Now.AddDays(1));
            }
            catch (IOException)
            {
                BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddDays(1), (ip, before) => DateTime.Now.AddDays(1));
            }
            catch (Exception ex)
            {
                log.Error("下载失败", ex);
            }
        }

        private static void RunDown()
        {
            var downMode = ConfigurationManager.Default.GetString("DownMode");
            if (string.Equals(downMode, "thread", StringComparison.OrdinalIgnoreCase))
            {
                DownByThread();
            }
            else
            {
                DownByTask();
            }
        }

        private static void DownByThread()
        {
            int parallelDownSize = ConfigurationManager.Default.GetInt("MaxDownThreadNum", 32);//, downSize = ConfigurationManager.Default.GetInt("BatchDownSize", 128)
            var tasks = new Task[parallelDownSize];
            for (int i = 0; i < parallelDownSize; i++)
            {
                var taskId = i;
                tasks[i] = Task.Factory.StartNew(() =>
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
                                Thread.Sleep(1000);
                                continue;
                            }
                            if (DownlaodedSet.Contains(info.Value))
                                continue;
                            Console.WriteLine($"thread {taskId} downloading {info.Value}");
                            foreach (var peer in info.Peers.Where(p => p.Address.IsPublic()))
                            {
                                if (!running)
                                    return;
                                if (DownlaodedSet.Contains(info.Value))
                                {
                                    break;
                                }
                                var longPeer = peer.ToInt64();
                                try
                                {
                                    if (BadAddress.TryGetValue(longPeer, out var expireTime))
                                    {
                                        if (expireTime > DateTime.Now)
                                        {
                                            continue;
                                        }
                                        BadAddress.TryRemove(longPeer, out expireTime);
                                    }
                                    using (var client = new WireClient(peer))
                                    {
                                        var meta = client.GetMetaData(new global::BitTorrent.InfoHash(info.Bytes), out var netError);
                                        if (meta == null)
                                        {
                                            if (netError)
                                            {
                                                BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddDays(1),
                                                    (ip, before) => DateTime.Now.AddDays(1));
                                            }
                                            continue;
                                        }
                                        DownlaodedSet.Add(info.Value);
                                        var torrent = ParseBitTorrent(meta);
                                        torrent.InfoHash = info.Value;
                                        WriteTorrentQueue.Enqueue(torrent);
                                    }
                                    break;
                                }
                                catch (SocketException)
                                {
                                    BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddDays(1), (ip, before) => DateTime.Now.AddDays(1));
                                }
                                catch (IOException)
                                {
                                    BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddDays(1), (ip, before) => DateTime.Now.AddDays(1));
                                }
                                catch (Exception ex)
                                {
                                    log.Error("下载失败", ex);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error("并行下载时错误", ex);
                        }
                    }
                }, TaskCreationOptions.LongRunning);
            }
            Console.CancelKeyPress += (sender, e) =>
            {
                Task.WaitAll(tasks);
            };
        }

        private static void DownByTask()
        {
            int parallelDownSize = ConfigurationManager.Default.GetInt("MaxDownThreadNum", 32);//downSize = ConfigurationManager.Default.GetInt("BatchDownSize", 128),
            var downTasks = new Task[parallelDownSize];
            for (int i = 0; i < parallelDownSize; i++)
            {
                var local = i;
                downTasks[local] = Task.Factory.StartNew(async () =>
                {
                    await LoopDownload(local);
                }, TaskCreationOptions.LongRunning);
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
        #endregion

        #region DHT事件

        private static Task DhtClient_OnAnnouncePeer(InfoHash arg)
        {
            if (DownlaodedSet.Contains(arg.Value))
                return Task.CompletedTask;
            if (!DownLoadQueue.TryAdd(arg))
                InfoStore.Add(arg);
            return Task.CompletedTask;
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

        #endregion

        private static void Init()
        {
            InfoHashQueue = new ConcurrentQueue<string>();
            WriteTorrentQueue = new ConcurrentQueue<Torrent>();
            DownLoadQueue = new BlockingCollection<InfoHash>(ConfigurationManager.Default.GetInt("BufferDownSize", 5120));
            DownlaodedSet = new ConcurrentHashSet<string>();
            BadAddress = new ConcurrentDictionary<long, DateTime>();
            InfoStore = new StoreManager<InfoHash>("infohash.store");
            var redisServer = ConfigurationManager.Default.GetString("redis.server");
            if (redisServer.IsBlank())
            {
                IocContainer.RegisterType<AbstractMessageMap>(new MessageMap(500));
                //IocContainer.RegisterType<AbstractMessageMap>(DefaultMessageMap.Instance);
            }
            else
            {
                IocContainer.RegisterType<AbstractMessageMap>(new RedisMessageMap(ConfigurationManager.Default.GetString("redis.server")));
            }
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
                KTableSize = dhtSection.GetInt("KTableSize", 1024),
                ProcessWaitSize = dhtSection.GetInt("ProcessWaitSize"),
                ProcessWaitTime = dhtSection.GetInt("ProcessWaitTime", 100)
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
                    watchLog.Info($"收到消息数:{dhtClient.ReceviceMessageCount},发送消息数:{dhtClient.SendMessageCount},响应消息数:{dhtClient.ResponseMessageCount},待查找节点数:{dhtClient.FindNodeCount},待记录InfoHash数:{InfoHashQueue.Count},待下载InfoHash数:{DownLoadQueue.Count},堆积的infoHash数:{InfoStore.Count},待写入磁盘种子数:{WriteTorrentQueue.Count}");
                    await Task.Delay(60 * 1000);
                }
            });
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    var infoHashFiles = Directory.GetFiles(InfoPath, "*.txt");
                    foreach (var hashFile in infoHashFiles)
                    {
                        using (var stream = File.OpenRead(hashFile))
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                while (reader.Peek() > 0)
                                {
                                    var line = reader.ReadLine();
                                    if (line.IsBlank() || DownlaodedSet.Contains(line))
                                    {
                                        continue;
                                    }
                                    while (dhtClient.SendMessageCount >= dhtConfig.SendQueueMaxSize)
                                    {
                                        Thread.Sleep(2000);
                                    }
                                    var hashBytes = new byte[20];
                                    for (var i = 0; i < hashBytes.Length; i++)
                                    {
                                        hashBytes[i] = Convert.ToByte(line.Substring(i * 2, 2), 16);
                                    }
                                    dhtClient.GetPeers(hashBytes);
                                }
                            }
                        }
                    }
                    Thread.Sleep(60000 * 60);
                }
            }, TaskCreationOptions.LongRunning);
            Console.CancelKeyPress += (sender, e) =>
            {
                dhtClient.ShutDown();
                e.Cancel = true;
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
            Task.Factory.StartNew(() =>
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
                        File.AppendAllText(Path.Combine(InfoPath, DateTime.Now.ToString("yyyy-MM-dd") + ".txt"), content.ToString());
                    }
                    else
                    {
                        Thread.Sleep(1000);
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
    }
}
