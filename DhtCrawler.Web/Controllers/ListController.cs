using System.Linq;
using System.Threading.Tasks;
using DhtCrawler.Common.Index.Utils;
using DhtCrawler.Common.Web.Model;
using DhtCrawler.Service;
using DhtCrawler.Service.Index;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace DhtCrawler.Web.Controllers
{
    public class ListController : Controller
    {
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
            var list = _indexSearchService.GetList(index, 20, out int count, keyword);
            return View(new PageModel<InfoHashModel>() { PageIndex = index, PageSize = 20, Total = count, List = list });
        }

        public async Task<IActionResult> Detail(string hash)
        {
            var item = await _infoHashRepository.GetInfoHashDetail(hash);
            if (item == null)
            {
                return RedirectToAction("List");
            }
            item.KeyWords = new HashSet<string>(item.Name.CutForSearch().Where(k => k.Length > 1));//.Take(10)
            return View(item);
        }
    }
}
