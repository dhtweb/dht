using System.Threading.Tasks;
using DhtCrawler.Common.Web.Model;
using DhtCrawler.Service;
using DhtCrawler.Service.Index;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Mvc;

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

        public async Task<IActionResult> Index(string keyword, int index = 1)
        {
            ViewBag.SearchKey = keyword;
            var result = await _infoHashRepository.GetInfoHashList(index, 20);
            return View(new PageModel<InfoHashModel>() { PageIndex = index, PageSize = 20, Total = (int)result.Count, List = result.List });
        }

        public IActionResult Search(string keyword, int index = 1)
        {
            ViewBag.SearchKey = keyword;
            var list = _indexSearchService.GetList(index, 20, out int count, keyword);
            return View("Index", new PageModel<InfoHashModel>() { PageIndex = index, PageSize = 20, Total = count, List = list });
        }

        public async Task<IActionResult> Detail(string hash)
        {
            var item = await _infoHashRepository.GetInfoHashDetail(hash);
            if (item == null)
            {
                return RedirectToAction("index");
            }
            return View(item);
        }
    }
}
