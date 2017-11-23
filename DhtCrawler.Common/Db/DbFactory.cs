using System.Data.Common;

namespace DhtCrawler.Common.Db
{
    public class DbFactory
    {
        private readonly string _connectionStr;
        private readonly DbProviderFactory _provider;
        public DbFactory(string connectionStr, DbProviderFactory provider)
        {
            _connectionStr = connectionStr;
            _provider = provider;
        }

        public DbConnection CreateConnection()
        {
            var connection = _provider.CreateConnection();
            connection.ConnectionString = _connectionStr;
            return connection;
        }
    }
}
