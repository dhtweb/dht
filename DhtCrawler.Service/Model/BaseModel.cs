using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace DhtCrawler.Service.Model
{
    public abstract class BaseModel<TId>
    {
        [BsonIgnore]
        public abstract TId Id { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}
