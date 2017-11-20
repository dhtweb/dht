﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Service.Model;
using DhtCrawler.Common;
using Npgsql;
using NpgsqlTypes;

namespace DhtCrawler.Service
{
    public class InfoHashRepository : BaseRepository<InfoHashModel, ulong>
    {
        private class FileListTypeHandler : SqlMapper.TypeHandler<IList<TorrentFileModel>>
        {
            public override void SetValue(IDbDataParameter parameter, IList<TorrentFileModel> value)
            {
                parameter.Value = value.ToJson();
            }

            public override IList<TorrentFileModel> Parse(object value)
            {
                if (value == null || !(value is string))
                    return null;
                return ((string)value).ToObject<IList<TorrentFileModel>>();
            }
        }
        static InfoHashRepository()
        {
            SqlMapper.AddTypeHandler(typeof(IList<TorrentFileModel>), new FileListTypeHandler());
        }

        public InfoHashRepository(IDbConnection connection) : base(connection)
        {
        }


        public async Task<bool> InsertOrUpdate(InfoHashModel model)
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

        public async Task<uint> GetTorrentNum()
        {
            return await Connection.ExecuteScalarAsync<uint>("select count(id) from t_infohash where isdown=@flag ", new { flag = true });
        }

        public async Task<IList<string>> GetDownloadInfoHash()
        {
            var result = new List<string>();
            var reader = await Connection.ExecuteReaderAsync("select infohash from t_infohash where isdown=@flag ", new { flag = true });
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
            return result;
        }

        public async Task<(IList<InfoHashModel> List, long Count)> GetInfoHashList(int index, int size)
        {
            var result = await Connection.QueryMultipleAsync("SELECT count(id) FROM t_infohash WHERE isdown = TRUE;SELECT infohash,name,filesize,downnum,createtime FROM t_infohash WHERE isdown=TRUE OFFSET @start LIMIT @size;", new { start = (index - 1) * size, size = size });
            var count = await result.ReadFirstAsync<long>();
            var list = await result.ReadAsync<InfoHashModel>();
            return (list.ToArray(), count);
        }

        public async Task<InfoHashModel> GetInfoHashDetail(string hash)
        {
            return await Connection.QuerySingleAsync<InfoHashModel>("SELECT * FROM t_infohash WHERE isdown = TRUE AND infohash=@hash", new { hash });
        }

        public IEnumerable<InfoHashModel> GetAllInfoHashModels(DateTime? start = null)
        {
            return start.HasValue ? Connection.Query<InfoHashModel>("SELECT infohash,name,filesize,downnum,createtime FROM t_infohash WHERE isdown=TRUE AND createtime>@start", new { start = start.Value }) : Connection.Query<InfoHashModel>("SELECT infohash,name,filesize,downnum,createtime FROM t_infohash WHERE isdown=TRUE");
        }
        private class InfoHashParamter : SqlMapper.IDynamicParameters
        {
            private List<DbParameter> _parameters;
            public InfoHashParamter(List<DbParameter> parameters)
            {
                _parameters = parameters;
            }
            public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
            {
                foreach (var parameter in _parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }
        }
    }
}
