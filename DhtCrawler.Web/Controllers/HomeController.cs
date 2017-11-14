using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DhtCrawler.Web.Models;
using DhtCrawler.Common;
using DhtCrawler.Common.Web.Mvc.Result;
using DhtCrawler.Service;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.Language.Extensions;

namespace DhtCrawler.Web.Controllers
{
    public class HomeController : Controller
    {
        private InfoHashRepository _infoHashRepository;

        public HomeController(InfoHashRepository infoHashRepository)
        {
            this._infoHashRepository = infoHashRepository;
        }
        public IActionResult Index()
        {
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
            var files = Directory.GetFiles(@"J:\down\info");
            foreach (var file in files)
            {
                var dicInfo = await System.IO.File.ReadAllTextAsync(file, Encoding.UTF8);
                var set = dicInfo.ToObject<Dictionary<string, int>>();
                var taskList = new List<Task>();
                var items = new BlockingCollection<KeyValuePair<string, int>>();
                foreach (var kv in set)
                {
                    items.Add(kv);
                }
                for (int i = 0; i < 10; i++)
                {
                    await Response.WriteAsync(file + Environment.NewLine);
                    taskList.Add(Task.Run(async () =>
                    {
                        while (true)
                        {
                            if (!items.TryTake(out var info))
                                return;
                            await _infoHashRepository.InsertOrUpdate(new InfoHashModel() { CreateTime = DateTime.Now, DownNum = info.Value, InfoHash = info.Key });
                            await Response.WriteAsync(info.Value + Environment.NewLine);
                        }
                    }));
                }
                await Task.WhenAll(taskList);
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
