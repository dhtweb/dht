using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace DhtCrawler.Service
{
    public abstract class BaseRepository<T>
    {
        private IMongoCollection<T> _collection;

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

    }
}
