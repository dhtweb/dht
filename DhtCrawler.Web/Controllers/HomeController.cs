using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DhtCrawler.Web.Models;
using DhtCrawler.Common;

namespace DhtCrawler.Web.Controllers
{
    public class HomeController : Controller
    {
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
            ViewData["Message"] = "Your contact page.";
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
            return Content(set.ToJson());
            //return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
