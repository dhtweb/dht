using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service.Repository
{
    public class VisitedHistoryRepository : BaseRepository
    {

        public VisitedHistoryRepository(DbFactory factory) : base(factory)
        {
        }

        private async Task<bool> InsertOrUpdateAsync(VisitedModel model, IDbTransaction transaction)
        {
            var sql = "INSERT INTO t_visit_history (infohash_id, user_id, visit_time) VALUES (@hashId,@userId,current_timestamp) ON CONFLICT (infohash_id,user_id) DO UPDATE SET visit_time=current_timestamp;";
            return await Connection.ExecuteAsync(sql, new { userId = model.UserId, hashId = model.HashId }, transaction) > 0;
        }
        public Task<bool> InsertOrUpdateAsync(VisitedModel model)
        {
            return InsertOrUpdateAsync(model, null);
        }

        public async Task<bool> InsertOrUpdateAsync(IEnumerable<VisitedModel> models)
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
                log.Error("添加失败", ex);
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
