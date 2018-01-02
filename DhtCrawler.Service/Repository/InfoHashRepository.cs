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
    public class InfoHashRepository : BaseRepository<InfoHashModel, long>
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
            if (model.IsDown)
            {
                list.Add(new NpgsqlParameter("isdown", model.IsDown));
                updateSql.Append("isdown = @isdown,");
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
            if (model.Files == null || model.Files.Count <= 0)
            {
                return true;
            }
            IList<DbParameter> fileParams = new DbParameter[]
            {
                new NpgsqlParameter("files", NpgsqlDbType.Jsonb) {Value = model.Files.ToJson()},
                new NpgsqlParameter("hashId", DbType.Int64) {Value = hashId},
            };
            await Connection.ExecuteAsync("INSERT INTO t_infohash_file (info_hash_id, files) VALUES (@hashId,@files) ON CONFLICT (info_hash_id) DO UPDATE SET files=@files;",
                new InfoHashParamter(fileParams), transaction);
            return true;
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
                Console.WriteLine(ex);
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
            return await Connection.ExecuteScalarAsync<uint>("select count(id) from t_infohash where isdown=@flag ", new { flag = true });
        }

        public async Task<bool> HasDownInfoHash(string infohash)
        {
            return await Connection.ExecuteScalarAsync<int>("select count(id) from t_infohash where isdown=True AND infohash=@hash", new { hash = infohash }) > 0;
        }

        public async Task<IList<string>> GetDownloadInfoHashAsync()
        {
            var result = new List<string>();
            var reader = await Connection.ExecuteReaderAsync("select infohash from t_infohash where isdown=@flag ", new { flag = true });
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
            var result = await Connection.QueryMultipleAsync(string.Format("SELECT count(id) FROM t_infohash WHERE isdown = TRUE {0};SELECT infohash,name,filesize,downnum,createtime FROM t_infohash WHERE isdown=TRUE {0} {1} OFFSET @start LIMIT @size;", where.ToString(), desc ? " order by createtime desc" : " order by createtime"), new { start = (index - 1) * size, size = size, startTime = start, endTime = end, danger = isDanger });
            var count = await result.ReadFirstAsync<long>();
            var list = (await result.ReadAsync<InfoHashModel>()).ToArray();
            //var fileIds = list.Where(l => l.HasFile).ToDictionary(l => l.Id, l => l);
            //if (fileIds.Count > 0)
            //{
            //    using (var reader = await Connection.ExecuteReaderAsync("SELECT info_hash_id,files FROM t_infohash_file WHERE info_hash_id IN @ids", new { ids = fileIds.Keys.ToArray() }))
            //    {
            //        while (reader.Read())
            //        {
            //            var hashId = reader.GetInt64(0);
            //            if (fileIds.ContainsKey(hashId))
            //            {
            //                fileIds[hashId].Files = reader.GetString(0).ToObjectFromJson<IList<TorrentFileModel>>();
            //            }
            //        }
            //    }
            //}
            return (list, count);
        }

        public async Task<InfoHashModel> GetInfoHashDetailAsync(string hash)
        {
            var item = await Connection.QueryFirstAsync<InfoHashModel>("SELECT * FROM t_infohash WHERE isdown = TRUE AND infohash=@hash", new { hash });
            if (item != null && item.HasFile)
            {
                item.Files = await Connection.QueryFirstAsync<IList<TorrentFileModel>>("SELECT files FROM t_infohash_file WHERE info_hash_id =@hashId; ", new { hashId = item.Id });
            }
            return item;
        }

        public IEnumerable<InfoHashModel> GetAllFullInfoHashModels(DateTime? start = null)
        {
            var id = 0L;
            var size = 500;
            var sql = new StringBuilder("SELECT id, infohash, name, filenum, filesize, downnum, isdown, createtime, updatetime, files, isdanger FROM t_infohash WHERE isdown=TRUE AND id > @id");
            if (start.HasValue)
            {
                sql.Append(" AND updatetime>@start");
            }
            sql.Append(" order by id LIMIT @size");
            var selectSql = sql.ToString();
            do
            {
                var length = 0;
                IEnumerable<InfoHashModel> hashs;
                try
                {
                    hashs = Connection.Query<InfoHashModel>(selectSql, new { id, start, size });
                }
                catch (Exception ex)
                {
                    continue;
                }
                foreach (var model in hashs)
                {
                    length++;
                    id = model.Id;
                    if (model.HasFile)
                    {
                        model.Files = Connection.QueryFirst<IList<TorrentFileModel>>("SELECT files FROM t_infohash_file WHERE info_hash_id =@hashId; ", new { hashId = model.Id });
                    }
                    yield return model;
                }
                if (length < size)
                    yield break;
            } while (true);
        }

    }
}
