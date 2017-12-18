﻿using System;
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
using DhtCrawler.Common.Web.Mvc.Static;
using Microsoft.Extensions.Caching.Memory;

namespace DhtCrawler.Web.Controllers
{
    public class ListController : Controller
    {
        private const int PageSize = 15;
        private readonly InfoHashRepository _infoHashRepository;
        private readonly IndexSearchService _indexSearchService;
        private readonly IMemoryCache _cache;
        public ListController(InfoHashRepository infoHashRepository, IndexSearchService indexSearchService, IMemoryCache cache)
        {
            _infoHashRepository = infoHashRepository;
            _indexSearchService = indexSearchService;
            _cache = cache;
        }

        public IActionResult List(string keyword, int index = 1)
        {
            ViewBag.SearchKey = keyword;
            var key = keyword.ToLower() + ":" + index;
            var page = _cache.GetOrCreate(key, entry =>
             {
                 entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                 var list = _indexSearchService.GetList(index, PageSize, out int count, keyword);
                 return new PageModel<InfoHashModel>()
                 {
                     PageIndex = index,
                     PageSize = PageSize,
                     Total = count,
                     List = list
                 };
             });
            return View(page);
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
            if (index > 500)
            {
                index = 500;
            }
            ViewBag.Date = date.ToString("yyyy-MM-dd");
            var result = await _infoHashRepository.GetInfoHashListAsync(index, PageSize, start, end);
            return View(new PageModel<InfoHashModel>() { PageIndex = index, PageSize = PageSize, Total = Math.Min((int)result.Count, 500 * PageSize), List = result.List });
        }

        public async Task<IActionResult> Lastestlist(int index = 1)
        {
            var result = await _infoHashRepository.GetInfoHashListAsync(index, PageSize);
            return View("LastList", new PageModel<InfoHashModel>() { PageIndex = index, PageSize = PageSize, Total = Math.Min((int)result.Count, 500 * PageSize), List = result.List });
        }
    }
}
