﻿using System;
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
using System.Globalization;

namespace DhtCrawler.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly InfoHashRepository _infoHashRepository;
        private readonly StatisticsInfoRepository statisticsInfoRepository;
        private readonly HomeWordRepository homeWordRepository;
        private readonly IMemoryCache _cache;
        private static ILog log = LogManager.GetLogger(typeof(HomeController));
        public HomeController(InfoHashRepository infoHashRepository, StatisticsInfoRepository statisticsInfoRepository, HomeWordRepository homeWordRepository, IMemoryCache cache)
        {
            this._infoHashRepository = infoHashRepository;
            this.statisticsInfoRepository = statisticsInfoRepository;
            this.homeWordRepository = homeWordRepository;
            _cache = cache;
        }
        public async Task<IActionResult> Index()
        {
            //var keys = new[] { "红海行动", "唐人街探案2", "捉妖记2", "西游记女儿国", "祖宗十九代", "熊出没·变形记", "黑豹", "闺蜜2", "比得兔", "爱在记忆消逝前", "妈妈咪鸭", "小萝莉的猴神大叔", "环太平洋：雷霆再起", "金钱世界", "古墓丽影：源起之战", "三块广告牌", "前任3：再见前任", "厉害了，我的国", "战神纪", "无问西东", "新哥斯拉", "芳华", "人怕出名猪怕壮", "南极之恋", "宇宙有爱浪漫同游", "奇迹男孩", "忌日快乐", "小马宝莉大电影", "复仇者联盟3：无限战争", "马戏之王" };
            var wordTask = _cache.GetOrCreateAsync("words", entry =>
           {
               return homeWordRepository.GetHomeWordListAsync().ContinueWith(t =>
               {
                   if (t.Result != null)
                   {
                       entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                       return t.Result.GroupBy(it => it.Order).OrderBy(kv => kv.Key).Select(kv =>
                       {
                           var item = kv.First();
                           return new HomeWord()
                           {
                               TypeName = item.Type,
                               Words = kv.OrderByDescending(m => m.Index).Select(w => w.Word).ToArray()
                           };
                       }).ToArray();
                   }
                   else
                   {
                       entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                       return new HomeWord[0];
                   }
               });
           });
            var torrentNumTask = _cache.GetOrCreateAsync("total", entry =>
             {
                 entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                 return statisticsInfoRepository.GetInfoById("TorrentNum").ContinueWith(t =>
                    {
                        if (t.Result == null)
                        {
                            return 0;
                        }
                        return t.Result.Num;
                    });
             });
            await Task.WhenAll(wordTask, torrentNumTask);
            ViewBag.TorrentNum = torrentNumTask.Result;
            ViewBag.HomeWord = wordTask.Result;
            return View();
        }

        public async Task Contact(string sourcePath, int num = 200)
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
                    }
                    set.Add(model.InfoHash);
                    list.Add(new KeyValuePair<InfoHashModel, string>(model, file));
                    if (list.Count > num)
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
                                var flag = await _infoHashRepository.BatchInsertOrUpdateAsync(list);
                                if (flag)
                                {
                                    size += list.Count;
                                    list.Clear();
                                    log.InfoFormat("已导入{0},完成比例{1:F4}", size, size * 1.0 / dic.Count);
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

        [ResponseCache(Duration = int.MaxValue, Location = ResponseCacheLocation.Any)]
        public IActionResult About()
        {
            ViewBag.Index = 0;
            return View();
        }

        [ResponseCache(Duration = int.MaxValue, Location = ResponseCacheLocation.Any)]
        public IActionResult Term()
        {
            ViewBag.Index = 1;
            return View();
        }

        [ResponseCache(Duration = int.MaxValue, Location = ResponseCacheLocation.Any)]
        public IActionResult Dmca()
        {
            ViewBag.Index = 2;
            return View();
        }
        public async Task<IActionResult> SiteMap()
        {
            var lastUpdateTime = await _infoHashRepository.GetLastInfoHashDownTimeAsync();
            var content = new StringBuilder();
            content.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            content.AppendFormat("<url><loc>{0}</loc><lastmod>{1}</lastmod><changefreq>hourly</changefreq><priority>0.8</priority></url>", "http://www.btcloudword.com/last", lastUpdateTime.ToString("yyyy-MM-ddThh:mm:sszzzz", DateTimeFormatInfo.InvariantInfo));
            content.Append("</urlset>");
            return Content(content.ToString(), "text/xml; charset=utf-8");
        }
    }
}
