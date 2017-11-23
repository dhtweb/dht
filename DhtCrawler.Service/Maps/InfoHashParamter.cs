using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Dapper;

namespace DhtCrawler.Service.Maps
{
    public class InfoHashParamter : SqlMapper.IDynamicParameters
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