using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DhtCrawler.Web.Models;
using DhtCrawler.Common;
using DhtCrawler.Common.Mvc.Result;
using DhtCrawler.Service;
using DhtCrawler.Service.Model;

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

        public async Task<IActionResult> Contact()
        {
            var files = Directory.GetFiles("info");
            var set = new Dictionary<string, int>();
            foreach (var file in files)
            {
                var lines = await System.IO.File.ReadAllLinesAsync(file);
                foreach (var line in lines)
                {
                    if (set.ContainsKey(line))
                    {
                        set[line]++;
                    }
                    else
                    {
                        set[line] = 1;
                    }
                }
            }
            foreach (var kv in set)
            {
                await _infoHashRepository.InsertOrUpdate(new InfoHashModel() { CreateTime = DateTime.Now, DownNum = kv.Value, InfoHash = kv.Key });
            }
            return Content(set.ToJson());
            //return View();
        }

        public IActionResult DownTorrent()
        {
            //Response.ContentType = "application/octet-stream";
            //Response.Headers["Content-Disposition"] = "attachment;filename=test.zip";
            //var files = Directory.GetFiles(@"E:\Code\dotnetcore\dht\DhtCrawler\bin\Release\PublishOutput\torrent");
            //using (var zip = new ZipArchive(Response.Body, ZipArchiveMode.Create))
            //{
            //    foreach (var filePath in files)
            //    {
            //        var file = zip.CreateEntry(Path.GetFileName(filePath));
            //        using (var fileStream = file.Open())
            //        {
            //            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            //            await fileStream.WriteAsync(bytes, 0, bytes.Length);
            //            await fileStream.FlushAsync();
            //        }
            //    }
            //}
            return new ZipResult(@"E:\Code\dotnetcore\dht\DhtCrawler\bin\Release\PublishOutput\torrent", "torrent.zip");
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
