using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DhtCrawler.Service.Model;
using DhtCrawler.Common;

namespace DhtCrawler.Service
{
    public class InfoHashRepository : BaseRepository<InfoHashModel, ulong>
    {
        public InfoHashRepository(IDbConnection connection) : base(connection)
        {
        }


        public async Task<bool> InsertOrUpdate(InfoHashModel model)
        {
            var updateSql = new StringBuilder();
            var list = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("updatetime", DateTime.Now),
                new KeyValuePair<string, object>("infohash", model.InfoHash)
            };
            if (model.CreateTime != default(DateTime))
            {
                list.Add(new KeyValuePair<string, object>("createtime", model.CreateTime));
                updateSql.Append("createtime=@createtime,");
            }
            if (model.IsDown)
            {
                list.Add(new KeyValuePair<string, object>("isdown", model.IsDown));
                updateSql.Append("isdown=@isdown,");
            }
            if (model.DownNum > 0)
            {
                list.Add(new KeyValuePair<string, object>("downnum", model.DownNum));
                updateSql.Append("downnum= downnum +@downnum,");
            }
            if (model.FileNum > 0)
            {
                list.Add(new KeyValuePair<string, object>("filenum", model.FileNum));
                updateSql.Append("filenum=@filenum,");
            }
            if (model.FileSize > 0)
            {
                list.Add(new KeyValuePair<string, object>("filesize", model.FileSize));
                updateSql.Append("filesize=@filesize,");
            }
            if (model.Name != null)
            {
                list.Add(new KeyValuePair<string, object>("name", model.Name));
                updateSql.Append("name=@name,");
            }
            if (model.Files != null && model.Files.Count > 0)
            {
                list.Add(new KeyValuePair<string, object>("files", model.Files.ToJson()));
                updateSql.Append("files=@files,");
            }
            return await Connection.ExecuteAsync(string.Format("INSERT INTO t_infohash ({0}) VALUES ({1}) ON CONFLICT  (infohash) DO UPDATE SET {2}", string.Join(",", list.Select(l => l.Key)), string.Join(",", list.Select(l => "@" + l.Key)), updateSql.ToString().TrimEnd(',')), list) > 0;
        }
    }
}
