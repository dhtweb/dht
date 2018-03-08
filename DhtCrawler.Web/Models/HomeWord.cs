using System.Collections.Generic;

namespace DhtCrawler.Web.Models
{
    public class HomeWord
    {
        public string TypeName { get; set; }
        public IList<string> Words { get; set; }
    }
}