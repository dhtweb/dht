using System.Collections.Generic;

namespace DhtCrawler.Common.Web.Model
{
    public class PageModel<T>
    {
        public IList<T> List { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int Pages
        {
            get
            {
                if (Total <= 0)
                    return 1;
                return (Total + PageSize - 1) / PageSize;
            }
        }

        public int PreIndex => PageIndex <= 1 ? 1 : PageIndex - 1;

        public int NextIndex
        {
            get
            {
                var pages = Pages;
                return PageIndex >= pages ? pages : PageIndex + 1;
            }
        }
    }
}
