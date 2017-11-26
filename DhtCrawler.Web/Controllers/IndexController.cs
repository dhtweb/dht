using System;
using System.Text;
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

        public IActionResult BuildAll()
        {
            var i = 0;
            _indexSearchService.ReBuildIndex(it =>
            {
                i++;
                if (i % 100 == 0)
                {
                    var codings = Encoding.UTF8.GetBytes(i.ToString() + Environment.NewLine);
                    Response.Body.Write(codings, 0, codings.Length);
                }
            });
            return new EmptyResult();
        }
    }
}
