﻿namespace DhtCrawler.Service.Model
{
    public class VisitedModel : BaseModel<string>
    {
        public override string Id { get; set; }
        public string UserId { get; set; }
        public string Hash { get; set; }

    }
}
