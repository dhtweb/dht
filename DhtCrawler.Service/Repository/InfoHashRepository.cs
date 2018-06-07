using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Common;
using DhtCrawler.Common.Db;
using DhtCrawler.Common.Utils;
using DhtCrawler.Service.Maps;
using DhtCrawler.Service.Model;
using Npgsql;
using NpgsqlTypes;

namespace DhtCrawler.Service.Repository
{
    public class InfoHashRepository : BaseRepository
    {
        static InfoHashRepository()
        {
            SqlMapper.AddTypeHandler(typeof(IList<TorrentFileModel>), new FileListTypeHandler());
        }

        public InfoHashRepository(DbFactory factory) : base(factory)
        {
        }

        private async Task<bool> InsertOrUpdateAsync(InfoHashModel model, IDbTransaction transaction)
        {
            var updateSql = new StringBuilder();
            var list = new List<DbParameter>
            {
                new NpgsqlParameter("updatetime", DateTime.Now),
                new NpgsqlParameter("infohash", model.InfoHash)
            };
            updateSql.Append("updatetime = @updatetime,");
            if (model.CreateTime != default(DateTime))
            {
                list.Add(new NpgsqlParameter("createtime", model.CreateTime));
            }
            if (model.IsDanger)
            {
                list.Add(new NpgsqlParameter("isdanger", model.IsDanger));
                updateSql.Append("isdanger = @isdanger,");
            }
            if (model.DownNum > 0)
            {
                list.Add(new NpgsqlParameter("downnum", model.DownNum));
                updateSql.Append("downnum = ti.downnum +@downnum,");
            }
            if (model.FileNum > 0)
            {
                list.Add(new NpgsqlParameter("filenum", model.FileNum));
                updateSql.Append("filenum = @filenum,");
            }
            if (model.FileSize > 0)
            {
                list.Add(new NpgsqlParameter("filesize", model.FileSize));
                updateSql.Append("filesize = @filesize,");
            }
            if (!model.Files.IsEmpty())
            {
                list.Add(new NpgsqlParameter("hasfile", true));
                updateSql.Append("hasfile = True,");
            }
            if (model.Name != null)
            {
                list.Add(new NpgsqlParameter("name", model.Name));
                updateSql.Append("name = @name,");
            }

            if (transaction != null)
                return await DoInsert(model, transaction, list, updateSql);
            if (Connection.State != ConnectionState.Open)
            {
                Connection.Open();
            }
            using (transaction = Connection.BeginTransaction())
            {
                return await DoInsert(model, transaction, list, updateSql);
            }
        }

        private async Task<bool> DoInsert(InfoHashModel model, IDbTransaction transaction, List<DbParameter> list, StringBuilder updateSql)
        {
            var hashId = await Connection.ExecuteScalarAsync<long>(
                string.Format(
                    "INSERT INTO t_infohash AS ti ({0}) VALUES ({1}) ON CONFLICT  (infohash) DO UPDATE SET {2} RETURNING id",
                    string.Join(",", list.Select(l => l.ParameterName)),
                    string.Join(",", list.Select(l => "@" + l.ParameterName)), updateSql.ToString().TrimEnd(',')), new InfoHashParamter(list),
                transaction);
            if (hashId <= 0)
            {
                return false;
            }
            model.Id = hashId;
            if (model.Files.IsEmpty())
            {
                return true;
            }
            await InsertHashFile(model, transaction);
            return true;
        }

        private async Task InsertHashFile(InfoHashModel model, IDbTransaction transaction)
        {
            IList<DbParameter> fileParams = new DbParameter[]
                        {
                new NpgsqlParameter("files", NpgsqlDbType.Jsonb) {Value = model.Files.ToJson()},
                new NpgsqlParameter("hashId", DbType.Int64) {Value = model.Id},
                        };
            await Connection.ExecuteAsync("INSERT INTO t_infohash_file (info_hash_id, files) VALUES (@hashId,@files) ON CONFLICT (info_hash_id) DO UPDATE SET files=@files;",
                new InfoHashParamter(fileParams), transaction);
        }

        public async Task<bool> InsertOrUpdateAsync(InfoHashModel model)
        {
            return await InsertOrUpdateAsync(model, null);
        }

        public async Task<bool> InsertOrUpdateAsync(IEnumerable<InfoHashModel> models)
        {
            if (Connection.State != ConnectionState.Open)
            {
                Connection.Open();
            }
            IDbTransaction trans = null;
            try
            {
                trans = Connection.BeginTransaction();
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
                trans?.Rollback();
                return false;
            }
            finally
            {
                trans?.Dispose();
            }

        }

