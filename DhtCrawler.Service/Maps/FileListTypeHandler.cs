using System.Collections.Generic;
using System.Data;
using Dapper;
using DhtCrawler.Common;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service.Maps
{
    public class FileListTypeHandler : SqlMapper.TypeHandler<IList<TorrentFileModel>>
    {
        public override void SetValue(IDbDataParameter parameter, IList<TorrentFileModel> value)
        {
            parameter.Value = value.ToJson();
        }

        public override IList<TorrentFileModel> Parse(object value)
        {
            if (value == null || !(value is string))
                return null;
            return ((string)value).ToObjectFromJson<IList<TorrentFileModel>>();
        }
    }
}