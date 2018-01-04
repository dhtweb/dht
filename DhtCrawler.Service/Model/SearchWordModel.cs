using System;

namespace DhtCrawler.Service.Model
{
    public class SearchWordModel : BaseModel<string>
    {
        public override string Id { get; set; }
        public string Word { get; set; }
        public int Num { get; set; }
        public DateTime SearchTime { get; set; }

    }
}
