using System;

namespace DhtCrawler.Service.Model
{
    public abstract class BaseModel<TId>
    {
        public abstract TId Id { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}
