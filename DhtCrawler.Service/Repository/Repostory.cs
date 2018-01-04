using System;
using System.Data;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service.Repository
{
    public abstract class BaseRepository<T, TId> : IDisposable where T : BaseModel<TId>
    {
        protected DbFactory Factory { get; }

        private IDbConnection _connection;
        protected IDbConnection Connection
        {
            get
            {
                if (_connection != null)
                    return _connection;
                return _connection = Factory.CreateConnection();
            }
        }

        protected BaseRepository(DbFactory factory)
        {
            this.Factory = factory;
        }

        protected IDbTransaction BeginTransaction()
        {
            if (Connection.State != ConnectionState.Open)
            {
                Connection.Open();
            }
            return Connection.BeginTransaction();
        }

        public void Dispose()
        {
            if (_connection == null)
                return;
            _connection.Dispose();
            _connection = null;
        }
    }
}
