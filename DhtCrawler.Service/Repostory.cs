using System.Collections.Generic;
using System.Threading.Tasks;
using DhtCrawler.Service.Model;
using MongoDB.Driver;

namespace DhtCrawler.Service
{
    public abstract class BaseRepository<T> where T : BaseModel<string>
    {
        protected IMongoCollection<T> _collection;

        protected abstract string CollectionName { get; }

        protected BaseRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<T>(CollectionName);
        }

        public async Task Add(T list)
        {
            await _collection.InsertOneAsync(list);
        }

        public async Task Add(IList<T> list)
        {
            await _collection.InsertManyAsync(list);
        }

        public async Task<bool> Exists(T item)
        {
            return await _collection.CountAsync(it => it.Id == item.Id, new CountOptions() { Limit = 1 }) > 0;
        }
    }
}
