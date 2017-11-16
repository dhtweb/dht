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
using DhtCrawler.Common.Web.Mvc.Result;
using DhtCrawler.Service;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Http;

namespace DhtCrawler.Web.Controllers
{
    public class HomeController : Controller
    {
        private InfoHashRepository _infoHashRepository;

        public HomeController(InfoHashRepository infoHashRepository)
        {
            this._infoHashRepository = infoHashRepository;
        }
        public async Task<IActionResult> Index()
        {
            ViewBag.TorrentNum = await _infoHashRepository.GetTorrentNum();
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public async Task Contact()
        {
            Response.ContentType = "text/plain";
            var queue = new Queue<string>(new[] { @"E:\Code\dotnetcore\dht\DhtCrawler\torrent" });
            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var dirs = Directory.GetDirectories(dir);
                foreach (var dirPath in dirs)
                {
                    queue.Enqueue(dirPath);
                }
                var files = Directory.GetFiles(dir, "*.json");
                foreach (var file in files)
                {
                    var dicInfo = await System.IO.File.ReadAllTextAsync(file, Encoding.UTF8);
                    try
                    {
                        var model = dicInfo.ToObject<InfoHashModel>();
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
                        await Response.WriteAsync(model.InfoHash + Environment.NewLine);
                        await _infoHashRepository.InsertOrUpdate(model);
                        //System.IO.File.Move(file, @"G:\torrent\" + Path.GetFileName(file));
                        System.IO.File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        await Response.WriteAsync(dicInfo + Environment.NewLine);
                    }
                    //foreach (var info in dicInfo)
                    //{
                    //    if (!set.ContainsKey(info))
                    //    {
                    //        set[info] = 0;
                    //    }
                    //    set[info]++;
                    //}
                    //foreach (var info in set)
                    //{
                    //    if (info.Value <= 1)
                    //        continue;
                    //    await _infoHashRepository.InsertOrUpdate(new InfoHashModel() { CreateTime = DateTime.Now, DownNum = info.Value, InfoHash = info.Key });
                    //    await Response.WriteAsync(info.Value + ",");
                    //}
                }
            }
        }


        public IActionResult DownTorrent()
        {
            return new ZipResult(@"E:\Code\dotnetcore\dht\DhtCrawler\bin\Release\PublishOutput\torrent", "torrent.zip");
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
