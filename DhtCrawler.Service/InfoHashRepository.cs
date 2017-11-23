using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Service.Model;
using DhtCrawler.Common;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Maps;
using Npgsql;
using NpgsqlTypes;

namespace DhtCrawler.Service
{
    public class InfoHashRepository : BaseRepository<InfoHashModel, ulong>
    {

        static InfoHashRepository()
        {
            SqlMapper.AddTypeHandler(typeof(IList<TorrentFileModel>), new FileListTypeHandler());
        }

        public InfoHashRepository(DbFactory factory) : base(factory)
        {
        }


        public async Task<bool> InsertOrUpdateAsync(InfoHashModel model)
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
                updateSql.Append("createtime = @createtime,");
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
                updateSql.Append("downnum = excluded.downnum +@downnum,");
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
            if (model.Name != null)
            {
                list.Add(new NpgsqlParameter("name", model.Name));
                updateSql.Append("name = @name,");
            }
            if (model.Files != null && model.Files.Count > 0)
            {
                var param = new NpgsqlParameter("files", NpgsqlDbType.Jsonb) { Value = model.Files.ToJson() };
                list.Add(param);
                updateSql.Append("files = @files,");
            }
            var paramater = new InfoHashParamter(list);
            return await Connection.ExecuteAsync(string.Format("INSERT INTO t_infohash ({0}) VALUES ({1}) ON CONFLICT  (infohash) DO UPDATE SET {2}", string.Join(",", list.Select(l => l.ParameterName)), string.Join(",", list.Select(l => "@" + l.ParameterName)), updateSql.ToString().TrimEnd(',')), paramater) > 0;
        }

        public async Task<bool> InsertOrUpdateAsync(IEnumerable<InfoHashModel> models)
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
                        updateSql.Append("createtime = @createtime,");
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
                        updateSql.Append("downnum = excluded.downnum +@downnum,");
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
                    if (model.Name != null)
                    {
                        list.Add(new NpgsqlParameter("name", model.Name));
                        updateSql.Append("name = @name,");
                    }
                    if (model.Files != null && model.Files.Count > 0)
                    {
                        var param = new NpgsqlParameter("files", NpgsqlDbType.Jsonb) { Value = model.Files.ToJson() };
                        list.Add(param);
                        updateSql.Append("files = @files,");
                    }
                    var paramater = new InfoHashParamter(list);
                    await Connection.ExecuteAsync(
                        string.Format(
                            "INSERT INTO t_infohash ({0}) VALUES ({1}) ON CONFLICT  (infohash) DO UPDATE SET {2}",
                            string.Join(",", list.Select(l => l.ParameterName)),
                            string.Join(",", list.Select(l => "@" + l.ParameterName)),
                            updateSql.ToString().TrimEnd(',')), paramater, trans);
                }
                trans.Commit();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                trans.Rollback();
                return false;
            }
            finally
            {
                trans.Dispose();
            }

        }

        public async Task<uint> GetTorrentNumAsync()
        {
            return await Connection.ExecuteScalarAsync<uint>("select count(id) from t_infohash where isdown=@flag ", new { flag = true });
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

        public async Task<(IList<InfoHashModel> List, long Count)> GetInfoHashListAsync(int index, int size, DateTime? start = null, DateTime? end = null, bool desc = true)
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
            var result = await Connection.QueryMultipleAsync(string.Format("SELECT count(id) FROM t_infohash WHERE isdown = TRUE {0};SELECT infohash,name,filesize,downnum,createtime FROM t_infohash WHERE isdown=TRUE {0} {1} OFFSET @start LIMIT @size;", where.ToString(), desc ? " order by createtime desc" : "order by create"), new { start = (index - 1) * size, size = size, startTime = start, endTime = end });
            var count = await result.ReadFirstAsync<long>();
            var list = await result.ReadAsync<InfoHashModel>();
            return (list.ToArray(), count);
        }

        public async Task<InfoHashModel> GetInfoHashDetailAsync(string hash)
        {
            return await Connection.QuerySingleAsync<InfoHashModel>("SELECT * FROM t_infohash WHERE isdown = TRUE AND infohash=@hash", new { hash });
        }

        public IEnumerable<InfoHashModel> GetAllFullInfoHashModels(DateTime? start = null)
        {
            using (var listCon = Factory.CreateConnection())
            {
                var hashs = start.HasValue ? listCon.Query<string>("SELECT infohash FROM t_infohash WHERE isdown=TRUE AND updatetime>@start", new { start = start.Value }, null, false) : listCon.Query<string>("SELECT infohash FROM t_infohash WHERE isdown=TRUE", (object)null, null, false);
                using (var queryCon = Factory.CreateConnection())
                {
                    foreach (var hash in hashs)
                    {
                        yield return queryCon.QuerySingle<InfoHashModel>("SELECT * FROM t_infohash WHERE isdown = TRUE AND infohash=@hash", new { hash });
                    }
                }
            }
        }
    }
}
