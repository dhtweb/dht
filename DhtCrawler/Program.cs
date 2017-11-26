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
using BitTorrent.MonoTorrent.BEncoding;
using DhtCrawler.BitTorrent;
using log4net;
using log4net.Config;
using DhtCrawler.Common;
using DhtCrawler.Common.Collections;
using DhtCrawler.Common.Utils;
using DhtCrawler.Configuration;
using DhtCrawler.Store;

namespace DhtCrawler
{
    class Program
    {
        private static readonly ConcurrentQueue<string> InfoHashQueue = new ConcurrentQueue<string>();
        private static readonly BlockingCollection<InfoHash> DownLoadQueue = new BlockingCollection<InfoHash>(1024);
        private static readonly ConcurrentHashSet<string> DownlaodedSet = new ConcurrentHashSet<string>();
        private static readonly ConcurrentDictionary<long, DateTime> BadAddress = new ConcurrentDictionary<long, DateTime>();
        private static readonly StoreManager<InfoHash> InfoStore = new StoreManager<InfoHash>("infohash.store");
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        private static readonly ILog watchLog = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private const string TorrentPath = "torrent";
        private const string InfoPath = "info";
        private static readonly string DownloadInfoPath = Path.Combine(TorrentPath, "downloaded.txt");
        static void Main(string[] args)
        {
            Init();
            var locker = new ManualResetEvent(false);
            var dhtSection = ConfigurationManager.Default.GetSection("DhtConfig");
            var dhtConfig = new DhtConfig()
            {
                NodeQueueMaxSize = dhtSection.GetInt("NodeQueueMaxSize", 10240),
                Port = (ushort)dhtSection.GetInt("Port"),
                ProcessThreadNum = dhtSection.GetInt("ProcessThreadNum", 1),
                SendQueueMaxSize = dhtSection.GetInt("SendQueueMaxSize", 20480),
                SendRateLimit = dhtSection.GetInt("SendRateLimit", 150),
                ReceiveRateLimit = dhtSection.GetInt("ReceiveRateLimit", 150),
                ReceiveQueueMaxSize = dhtSection.GetInt("ReceiveRateLimit", 20480)
            };
            var dhtClient = new DhtClient(dhtConfig);
            dhtClient.OnFindPeer += DhtClient_OnFindPeer;
            dhtClient.OnReceiveInfoHash += DhtClient_OnReceiveInfoHash;
            dhtClient.Run();
            Console.CancelKeyPress += (sender, e) =>
            {
                dhtClient.ShutDown();
                locker.Set();
                e.Cancel = true;
            };
            Task.Factory.StartNew(() =>
            {
                int downSize = ConfigurationManager.Default.GetInt("BatchDownSize", 256), paralleDownSize = ConfigurationManager.Default.GetInt("MaxDownThreadNum", 16);
                var list = new List<InfoHash>(downSize);
                var parallel = new ParallelOptions() { MaxDegreeOfParallelism = paralleDownSize, TaskScheduler = TaskScheduler.Current };
                while (true)
                {
                    try
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
                            Thread.Sleep(1000);
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
                             }).Select(p => new { Bytes = result.Bytes, Value = result.Value, Peer = p });
                         }).OrderBy(it => it.Bytes[rand.Next(it.Bytes.Length)]);
                        Parallel.ForEach(uniqueItems, parallel, item =>
                             {
                                 if (DownlaodedSet.Contains(item.Value))
                                     return;
                                 var longPeer = item.Peer.ToInt64();
                                 try
                                 {
                                     if (BadAddress.TryGetValue(longPeer, out var expireTime))
                                     {
                                         if (expireTime > DateTime.Now)
                                             return;
                                         BadAddress.TryRemove(longPeer, out expireTime);
                                     }
                                     Console.WriteLine($"downloading {item.Value} from {item.Peer}");
                                     using (var client = new WireClient(item.Peer))
                                     {
                                         var meta = client.GetMetaData(new global::BitTorrent.InfoHash(item.Bytes), out var rawBytes);
                                         if (meta == null)
                                         {
                                             BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddHours(1), (ip, before) => DateTime.Now.AddHours(1));
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
                                     BadAddress.AddOrUpdate(longPeer, DateTime.Now.AddHours(1), (ip, before) => DateTime.Now.AddHours(1));
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
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        var directory = new DirectoryInfo(TorrentPath);
                        var files = directory.GetFiles("*.json");
                        foreach (var fg in files.GroupBy(f => f.LastWriteTime.Date))
                        {
                            var subdirectory = directory.CreateSubdirectory(fg.Key.ToString("yyyy-MM-dd"));
                            foreach (var fileInfo in fg)
                            {
                                var target = Path.Combine(subdirectory.FullName, fileInfo.Name);
                                if (File.Exists(target))
                                {
                                    fileInfo.Delete();
                                    continue;
                                }
                                fileInfo.MoveTo(target);
                                await File.AppendAllTextAsync(DownloadInfoPath, Path.GetFileNameWithoutExtension(fileInfo.Name) + Environment.NewLine);
                            }
                        }
                        await Task.Delay(TimeSpan.FromHours(1));
                    }
                    catch (Exception ex)
                    {
                        log.Error("move file error", ex);
                    }
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
            Console.WriteLine($"get {arg.Value} peers success");
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
