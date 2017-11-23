using System;
using System.Collections.Generic;
using System.Data;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Model;

namespace DhtCrawler.Service
{
    public abstract class BaseRepository<T, TId> : IDisposable where T : BaseModel<TId>
    {
        private readonly IList<IDbConnection> _connections;
        protected DbFactory Factory { get; }

        protected IDbConnection Connection
        {
            get
            {
                var connection = Factory.CreateConnection();
                lock (this)
                {
                    _connections.Add(connection);
                }
                return connection;
            }
        }

        protected BaseRepository(DbFactory factory)
        {
            this.Factory = factory;
            _connections = new List<IDbConnection>();
        }

        public void Dispose()
        {
            lock (this)
            {
                foreach (var connection in _connections)
                {
                    connection.Dispose();
                }
                _connections.Clear();
            }
        }
    }
}
