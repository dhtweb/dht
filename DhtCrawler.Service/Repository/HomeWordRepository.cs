using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service.Repository
{
    public class HomeWordRepository : BaseRepository
    {

        public HomeWordRepository(DbFactory factory) : base(factory)
        {
        }


        public async Task<IList<HomeWordModel>> GetHomeWordListAsync()
        {
            var sql = "SELECT * FROM t_home_word;";
            return (await Connection.QueryAsync<HomeWordModel>(sql)).ToArray();
        }
    }
}
