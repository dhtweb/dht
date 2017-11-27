using System;
using System.Linq;
using System.Threading.Tasks;
using DhtCrawler.Common.Index.Utils;
using DhtCrawler.Common.Web.Model;
using DhtCrawler.Service;
using DhtCrawler.Service.Index;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text;
using DhtCrawler.Common.Web.Mvc.Filter;

namespace DhtCrawler.Web.Controllers
{
    public class ListController : Controller
    {
        private const int PageSize = 15;
        private InfoHashRepository _infoHashRepository;
        private IndexSearchService _indexSearchService;
        public ListController(InfoHashRepository infoHashRepository, IndexSearchService indexSearchService)
        {
            _infoHashRepository = infoHashRepository;
            _indexSearchService = indexSearchService;
        }

        public IActionResult List(string keyword, int index = 1)
        {
            ViewBag.SearchKey = keyword;
            var list = _indexSearchService.GetList(index, PageSize, out int count, keyword);
            return View(new PageModel<InfoHashModel>() { PageIndex = index, PageSize = PageSize, Total = count, List = list });
        }

        [ServiceFilter(typeof(StaticHtmlFilterAttribute), IsReusable = true)]
        public async Task<IActionResult> Detail(string hash)
        {
            var item = await _infoHashRepository.GetInfoHashDetailAsync(hash);
            if (item == null)
            {
                return RedirectToAction("List");
            }
            item.KeyWords = new HashSet<string>(item.Name.Cut().Union(item.Name.Cut(false)).Where(k => k.Length > 1));//.Take(10)
            return View(item);
        }

        public async Task<IActionResult> Lastlist(DateTime date, int index = 1)
        {
            DateTime start = date.Date, end = date.Date.AddDays(1);
            if (index * PageSize > 10000)
            {
                index = 10000 / PageSize;
            }
            ViewBag.Date = date.ToString("yyyy-MM-dd");
            var result = await _infoHashRepository.GetInfoHashListAsync(index, PageSize, start, end);
            return View(new PageModel<InfoHashModel>() { PageIndex = index, PageSize = PageSize, Total = Math.Min((int)result.Count, 10000), List = result.List });
        }
    }
}
