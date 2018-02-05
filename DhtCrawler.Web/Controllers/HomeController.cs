using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DhtCrawler.Common;
using Microsoft.AspNetCore.Mvc;
using DhtCrawler.Web.Models;
using DhtCrawler.Service.Model;
using DhtCrawler.Service.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using log4net;

namespace DhtCrawler.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly InfoHashRepository _infoHashRepository;
        private readonly IMemoryCache _cache;
        private static ILog log = LogManager.GetLogger(typeof(HomeController));
        public HomeController(InfoHashRepository infoHashRepository, IMemoryCache cache)
        {
            this._infoHashRepository = infoHashRepository;
            _cache = cache;
        }
        public async Task<IActionResult> Index()
        {
            ViewBag.TorrentNum = await _cache.GetOrCreateAsync("total", entry =>
             {
                 entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                 return _infoHashRepository.GetTorrentNumAsync();
             });
            return View();
        }

        public async Task Contact(string sourcePath)
        {
            Response.ContentType = "text/plain";
            if (sourcePath == null || !Directory.Exists(sourcePath))
            {
                await Response.WriteAsync("");
                return;
            }
            var queue = new Queue<string>(new[] { sourcePath });
            var set = new HashSet<string>();
            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var dirs = Directory.GetDirectories(dir);
                foreach (var dirPath in dirs)
                {
                    queue.Enqueue(dirPath);
                }
                var files = Directory.GetFiles(dir, "*.json");
                var size = 0;
                var list = new List<KeyValuePair<InfoHashModel, string>>();
                foreach (var file in files)
                {
                    var dicInfo = await System.IO.File.ReadAllTextAsync(file, Encoding.UTF8);
                    var model = dicInfo.ToObjectFromJson<InfoHashModel>();
                    if (model == null)
                    {
                        continue;
                    }
                    model.CreateTime = System.IO.File.GetLastWriteTime(file);
                    model.IsDown = true;
                    if (model.Files != null && model.Files.Any(f => f.Name.IndexOf('/') > -1))
                    {
                        var metaFiles = model.Files;
                        model.Files = new List<TorrentFileModel>();
                        foreach (var metaFile in metaFiles)
                        {
                            var paths = metaFile.Name.Split('/');
                            if (paths.Length <= 1)
                            {
                                model.Files.Add(metaFile);
                            }
                            else
                            {
                                var rootFiles = model.Files;
                                for (var i = 0; i < paths.Length; i++)
                                {
                                    var path = paths[i];
                                    var parent = rootFiles.FirstOrDefault(f => f.Name == path);
                                    if (parent == null)
                                    {
                                        parent = new TorrentFileModel() { Name = path, Files = new List<TorrentFileModel>() };
                                        rootFiles.Add(parent);
                                    }
                                    if (i == paths.Length - 1)
                                    {
                                        parent.Files = null;
                                        parent.FileSize = metaFile.FileSize;
                                    }
                                    rootFiles = parent.Files;
                                }
                            }
                        }

                    }
                    if (string.IsNullOrWhiteSpace(model.InfoHash) || set.Contains(model.InfoHash))
                    {
                        continue;
                        // model.InfoHash = Path.GetFileNameWithoutExtension(file);
                    }
                    set.Add(model.InfoHash);
                    list.Add(new KeyValuePair<InfoHashModel, string>(model, file));
                    if (list.Count > 200)
                    {
                        await Response.WriteAsync((++size) + Environment.NewLine);
                        var flag = await _infoHashRepository.InsertOrUpdateAsync(list.Select(kv => kv.Key));
                        if (flag)
                        {
                            foreach (var kv in list)
                            {
                                System.IO.File.Delete(kv.Value);
                            }
                        }
                        list.Clear();
                    }
                }
                if (list.Count > 0)
                {
                    await Response.WriteAsync((++size) + Environment.NewLine);
                    var flag = await _infoHashRepository.InsertOrUpdateAsync(list.Select(kv => kv.Key));
                    if (flag)
                    {
                        foreach (var kv in list)
                        {
                            System.IO.File.Delete(kv.Value);
                        }
                    }
                }
            }
        }


        public IActionResult ImportInfo(string importPath, int num = 1000)
        {
            if (Directory.Exists(importPath))
            {
                num = Math.Abs(num);
                Task.Factory.StartNew(async () =>
                {
                    var files = Directory.GetFiles(importPath, "*.txt");
                    var dic = new Dictionary<string, int>();
                    foreach (var filePath in files)
                    {
                        using (var reader = System.IO.File.OpenText(filePath))
                        {
                            do
                            {
                                var line = await reader.ReadLineAsync();
                                if (string.IsNullOrWhiteSpace(line))
                                    break;
                                var key = line;
                                var n = 1;
                                if (line.IndexOf(':') > -1)
                                {
                                    var info = line.Split(':');
                                    key = info[0];
                                    n = int.Parse(info[1]);
                                }
                                if (dic.ContainsKey(key))
                                {
                                    dic[key] += n;
                                }
                                else
                                {
                                    dic[key] = n;
                                }
                            } while (true);
                        }
                        var size = 0;
                        var list = new LinkedList<InfoHashModel>();
                        foreach (var kv in dic)
                        {
                            list.AddLast(new InfoHashModel() { InfoHash = kv.Key, DownNum = kv.Value });
                            if (list.Count > num)
                            {
                                var flag = await _infoHashRepository.InsertOrUpdateAsync(list);
                                if (flag)
                                {
                                    size += list.Count;
                                    list.Clear();
                                    log.InfoFormat("已导入{0},完成比例{1:F2}", size, size * 1.0 / dic.Count);
                                }
                            }
                        }
                        if (list.Count > 0)
                        {
                            while (true)
                            {
                                var flag = await _infoHashRepository.InsertOrUpdateAsync(list);
                                if (flag)
                                {
                                    size += list.Count;
                                    log.InfoFormat("已导入{0},完成比例{1:F2}", size, size * 1.0 / dic.Count);
                                    break;
                                }
                            }
                        }
                        list.Clear();
                        dic.Clear();
                        System.IO.File.Delete(filePath);
                    }
                }, TaskCreationOptions.LongRunning);
            }
            return new EmptyResult();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult DownFile(string path)
        {
            if (System.IO.File.Exists(path))
            {
                return File(path, "application/octet-stream");
            }
            return Content("");
        }
    }
}
