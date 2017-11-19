using System.Threading.Tasks;
using DhtCrawler.Common.Web.Model;
using DhtCrawler.Service;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Web.Controllers
{
    public class ListController : Controller
    {
        private InfoHashRepository _infoHashRepository;
        public ListController(InfoHashRepository infoHashRepository)
        {
            _infoHashRepository = infoHashRepository;
        }

        public async Task<IActionResult> Index(string keyword, int index = 1)
        {
            ViewBag.SearchKey = keyword;
            var result = await _infoHashRepository.GetInfoHashList(index, 20);
            return View(new PageModel<InfoHashModel>() { PageIndex = index, PageSize = 20, Total = (int)result.Count, List = result.List });
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
