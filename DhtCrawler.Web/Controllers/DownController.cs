using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Web.Controllers
{
    public class DownController : Controller
    {

        public IActionResult Index(string path)
        {
            if (System.IO.File.Exists(path))
            {
                return File(path, "application/octet-stream");
            }
            return Content("");
        }
    }
}
