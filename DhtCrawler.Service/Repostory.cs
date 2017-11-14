using System.Collections.Generic;
using System.Threading.Tasks;
using DhtCrawler.Service.Model;
using MongoDB.Driver;

namespace DhtCrawler.Service
{
    public abstract class BaseRepository<T, TId> where T : BaseModel<TId>
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

        public async Task<bool> Exists(TId key)
        {
            return await _collection.CountAsync(it => it.Id.Equals(key), new CountOptions() { Limit = 1 }) > 0;
        }
    }
}
