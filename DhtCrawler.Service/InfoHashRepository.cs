using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DhtCrawler.Service.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DhtCrawler.Service
{
    public class InfoHashRepository : BaseRepository<InfoHashModel>
    {
        public InfoHashRepository(IMongoDatabase database) : base(database)
        {
        }

        protected override string CollectionName => "InfoHash";

        public async Task<bool> InsertOrUpdate(InfoHashModel model)
        {
            var list = new List<UpdateDefinition<InfoHashModel>>
            {
                Builders<InfoHashModel>.Update.Set(t => t.IsDown, model.IsDown),
                Builders<InfoHashModel>.Update.Set(t => t.UpdateTime, DateTime.Now),
                Builders<InfoHashModel>.Update.Inc(t => t.DownNum, model.DownNum),
                Builders<InfoHashModel>.Update.Inc(t => t.FileNum, model.FileNum),
                Builders<InfoHashModel>.Update.Inc(t => t.FileSize, model.FileSize),
            };
            if (model.Name != null)
                list.Add(Builders<InfoHashModel>.Update.Set(t => t.Name, model.Name));
            if (model.Files != null && model.Files.Count > 0)
                list.Add(Builders<InfoHashModel>.Update.AddToSetEach(t => t.Files, model.Files));
            await _collection.UpdateOneAsync(t => t.InfoHash == model.InfoHash, Builders<InfoHashModel>.Update.Combine(list), new UpdateOptions() { IsUpsert = true });
            return true;
        }
    }
}
