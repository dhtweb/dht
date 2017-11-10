using DhtCrawler.Service.Model;
using MongoDB.Driver;

namespace DhtCrawler.Service
{
    public class InfoHashRepository : BaseRepository<InfoHashModel>
    {
        public InfoHashRepository(IMongoDatabase database) : base(database)
        {
        }

        protected override string CollectionName => "InfoHash";
    }
}