        private async Task<bool> BatchInsertAsync(IList<InfoHashModel> models, IDbTransaction transaction = null)
        {
            var inTrans = transaction != null;
            try
            {
                if (!inTrans)
                {
                    if (Connection.State != ConnectionState.Open)
                    {
                        Connection.Open();
                        transaction = Connection.BeginTransaction();
                    }
                }
                var hashFileMap = new Dictionary<string, InfoHashModel>();
                var batchSql = "INSERT INTO t_infohash (infohash, name, filenum, filesize, downnum, createtime, updatetime, isdanger, hasfile) VALUES {0} RETURNING infohash,id;";
                var updateItemsSql = new StringBuilder();
                var parameterList = new List<DbParameter>();
                for (int i = 0, j = models.Count - 1; i < models.Count; i++)
                {
                    var item = models[i];
                    if (item.Files != null && item.Files.Count > 0)
                    {
                        hashFileMap[item.InfoHash] = item;
                    }
                    updateItemsSql.AppendFormat("(@hash{0}, @name{0}, @filenum{0}, @filesize{0}, @downnum{0}, @createtime{0}, @updatetime{0}, @isdanger{0}, @hasfile{0})", i.ToString());
                    parameterList.Add(new NpgsqlParameter($"hash{i}", item.InfoHash));
                    parameterList.Add(new NpgsqlParameter($"name{i}", item.Name ?? ""));
                    parameterList.Add(new NpgsqlParameter($"filenum{i}", item.FileNum));
                    parameterList.Add(new NpgsqlParameter($"filesize{i}", item.FileSize));
                    parameterList.Add(new NpgsqlParameter($"downnum{i}", item.DownNum));
                    parameterList.Add(new NpgsqlParameter($"createtime{i}", item.CreateTime == default(DateTime) ? DateTime.Now : item.CreateTime));
                    parameterList.Add(new NpgsqlParameter($"updatetime{i}", item.UpdateTime == default(DateTime) ? DateTime.Now : item.UpdateTime));
                    parameterList.Add(new NpgsqlParameter($"isdanger{i}", item.IsDanger));
                    parameterList.Add(new NpgsqlParameter($"hasfile{i}", item.HasFile));
                    if (i != j)
                    {
                        updateItemsSql.Append(',');
                    }
                }
                using (var reader = await Connection.ExecuteReaderAsync(string.Format(batchSql, updateItemsSql.ToString()), new InfoHashParamter(parameterList), transaction))
                {
                    if (hashFileMap.Count <= 0)
                    {
                        return true;
                    }
                    while (reader.Read())
                    {
                        var hash = reader.GetString(0);
                        var id = reader.GetInt64(1);
                        if (!hashFileMap.TryGetValue(hash, out var item))
                        {
                            continue;
                        }
                        item.Id = id;
                    }
                }
                foreach (var item in hashFileMap.Values)
                {
                    if (item.Id > 0)
                        await InsertHashFile(item, transaction);
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Error("批量添加失败", ex);
                transaction?.Rollback();
                return false;
            }
            finally
            {
                if (!inTrans && transaction != null)
                {
                    transaction.Dispose();
                }
            }
        }

        private async Task<bool> BatchUpdateAsync(IList<InfoHashModel> models, IDbTransaction transaction = null)
        {
            var inTrans = transaction != null;
            try
            {
                if (!inTrans)
                {
                    if (Connection.State != ConnectionState.Open)
                    {
                        Connection.Open();
                        transaction = Connection.BeginTransaction();
                    }
                }
                var updateSql = new StringBuilder();
                var list = new List<DbParameter>();
                foreach (var model in models)
                {
                    list.Add(new NpgsqlParameter("id", model.Id));
                    if (model.CreateTime != default(DateTime))
                    {
                        list.Add(new NpgsqlParameter("createtime", model.CreateTime));
                    }
                    if (model.IsDanger)
                    {
                        list.Add(new NpgsqlParameter("isdanger", model.IsDanger));
                        updateSql.Append("isdanger = @isdanger,");
                    }
                    if (model.DownNum > 0)
                    {
                        list.Add(new NpgsqlParameter("downnum", model.DownNum));
                        updateSql.Append("downnum = downnum +@downnum,");
                    }
                    if (model.FileNum > 0)
                    {
                        list.Add(new NpgsqlParameter("filenum", model.FileNum));
                        updateSql.Append("filenum = @filenum,");
                    }
                    if (model.FileSize > 0)
                    {
                        list.Add(new NpgsqlParameter("filesize", model.FileSize));
                        updateSql.Append("filesize = @filesize,");
                    }
                    if (!model.Files.IsEmpty())
                    {
                        list.Add(new NpgsqlParameter("hasfile", true));
                        updateSql.Append("hasfile = True,");
                    }
                    if (model.Name != null)
                    {
                        list.Add(new NpgsqlParameter("name", model.Name));
                        updateSql.Append("name = @name,");
                    }
                    await Connection.ExecuteAsync(string.Format("UPDATE t_infohash SET {0} updatetime=current_timestamp WHERE id=@id;", updateSql.ToString()), new InfoHashParamter(list), transaction);
                    if (!model.Files.IsEmpty())
                    {
                        await InsertHashFile(model, transaction);
                    }
                    list.Clear();
                    updateSql.Clear();
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Error("操作失败", ex);
                transaction?.Rollback();
                return false;
            }
            finally
            {
                if (!inTrans && transaction != null)
                {
                    transaction.Dispose();
                }
            }
        }

        public async Task<bool> BatchInsertOrUpdateAsync(IEnumerable<InfoHashModel> models)
        {
            if (Connection.State != ConnectionState.Open)
            {
                Connection.Open();
            }
            IDbTransaction trans = null;
            try
            {
                List<InfoHashModel> insertItems = new List<InfoHashModel>(), updateItems = new List<InfoHashModel>();
                var hashIds = string.Join(",", models.Select(t => $"'{t.InfoHash}'"));
                var idMap = new Dictionary<string, long>();
                using (var reader = await Connection.ExecuteReaderAsync($"SELECT id,infohash FROM t_infohash WHERE infohash IN ({hashIds})"))
                {
                    while (reader.Read())
                    {
                        idMap[reader.GetString(1)] = reader.GetInt64(0);
                    }
                }
                long hashId = 0;
                foreach (var model in models)
                {
                    if (idMap.TryGetValue(model.InfoHash, out hashId))
                    {
                        model.Id = hashId;
                        updateItems.Add(model);
                    }
                    else
                    {
                        insertItems.Add(model);
                    }
                }
                trans = Connection.BeginTransaction();
                if (insertItems.Count > 0)
                {
                    await BatchInsertAsync(insertItems, trans);
                }
                if (updateItems.Count > 0)
                {
                    await BatchUpdateAsync(updateItems, trans);
                }
                trans.Commit();
                return true;
            }
            catch (Exception ex)
            {
                log.Error("添加失败", ex);
                trans?.Rollback();
                return false;
            }
            finally
            {
                trans?.Dispose();
            }
        }
        public async Task<uint> GetTorrentNumAsync()
        {
            return await Connection.ExecuteScalarAsync<uint>("select count(id) from t_infohash;");
        }

        public async Task<bool> ExistsByHashAsync(string infohash)
        {
            return await Connection.ExecuteScalarAsync<int>("select 1 from t_infohash where infohash=@hash;", new { hash = infohash }) > 0;
        }

        public async Task<bool> HasDownInfoHash(string infohash)
        {
            return await Connection.ExecuteScalarAsync<int>("select count(id) from t_infohash where infohash=@hash;", new { hash = infohash }) > 0;
        }

        public async Task<IList<string>> GetDownloadInfoHashAsync()
        {
            var result = new List<string>();
            var reader = await Connection.ExecuteReaderAsync("select infohash from t_infohash;");
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
            return result;
        }

        public async Task<(IList<InfoHashModel> List, long Count)> GetInfoHashListAsync(int index, int size, DateTime? start = null, DateTime? end = null, bool? isDanger = null, bool desc = true)
        {
            var where = new StringBuilder();
            if (start.HasValue)
            {
                where.Append("AND createtime >= @startTime ");
            }
            if (end.HasValue)
            {
                where.Append("AND createtime <= @endTime ");
            }
            if (isDanger.HasValue)
            {
                where.Append("AND isdanger=@danger");
            }
            if (where.Length > 0)
            {
                var result = await Connection.QueryMultipleAsync(string.Format("SELECT count(id) FROM t_infohash {0};SELECT infohash,name,filenum,filesize,downnum,createtime FROM t_infohash {0} {1} OFFSET @start LIMIT @size;", where.Length > 0 ? "WHERE " + where.ToString().Substring(3) : string.Empty, desc ? " order by createtime desc" : " order by createtime"), new { start = (index - 1) * size, size = size, startTime = start, endTime = end, danger = isDanger });
                var count = await result.ReadFirstAsync<long>();
                var list = (await result.ReadAsync<InfoHashModel>()).ToArray();
                return (list, count);
            }
            else
            {
                var result = await Connection.QueryMultipleAsync(
                    $"SELECT infohash,name,filenum,filesize,downnum,createtime FROM t_infohash {(desc ? " order by createtime desc" : " order by createtime")} OFFSET @start LIMIT @size;", new { start = (index - 1) * size, size = size, startTime = start, endTime = end, danger = isDanger });
                var list = (await result.ReadAsync<InfoHashModel>()).ToArray();
                return (list, int.MaxValue);
            }
        }

        public async Task<InfoHashModel> GetInfoHashDetailAsync(string hash)
        {
            var item = await Connection.QueryFirstOrDefaultAsync<InfoHashModel>("SELECT * FROM t_infohash WHERE infohash=@hash", new { hash });
            if (item != null && item.HasFile)
            {
                item.Files = await Connection.QueryFirstOrDefaultAsync<IList<TorrentFileModel>>("SELECT files FROM t_infohash_file WHERE info_hash_id =@hashId; ", new { hashId = item.Id });
            }
            return item;
        }

        public IEnumerable<InfoHashModel> GetAllFullInfoHashModels(DateTime? start = null)
        {
            var id = 0L;
            var size = 500;
            var sql = new StringBuilder("SELECT id, infohash, name, filenum, filesize, downnum, createtime, updatetime, hasfile, isdanger FROM t_infohash WHERE id > @id");
            if (start.HasValue)
            {
                sql.Append(" AND updatetime>@start");
            }
            sql.Append(" order by id LIMIT @size");
            var selectSql = sql.ToString();
            using (var connection = this.Factory.CreateConnection())
            {
                do
                {
                    var length = 0;
                    IEnumerable<InfoHashModel> hashs;
                    try
                    {
                        hashs = connection.Query<InfoHashModel>(selectSql, new { id, start, size }, null, true, 60 * 5);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                        throw;
                    }
                    foreach (var model in hashs)
                    {
                        length++;
                        id = model.Id;
                        if (model.HasFile)
                        {
                            model.Files = connection.QueryFirstOrDefault<IList<TorrentFileModel>>("SELECT files FROM t_infohash_file WHERE info_hash_id =@hashId; ", new { hashId = model.Id });
                            if (model.Files.IsEmpty())
                            {
                                log.InfoFormat("种子信息有误，对应文件内容不存在,hash:{0}", model.InfoHash);
                            }
                        }
                        yield return model;
                    }
                    if (length < size)
                        yield break;
                } while (true);
            }
        }


        public IEnumerable<InfoHashModel> GetSyncFullInfoHashModels()
        {
            long hashId = 0;
            var size = 500;
            const string sql = "SELECT infohash_id FROM t_sync_infohash WHERE infohash_id>@hashId ORDER BY infohash_id LIMIT @size";
            const string infoSql = "SELECT id, infohash, name, filenum, filesize, downnum, createtime, updatetime, hasfile, isdanger FROM t_infohash WHERE id = @hashId";
            const string fileSql = "SELECT files FROM t_infohash_file WHERE info_hash_id = @hashId; ";
            using (var connection = this.Factory.CreateConnection())
            {
                do
                {
                    var length = 0;
                    IEnumerable<long> hashIds;
                    try
                    {
                        hashIds = connection.Query<long>(sql, new { hashId, size });
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                        throw;
                    }
                    foreach (var id in hashIds)
                    {
                        length++;
                        hashId = id;
                        var param = new { hashId };
                        var model = connection.QuerySingleOrDefault<InfoHashModel>(infoSql, param);
                        if (model == null)
                        {
                            continue;
                        }
                        if (model.HasFile)
                        {
                            model.Files = connection.QueryFirstOrDefault<IList<TorrentFileModel>>(fileSql, param);
                            if (model.Files.IsEmpty())
                            {
                                log.InfoFormat("种子信息有误，对应文件内容不存在,hash:{0}", model.InfoHash);
                            }
                        }
                        yield return model;
                    }
                    if (length < size)
                        yield break;
                } while (true);
            }
        }

        public async Task<DateTime> GetLastInfoHashDownTimeAsync()
        {
            return await Connection.ExecuteScalarAsync<DateTime>("SELECT max(createtime) FROM t_infohash");
        }

        public void RemoveSyncInfo(ICollection<long> hashIds)
        {
            Connection.Execute($"DELETE FROM t_sync_infohash WHERE infohash_id IN ({string.Join(",", hashIds)})");
        }
    }
}
