using System.Data;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service
{
    public abstract class BaseRepository<T, TId> where T : BaseModel<TId>
    {

        protected IDbConnection Connection { get; }

        protected BaseRepository(IDbConnection connection)
        {
            this.Connection = connection;
        }

    }
}
