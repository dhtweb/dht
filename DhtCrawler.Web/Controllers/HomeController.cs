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
using DhtCrawler.Service;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace DhtCrawler.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly InfoHashRepository _infoHashRepository;
        private readonly IMemoryCache _cache;
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
                        Console.WriteLine(file);
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
                    if (string.IsNullOrWhiteSpace(model.InfoHash))
                    {
                        model.InfoHash = Path.GetFileNameWithoutExtension(file);
                    }
                    list.Add(new KeyValuePair<InfoHashModel, string>(model, file));
                    if (list.Count > 200)
                    {
                        await Response.WriteAsync((++size) + Environment.NewLine);
                        await _infoHashRepository.InsertOrUpdateAsync(list.Select(kv => kv.Key));
                        foreach (var kv in list)
                        {
                            System.IO.File.Delete(kv.Value);
                        }
                        list.Clear();
                    }
                }
                if (list.Count > 0)
                {
                    await Response.WriteAsync((++size) + Environment.NewLine);
                    await _infoHashRepository.InsertOrUpdateAsync(list.Select(kv => kv.Key));
                    foreach (var kv in list)
                    {
                        System.IO.File.Delete(kv.Value);
                    }
                }
            }
        }


        public async Task ImportInfo(string importPath)
        {
            Response.ContentType = "text/plain";
            if (!Directory.Exists(importPath))
            {
                await Response.WriteAsync("");
                return;
            }
            var files = Directory.GetFiles(importPath, "*.txt");
            var dic = new Dictionary<string, int>();
            foreach (var filePath in files)
            {
                var reader = System.IO.File.OpenText(filePath);
                do
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        break;
                    if (!dic.ContainsKey(line))
                    {
                        dic[line] = 0;
                    }
                    dic[line]++;
                } while (true);
                var list = new LinkedList<InfoHashModel>();
                foreach (var kv in dic)
                {
                    if (kv.Value <= 1)
                        continue;
                    list.AddLast(new InfoHashModel() { InfoHash = kv.Key, DownNum = kv.Value });
                    if (list.Count > 10000)
                    {
                        await _infoHashRepository.InsertOrUpdateAsync(list);
                        await Response.WriteAsync(kv.Key + Environment.NewLine);
                        list.Clear();
                    }
                }
                if (list.Count > 0)
                {
                    await _infoHashRepository.InsertOrUpdateAsync(list);
                }
                list.Clear();
                dic.Clear();
                reader.Close();
                System.IO.File.Delete(filePath);
            }
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
