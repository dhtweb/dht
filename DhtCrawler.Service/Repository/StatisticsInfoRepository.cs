using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service.Repository
{
    public class StatisticsInfoRepository : BaseRepository
    {

        public StatisticsInfoRepository(DbFactory factory) : base(factory)
        {
        }

        public async Task<StatisticsInfoModel> GetInfoById(string key)
        {
            var sql = "SELECT * FROM t_statistics_info WHERE datakey = @key;";
            return await Connection.QueryFirstOrDefaultAsync<StatisticsInfoModel>(sql, new { key });
        }

        public async Task<bool> InsertOrUpdateAsync(StatisticsInfoModel model)
        {
            var sql = "INSERT INTO t_statistics_info (datakey, num, updatetime) VALUES (@DataKey,@Num,@UpdateTime) ON CONFLICT (datakey) DO UPDATE SET num=@Num,updatetime=@UpdateTime;";
            return await Connection.ExecuteAsync(sql, model) > 0;
        }

        public bool InsertOrUpdate(StatisticsInfoModel model)
        {
            var sql = "INSERT INTO t_statistics_info (datakey, num, updatetime) VALUES (@DataKey,@Num,@UpdateTime) ON CONFLICT (datakey) DO UPDATE SET num=@Num,updatetime=@UpdateTime;";
            return Connection.Execute(sql, model) > 0;
        }
    }
}
