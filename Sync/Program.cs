using System;
using System.Threading;
using DhtCrawler.Common;
using DhtCrawler.Common.Db;
using DhtCrawler.Service.Repository;
using Npgsql;

namespace Sync
{
    class Program
    {
        static void Main(string[] args)
        {
            var dbFactory = new DbFactory("Host=198.13.54.254;Username=zk;Database=dht;Port=5432;Password=741-plm-qaz", NpgsqlFactory.Instance);
            var repository = new InfoHashRepository(dbFactory);
            var store = new StoreFile("infohash.data");
            Console.WriteLine("start index {0}", store.Index);
            var start = store.Index;
            var size = args.Length > 0 ? Int32.Parse(args[0]) : 500;
            var count = 0;
            while (true)
            {
                try
                {
                    var list = repository.GetInfoHashDetailList(start, size);
                    foreach (var model in list)
                    {
                        start = model.Id;
                        var json = model.ToJson();
                        store.Append(json);
                        Console.WriteLine(model.InfoHash);
                    }
                    store.SetIndex(start);
                    store.Commit();
                    count += list.Count;
                    Console.WriteLine("Commit {0} records", count);
                    if (list.Count < size)
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Thread.Sleep(2000);
                }
            }
        }
    }
}
