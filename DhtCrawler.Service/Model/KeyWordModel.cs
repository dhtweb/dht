using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DhtCrawler.Service.Model
{
    public class KeyWordModel : BaseModel<string>
    {
        public override string Id { get; set; }
        public string KeyWord { get; set; }


    }
}
