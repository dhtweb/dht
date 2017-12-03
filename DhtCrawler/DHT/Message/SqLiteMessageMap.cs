using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace DhtCrawler.DHT.Message
{
    public static class SqliteTool
    {
        public static T ExcuteRead<T>(this DbConnection con, string sql, IList<DbParameter> parameters, Func<DbDataReader, T> convert)
        {
            var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            foreach (var parameter in parameters)
            {
                cmd.Parameters.Add(parameter);
            }
            if (con.State == ConnectionState.Closed)
            {
                con.Open();
            }
            using (var reader = cmd.ExecuteReader())
            {
                return convert(reader);
            }
        }

        public static int Excute(this DbConnection con, string sql, IList<DbParameter> parameters)
        {
            var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            foreach (var parameter in parameters)
            {
                cmd.Parameters.Add(parameter);
            }
            if (con.State == ConnectionState.Closed)
            {
                con.Open();
            }
            return cmd.ExecuteNonQuery();
        }
    }
    public class SqLiteMessageMap : AbstractMessageMap
    {
        private readonly object _syncRoot = new object();
        private readonly DbConnection connection;
        private int _bucketIndex;
        private readonly string dbPath;
        public SqLiteMessageMap(string storePath)
        {
            dbPath = storePath;
            EnsureTable(storePath);
            connection = new SqliteConnection(dbPath);
        }

        private static void EnsureTable(string storePath)
        {
            using (var sqlite = new SqliteConnection(storePath))
            {
                sqlite.Open();
                var cmd = sqlite.CreateCommand();
                cmd.CommandText = "select count(0) from sqlite_master where type = 'table' and name = 'dht_msg'";
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count <= 0)
                {
                    cmd.CommandText = "CREATE TABLE dht_msg (id BLOB PRIMARY KEY,info_hash BLOB NOT NULL,add_time INTEGER NOT NULL); ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeKey = node.CompactEndPoint();
            var key = new byte[nodeKey.Length + 2];
            Array.Copy(nodeKey, 0, key, 0, nodeKey.Length);
            var tryTimes = 0;
            while (tryTimes < 3)
            {
                lock (this)
                {
                    _bucketIndex++;
                    if (_bucketIndex >= _bucketArray.Length)
                        _bucketIndex = 0;
                }
                msgId = _bucketArray[_bucketIndex];
                key[6] = msgId[0];
                key[7] = msgId[1];
                lock (_syncRoot)
                {
                    var keyParameter = new SqliteParameter("id", SqliteType.Blob) { Value = nodeKey };
                    var con = connection;
                    //using (var con = new SqliteConnection(dbPath))
                    {
                        var record = con.ExcuteRead("select info_hash,add_time from dht_msg where id=@id", new DbParameter[] { keyParameter }, reader =>
                          {
                              if (!reader.Read())
                                  return null;
                              var row = new Record
                              {
                                  AddTime = new DateTime(reader.GetInt64(1)),
                                  InfoHash = (byte[])reader.GetValue(0)
                              };
                              return row;
                          });

                        if (record == null)
                        {
                            if (con.Excute("insert into dht_msg(id,info_hash,add_time) values(@id,@hash,@time)",
                                    new DbParameter[]
                                    {
                                            new SqliteParameter("hash",SqliteType.Blob) {ParameterName = "hash",Value = infoHash},
                                            new SqliteParameter("time",SqliteType.Integer){Value = DateTime.Now.Ticks},
                                            keyParameter
                                    }) > 0)
                            {
                                return true;
                            }
                        }
                        else if (record.AddTime.AddMinutes(30) < DateTime.Now)
                        {
                            if (con.Excute("update dht_msg set info_hash=@hash,add_time=@time where id=@id",
                                    new DbParameter[]
                                    {
                                        new SqliteParameter("hash",SqliteType.Blob) {ParameterName = "hash",Value = infoHash},
                                        new SqliteParameter("time",SqliteType.Integer){Value = DateTime.Now.Ticks},
                                            keyParameter
                                    }) > 0)
                            {
                                return true;
                            }
                        }
                    }
                }
                tryTimes++;
            }
            msgId = null;
            return false;
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            var nodeKey = node.CompactEndPoint();
            var key = new byte[nodeKey.Length + 2];
            Array.Copy(nodeKey, 0, key, 0, nodeKey.Length);
            key[6] = msgId[0];
            key[7] = msgId[1];
            lock (_syncRoot)
            {
                var keyParameter = new DbParameter[] { new SqliteParameter("id", SqliteType.Blob) { Value = nodeKey } };
                var con = connection;
                //using (var con = new SqliteConnection(dbPath))
                {
                    var record = con.ExcuteRead("select info_hash,add_time from dht_msg where id=@id",
                        keyParameter, reader =>
                          {
                              if (!reader.Read())
                                  return null;
                              var row = new Record
                              {
                                  AddTime = new DateTime(reader.GetInt64(1)),
                                  InfoHash = (byte[])reader.GetValue(0)
                              };
                              return row;
                          });
                    if (record == null)
                    {
                        infoHash = null;
                        return false;
                    }
                    infoHash = record.InfoHash;
                    con.Excute("delete from dht_msg where id=@id", keyParameter);
                    return true;
                }
            }
        }
    }
}
