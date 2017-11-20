using System;
using System.Text;
using System.Threading.Tasks;
using DhtCrawler.Common.Web.Model;
using DhtCrawler.Service;
using DhtCrawler.Service.Index;
using DhtCrawler.Service.Model;
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

        public IActionResult BuildAll()
        {
            _indexSearchService.ReBuildIndex(it =>
            {
                var data = Encoding.UTF8.GetBytes(it.InfoHash + Environment.NewLine);
                Response.Body.Write(data, 0, data.Length);
            });
            return new EmptyResult();
        }
    }
}
