using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service.Repository
{
    public class SearchWordRepository : BaseRepository<SearchWordModel, string>
    {

        public SearchWordRepository(DbFactory factory) : base(factory)
        {
        }


        private async Task<bool> InsertOrUpdateAsync(SearchWordModel model, IDbTransaction transaction)
        {
            var sql = "INSERT INTO t_search_history AS t (word, num, last_search_time) VALUES (@word,@num,@time) ON CONFLICT(word) DO UPDATE SET num=t.num + @num ,last_search_time=@time;";
            return await Connection.ExecuteAsync(sql, new { word = model.Word, time = model.SearchTime, num = model.Num }, transaction) > 0;
        }

        public Task<bool> InsertOrUpdateAsync(SearchWordModel model)
        {
            return InsertOrUpdateAsync(model, null);
        }


        public async Task<bool> InsertOrUpdateAsync(IEnumerable<SearchWordModel> models)
        {
            if (Connection.State != ConnectionState.Open)
            {
                Connection.Open();
            }
            var trans = Connection.BeginTransaction();
            try
            {
                foreach (var model in models)
                {
                    await InsertOrUpdateAsync(model, trans);
                }
                trans.Commit();
                return true;
            }
            catch (Exception ex)
            {
                trans.Rollback();
                return false;
            }
            finally
            {
                trans.Dispose();
            }

        }
    }
}
