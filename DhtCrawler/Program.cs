﻿using DhtCrawler.DHT;
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
using DhtCrawler.Common.Filters;
using DhtCrawler.Common.Utils;
using DhtCrawler.Configuration;
using DhtCrawler.DHT.Message;
using DhtCrawler.Store;
using Npgsql;
using NpgsqlTypes;

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
        private static BlockingCollection<InfoHash> DownLoadQueue;
        private static ConcurrentHashSet<string> DownlaodedSet;
        private static ConcurrentDictionary<long, DateTime> BadAddress;
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        private static readonly ILog watchLog = LogManager.GetLogger(Assembly.GetEntryAssembly(), "watchLogger");
        private const string TorrentPath = "torrent";
        private static readonly DirectoryInfo TorrentDirectory = new DirectoryInfo(TorrentPath);
        private static SpinLock writeLock = new SpinLock(false);

        private const string InfoPath = "info";
        private static readonly string DownloadInfoPath = Path.Combine(TorrentPath, "downloaded.txt");
        private static volatile bool running = true;
        #region 下载信息
        private static ThreadSafeList<Task> DownTaskList;
        private static StoreManager<InfoHash> InfoStore;
        private static int MinDownTaskNum;
        private static int MaxDownTaskNum;
        private static int DownTimeOutSeconds;
        #endregion
        static void Main(string[] args)
        {
            Init();
            EnsureDirectory();
            InitLog();
            LoadDownInfoHash();
            RunSpider();
            RunDown();
            RunRecordInfoHash();
            SyncToDatabase().Wait();
            WaitComplete();
        }

        #region BT种子下载
        private static void RunDown()
        {
            MinDownTaskNum = ConfigurationManager.Default.GetInt("MinDownThreadNum", 1);
            MaxDownTaskNum = ConfigurationManager.Default.GetInt("MaxDownThreadNum", 32);
            DownTimeOutSeconds = ConfigurationManager.Default.GetInt("DownTimeout", 60) * 1000;
            DownTaskList = new ThreadSafeList<Task>();
            for (int i = 0; i < MinDownTaskNum; i++)
            {
                StartDownTorrent();
            }
            Console.CancelKeyPress += (sender, e) =>
            {
                Task.WaitAll(DownTaskList.ToArray());
            };
        }
        private static void StartDownTorrent()
        {
            if (DownTaskList.Count >= MaxDownTaskNum)
            {
                return;
            }
            Task downTask = null;
            downTask = Task.Factory.StartNew(t =>
            {
                var currentTask = t == null ? downTask : (Task)t;
                while (running)
                {
                    try
                    {
                        if (!DownLoadQueue.TryTake(out var info))
                        {
                            lock (InfoStore)
                            {
                                if (!DownLoadQueue.TryTake(out info))
                                {
                                    var infos = InfoStore.ReadLast(Math.Min(MaxDownTaskNum, DownLoadQueue.BoundedCapacity - DownLoadQueue.Count));
                                    if (infos.Count > 0)
                                    {
                                        for (int i = 0; i < infos.Count - 1; i++)
                                        {
                                            var item = infos[i];
                                            if (!DownLoadQueue.TryAdd(item))
                                            {
                                                InfoStore.Add(item);
                                            }
                                        }
                                        info = infos.Last();
                                    }
                                }
                            }
                        }
                        if (info == null)
                        {
                            if (!DownLoadQueue.TryTake(out info, DownTimeOutSeconds))
                            {
                                lock (DownTaskList.SyncRoot)
                                {
                                    if (DownTaskList.Count > MinDownTaskNum)
                                    {
                                        DownTaskList.Remove(currentTask);
                                        return;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                        if (DownlaodedSet.Contains(info.Value))
                            continue;
                        watchLog.Info($"thread {Task.CurrentId:x2} downloading {info.Value}");
                        foreach (var peer in info.Peers.Where(p => p.Address.IsPublic()))
                        {
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
                                    var subdirectory = TorrentDirectory.CreateSubdirectory(DateTime.Now.ToString("yyyy-MM-dd"));
                                    var path = Path.Combine(subdirectory.FullName, torrent.InfoHash + ".json");
                                    var hasLock = false;
                                    writeLock.Enter(ref hasLock);
                                    try
                                    {
                                        File.WriteAllText(Path.Combine(TorrentPath, path), torrent.ToJson());
                                        File.AppendAllText(DownloadInfoPath, torrent.InfoHash + Environment.NewLine);
                                    }
                                    finally
                                    {
                                        if (hasLock)
                                        {
                                            writeLock.Exit(false);
                                        }
                                    }
                                    watchLog.Info($"download {torrent.InfoHash} success");
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
            }, downTask, TaskCreationOptions.LongRunning);
            DownTaskList.Add(downTask);
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

        private static Torrent ParseBitTorrent(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var torrent = BEncodedDictionary.DecodeTorrent(stream);
                var it = ParseBitTorrent((BEncodedDictionary)torrent["info"]);
                using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
                {
                    var rawBytes = ((BEncodedDictionary)torrent["info"]).Encode();
                    byte[] infohash = sha1.ComputeHash(rawBytes);
                    it.InfoHash = infohash.ToHex();
                    return it;
                }
            }
        }
        #endregion

        #region DHT事件

        private static void DownInfoEnqueue(InfoHash arg)
        {
            if (!DownLoadQueue.TryAdd(arg))
            {
                InfoStore.Add(arg);
                StartDownTorrent();
            }
        }

        private static Task DhtClient_OnAnnouncePeer(InfoHash arg)
        {
            DownInfoEnqueue(arg);
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
            DownInfoEnqueue(arg);
            return Task.CompletedTask;
        }

        #endregion

        private static void Init()
        {
            InfoHashQueue = new ConcurrentQueue<string>();
            DownLoadQueue = new BlockingCollection<InfoHash>(ConfigurationManager.Default.GetInt("BufferDownSize", 5120));
            DownlaodedSet = new ConcurrentHashSet<string>();
            BadAddress = new ConcurrentDictionary<long, DateTime>();
            InfoStore = new StoreManager<InfoHash>("infohash.store");
            var redisServer = ConfigurationManager.Default.GetString("redis.server");
            if (redisServer.IsBlank())
            {
                var filterType = ConfigurationManager.Default.GetString("filter").ToLower();
                switch (filterType)
                {
                    case "hash":
                        IocContainer.RegisterType<IFilter<long>>(new HashFilter<long>());
                        break;
                    case "bloom":
                        IocContainer.RegisterType<IFilter<long>>(new BloomFilter<long>(2 << 24, 32, (seed, item) => (item << 16 | seed).GetHashCode() & int.MaxValue));
                        break;
                    default:
                        IocContainer.RegisterType<IFilter<long>>(new EmptyFilter<long>());
                        break;
                }
                //IocContainer.RegisterType<AbstractMessageMap>(new LocalFileMap(Path.Combine("msg", "data.bat")));
                IocContainer.RegisterType<AbstractMessageMap>(new MessageMap(15));
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
                ProcessResponseThreadNum = dhtSection.GetInt("ProcessResponseThreadNum", 1),
                ProcessRequestThreadNum = dhtSection.GetInt("ProcessRequestThreadNum", 1),
                SendQueueMaxSize = dhtSection.GetInt("SendQueueMaxSize", 20480),
                SendRateLimit = dhtSection.GetInt("SendRateLimit", 150),
                ReceiveRateLimit = dhtSection.GetInt("ReceiveRateLimit", 150),
                ReceiveQueueMaxSize = dhtSection.GetInt("ReceiveQueueMaxSize", 20480),
                RequestQueueMaxSize = dhtSection.GetInt("RequestQueueMaxSize", 20480),
                ResponseQueueMaxSize = dhtSection.GetInt("ResponseQueueMaxSize", 20480),
                KTableSize = dhtSection.GetInt("KTableSize", 1024)
            };
            var dhtClient = new DhtClient(dhtConfig);
            dhtClient.OnFindPeer += DhtClient_OnFindPeer;
            dhtClient.OnReceiveInfoHash += DhtClient_OnReceiveInfoHash;
            dhtClient.OnAnnouncePeer += DhtClient_OnAnnouncePeer;
            dhtClient.Run();
            Task.Run(async () =>
            {
                while (true)
                {
                    watchLog.Info($"收到消息数:{dhtClient.ReceviceMessageCount},收到请求消息数:{dhtClient.RequestMessageCount},收到回复消息数:{dhtClient.ResponseMessageCount},发送消息数:{dhtClient.SendMessageCount},回复消息数:{dhtClient.ReplyMessageCount},待查找节点数:{dhtClient.FindNodeCount},待记录InfoHash数:{InfoHashQueue.Count},待下载InfoHash数:{DownLoadQueue.Count},堆积的infoHash数:{InfoStore.Count},下载线程数:{DownTaskList.Count}");
                    await Task.Delay(60 * 1000);
                }
            });
            var reTryDown = ConfigurationManager.Default.GetBool("ReTryDownHash");
            if (reTryDown)
            {
                Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        var infoHashFiles = Directory.GetFiles(InfoPath, "*.retry");
                        foreach (var hashFile in infoHashFiles)
                        {
                            var allDown = true;
                            using (var stream = File.OpenRead(hashFile))
                            {
                                using (var reader = new StreamReader(stream))
                                {
                                    while (reader.Peek() > 0)
                                    {
                                        var line = reader.ReadLine();
                                        if (line.IsBlank())
                                        {
                                            break;
                                        }
                                        if (line.IndexOf(':') > 0)
                                        {
                                            line = line.Substring(0, 40);
                                        }
                                        if (DownlaodedSet.Contains(line))
                                        {
                                            continue;
                                        }
                                        var hashBytes = line.HexStringToByteArray();
                                        while (dhtClient.SendMessageCount >= dhtConfig.SendQueueMaxSize)
                                        {
                                            Thread.Sleep(2000);
                                        }
                                        dhtClient.GetPeers(hashBytes);
                                        allDown = false;
                                    }
                                }
                            }
                            if (allDown)
                            {
                                File.Delete(hashFile);
                            }
                        }
                        log.Info("LOOP COMPLETE");
                        Thread.Sleep(TimeSpan.FromMinutes(15));
                    }
                }, TaskCreationOptions.LongRunning);
            }
            Console.CancelKeyPress += (sender, e) =>
            {
                dhtClient.ShutDown();
                e.Cancel = true;
            };
        }

        private static async Task SyncToDatabase()
        {
            watchLog.Info("同步种子到数据库启动");
            while (running)
            {
                var queue = new Queue<string>(Directory.GetDirectories(TorrentPath));
                while (queue.Count > 0)
                {
                    var dir = queue.Dequeue();
                    foreach (var directory in Directory.GetDirectories(dir))
                    {
                        queue.Enqueue(directory);
                    }
                    var files = Directory.GetFiles(dir, "*.json");
                    if (files.Length == 0 && Directory.GetLastWriteTime(dir) <= DateTime.Now.AddHours(-6))
                    {
                        Directory.Delete(dir);
                        continue;
                    }
                    var conStr = ConfigurationManager.Default.GetString("conStr");
                    try
                    {
                        using (var con = new NpgsqlConnection(conStr))
                        {
                            await con.OpenAsync();
                            foreach (var file in files)
                            {
                                if (!running)
                                {
                                    return;
                                }
                                try
                                {
                                    var content = await File.ReadAllTextAsync(file);
                                    if (content.IsBlank())
                                    {
                                        File.Delete(file);
                                        continue;
                                    }
                                    var item = content.ToObjectFromJson<Torrent>();
                                    if (item == null)
                                    {
                                        continue;
                                    }
                                    var fileNum = 0;
                                    if (!item.Files.IsEmpty())
                                    {
                                        var fileQueue = new Queue<TorrentFile>(item.Files);
                                        while (fileQueue.Count > 0)
                                        {
                                            var tfile = fileQueue.Dequeue();
                                            if (tfile.Files.IsEmpty())
                                            {
                                                fileNum += 1;
                                            }
                                            else
                                            {
                                                foreach (var it in tfile.Files)
                                                {
                                                    fileQueue.Enqueue(it);
                                                }
                                            }
                                        }
                                    }
                                    if (con.State != System.Data.ConnectionState.Open)
                                    {
                                        await con.OpenAsync();
                                    }
                                    using (var transaction = con.BeginTransaction())
                                    {
                                        try
                                        {
                                            using (var insertHash = con.CreateCommand())
                                            {
                                                insertHash.CommandText = "INSERT INTO t_infohash AS ti (infohash, name, filenum, filesize, downnum, isdown, createtime,updatetime, hasfile) VALUES (@hash, @name, @filenum, @filesize, 1, TRUE, @createtime,@now,  @hasfile) ON CONFLICT  (infohash) DO UPDATE SET name=@name,filenum=@filenum,filesize=@filesize,isdown=TRUE,createtime=@createtime,updatetime=@now,hasfile=@hasfile RETURNING id;";
                                                insertHash.Transaction = transaction;
                                                insertHash.Parameters.Add(new NpgsqlParameter("hash", item.InfoHash));
                                                insertHash.Parameters.Add(new NpgsqlParameter("name", item.Name ?? ""));
                                                insertHash.Parameters.Add(new NpgsqlParameter("filenum", fileNum == 0 ? 1 : fileNum));
                                                insertHash.Parameters.Add(new NpgsqlParameter("filesize", item.FileSize));
                                                insertHash.Parameters.Add(new NpgsqlParameter("createtime", File.GetCreationTime(file)));
                                                insertHash.Parameters.Add(new NpgsqlParameter("now", DateTime.Now));
                                                insertHash.Parameters.Add(new NpgsqlParameter("hasfile", !item.Files.IsEmpty()));
                                                var hashId = Convert.ToInt64(await insertHash.ExecuteScalarAsync());
                                                if (!item.Files.IsEmpty())
                                                {
                                                    using (var insertFile = con.CreateCommand())
                                                    {
                                                        insertFile.CommandText = "INSERT INTO t_infohash_file (info_hash_id, files) VALUES (@hashId,@files) ON CONFLICT (info_hash_id) DO UPDATE SET files=@files;";
                                                        insertFile.Transaction = transaction;
                                                        insertFile.Parameters.Add(new NpgsqlParameter("hashId", hashId));
                                                        insertFile.Parameters.Add(new NpgsqlParameter("files", NpgsqlDbType.Jsonb) { Value = item.Files.ToJson() });
                                                        await insertFile.ExecuteNonQueryAsync();
                                                    }
                                                }
                                            }
                                            await transaction.CommitAsync();
                                        }
                                        catch (Exception ex)
                                        {
                                            await transaction.RollbackAsync();
                                            log.Error("添加种子信息到数据库失败", ex);
                                        }
                                    }
                                    File.Delete(file);
                                    watchLog.InfoFormat("文件{0}写入数据库成功", file);
                                }
                                catch (Exception ex)
                                {
                                    log.Error("添加种子信息到数据库失败，文件：" + file, ex);
                                }
                            }
                        }
                        watchLog.InfoFormat("文件夹写入数据库成功{0}", dir);
                    }
                    catch (Exception ex)
                    {
                        log.Error("打开数据库失败", ex);
                    }
                }
                await Task.WhenAll(UpdateDownNum(), SyncDownNumToDatabase(), Task.Delay(TimeSpan.FromHours(1)));
            }
        }

        private static async Task UpdateDownNum()
        {
            try
            {
                var conStr = ConfigurationManager.Default.GetString("conStr");
                using (var con = new NpgsqlConnection(conStr))
                {
                    await con.OpenAsync();
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE t_statistics_info SET num = (SELECT count(id) FROM t_infohash WHERE isdown=TRUE) WHERE datakey='TorrentNum';";
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("更新下载数失败", ex);
            }
        }

        private static async Task SyncDownNumToDatabase()
        {
            var files = Directory.GetFiles(InfoPath, "*.txt");
            var conStr = ConfigurationManager.Default.GetString("conStr");
            try
            {
                var info = new Dictionary<string, int>();
                using (var con = new NpgsqlConnection(conStr))
                {
                    await con.OpenAsync();
                    foreach (var file in files)
                    {
                        try
                        {
                            if (File.GetLastWriteTime(file) < DateTime.Now.AddHours(-1))
                            {
                                continue;
                            }
                            using (var reader = new StreamReader(File.OpenRead(file), Encoding.UTF8))
                            {
                                while (reader.Peek() > 0)
                                {
                                    var key = await reader.ReadLineAsync();
                                    if (key.IsBlank())
                                    {
                                        continue;
                                    }
                                    var size = 1;
                                    if (key.IndexOf(':') > 0)
                                    {
                                        var infos = key.Split(':');
                                        key = infos[0];
                                        size = int.Parse(infos[1]);
                                    }
                                    if (info.TryGetValue(key, out var num))
                                    {
                                        info[key] = num + size;
                                    }
                                    else
                                    {
                                        info[key] = size;
                                    }
                                }
                            }
                            using (var insertHash = con.CreateCommand())
                            {
                                foreach (var kv in info)
                                {
                                    try
                                    {
                                        insertHash.CommandText = "UPDATE t_infohash SET downnum = downnum+@downnum,updatetime=@now WHERE infohash=@hash;";
                                        insertHash.Parameters.Add(new NpgsqlParameter("hash", kv.Key));
                                        insertHash.Parameters.Add(new NpgsqlParameter("downnum", kv.Value));
                                        insertHash.Parameters.Add(new NpgsqlParameter("now", DateTime.Now));
                                        await insertHash.ExecuteScalarAsync();
                                        insertHash.Parameters.Clear();
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error("更新下载信息到数据库失败", ex);
                                    }
                                }
                            }
                            watchLog.InfoFormat("文件写入数据库成功{0}", file);
                            File.Delete(file);
                            info.Clear();
                        }
                        catch (Exception ex)
                        {
                            log.Error("更新下载信息到数据库失败，文件：" + file, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("打开数据库失败", ex);
            }
        }

        private static void RunRecordInfoHash()
        {
            Task.Factory.StartNew(() =>
            {
                var set = new Dictionary<string, int>();
                while (true)
                {
                    var content = new StringBuilder();
                    while (InfoHashQueue.TryDequeue(out var info) && set.Count < 1000)
                    {
                        if (!set.ContainsKey(info))
                        {
                            set[info] = 0;
                        }
                        set[info]++;
                    }
                    foreach (var kv in set)
                    {
                        content.AppendLine($"{kv.Key}:{kv.Value}");
                    }
                    if (set.Count > 0)
                    {
                        File.AppendAllText(Path.Combine(InfoPath, DateTime.Now.ToString("yyyy-MM-dd-HH") + ".txt"), content.ToString());
                        set.Clear();
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
