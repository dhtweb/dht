using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service.Repository
{
    public class KeyWordRepository : BaseRepository<KeyWordModel, string>
    {

        public KeyWordRepository(DbFactory factory) : base(factory)
        {
        }


        public async Task<bool> InsertOrUpdateAsync(KeyWordModel model, IDbTransaction transaction)
        {
            var sql = "INSERT INTO t_keyword AS t (word, num, isdanger) VALUES (@word,@num,@danger) ON CONFLICT(word) DO UPDATE SET num=t.num + excluded.num ,isdanger=excluded.isdanger;";
            return await SqlMapper.ExecuteAsync(Connection, sql, new { word = model.Word, num = model.Num, danger = model.IsDanger }, transaction) > 0;
        }

        public async Task<bool> InsertOrUpdateAsync(IEnumerable<KeyWordModel> models)
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
