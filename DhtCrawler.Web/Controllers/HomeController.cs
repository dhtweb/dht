using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DhtCrawler.Web.Models;
using DhtCrawler.Common;
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

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
