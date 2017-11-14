﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DhtCrawler.Service.Model;
using MongoDB.Driver;

namespace DhtCrawler.Service
{
    public class InfoHashRepository : BaseRepository<InfoHashModel, string>
    {
        public InfoHashRepository(IMongoDatabase database) : base(database)
        {
        }

        protected override string CollectionName => "InfoHash";

        public async Task<bool> InsertOrUpdate(InfoHashModel model)
        {
            var list = new List<UpdateDefinition<InfoHashModel>> { Builders<InfoHashModel>.Update.Set(t => t.UpdateTime, DateTime.Now) };
            if (model.CreateTime != default(DateTime))
                list.Add(Builders<InfoHashModel>.Update.Set(t => t.CreateTime, model.CreateTime));
            if (model.IsDown)
                list.Add(Builders<InfoHashModel>.Update.Set(t => t.IsDown, model.IsDown));
            if (model.DownNum > 0)
                list.Add(Builders<InfoHashModel>.Update.Inc(t => t.DownNum, model.DownNum));
            if (model.FileNum > 0)
                list.Add(Builders<InfoHashModel>.Update.Inc(t => t.FileNum, model.FileNum));
            if (model.FileSize > 0)
                list.Add(Builders<InfoHashModel>.Update.Inc(t => t.FileSize, model.FileSize));
            if (model.Name != null)
                list.Add(Builders<InfoHashModel>.Update.Set(t => t.Name, model.Name));
            if (model.Files != null && model.Files.Count > 0)
                list.Add(Builders<InfoHashModel>.Update.AddToSetEach(t => t.Files, model.Files));
            var result = await _collection.UpdateOneAsync(t => t.Id == model.InfoHash, Builders<InfoHashModel>.Update.Combine(list), new UpdateOptions() { IsUpsert = true });
            return result.IsAcknowledged;
        }
    }
}
