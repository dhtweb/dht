using System;
using System.Data;
using DhtCrawler.Common.Db;
using log4net;

namespace DhtCrawler.Service.Repository
{
    public abstract class BaseRepository : IDisposable
    {
        protected readonly ILog log;

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
            this.log = LogManager.GetLogger(GetType());
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
