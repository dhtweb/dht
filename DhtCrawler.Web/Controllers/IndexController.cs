using System;
using System.Text;
using System.Threading.Tasks;
using DhtCrawler.Service.Index;
using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Web.Controllers
{
    public class IndexController : Controller
    {
        private IndexSearchService _indexSearchService;
        public IndexController(IndexSearchService indexSearchService)
        {
            _indexSearchService = indexSearchService;
        }

        public IActionResult IncrementBuild(DateTime updateTime)
        {
            Task.Factory.StartNew(() =>
            {
                _indexSearchService.IncrementBuild(updateTime);
            }, TaskCreationOptions.LongRunning);
            return new EmptyResult();
        }

        public IActionResult MBuild()
        {
            var i = 0;
            Task.Factory.StartNew(() =>
            {
                _indexSearchService.MultipleThreadReBuildIndex();
            }, TaskCreationOptions.LongRunning);
            return Content("over");
        }
    }
}
