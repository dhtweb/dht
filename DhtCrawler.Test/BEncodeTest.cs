using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dapper;
using DhtCrawler.Common;
using DhtCrawler.Encode;
using DhtCrawler.Service;
using DhtCrawler.Service.Maps;
using DhtCrawler.Service.Model;
using Npgsql;
using Xunit;

namespace DhtCrawler.Test
{
    public class BEncodeTest
    {

        [Fact]
        static void EncodeTest()
        {
            var str =
                "64-31-3A-65-6C-69-32-30-33-65-31-34-3A-45-72-72-6F-72-20-50-72-6F-74-6F-63-6F-6C-65-31-3A-74".Split('-');
            var bytes = new List<byte>();
            foreach (var s in str)
            {
                bytes.Add(Convert.ToByte(s, 16));
            }
            var dic = BEncoder.Decode(bytes.ToArray());
            Console.WriteLine(dic);
            Assert.True(true);
        }
        [Fact]
        static void DecodeTorrentTest()
        {
            var path = @"E:\Code\dotnetcore\dht\DhtCrawler.Test\xp1024.com_STP1179MP4.torrent";
            var bytes = File.ReadAllBytes(path);
            var dic = (IDictionary<string, object>)BEncoder.Decode(bytes);
            var info = (IDictionary<string, object>)dic["info"];
            var name = (byte[])info["name"];
            Console.WriteLine(Encoding.UTF8.GetString(name));
            var files = (IList<object>)info["files"];
            foreach (IDictionary<string, object> file in files)
            {
                foreach (var kv in file)
                {
                    Console.WriteLine(kv.Key);
                    if (kv.Value is IList<object> list)
                    {
                        foreach (byte[] item in list)
                        {
                            Console.WriteLine(Encoding.UTF8.GetString(item));
                        }
                    }
                    else
                    {
                        Console.WriteLine(kv.Value);
                    }
                }
            }
            Console.WriteLine(info);
            ;
        }

        [Fact]
        static void Test()
        {
            var lines = File.ReadAllLines(@"E:\Code\dotnetcore\dht\DhtCrawler\bin\Release\PublishOutput\torrent\B2960BC78CBBE627E43DCE0562C0DB6552F190A7");
            foreach (var line in lines)
            {
                if (!line.StartsWith("files"))
                    continue;
                var info = Encoding.UTF8.GetBytes(line.Split('\t')[1]);
                var list = (IList<object>)BEncoder.Decode(info);
                foreach (IDictionary<string, object> file in list)
                {
                    foreach (var kv in file)
                    {
                        Console.Write(kv.Key);
                        Console.Write('\t');
                        if (kv.Value is IList<object> paths)
                        {
                            Console.Write(string.Join("/", paths.Select(path => Encoding.UTF8.GetString((byte[])path))));
                        }
                        else
                        {
                            Console.Write(kv.Value);
                        }
                        Console.WriteLine();
                    }
                }
            }
            Assert.True(true);
        }

        [Fact]
        static void TestUpdate()
        {
            SqlMapper.AddTypeHandler(typeof(IList<TorrentFileModel>), new FileListTypeHandler());
            const string conStr = "Host=127.0.0.1;Username=zk;Database=dht;Port=5432";
            var connection = new NpgsqlConnection(conStr);
            var update = new NpgsqlConnection(conStr);
            var list = connection.Query<InfoHashModel>("SELECT * FROM t_infohash WHERE filenum=1 AND files NOTNULL", (object)null, null, false);
            foreach (var item in list)
            {
                update.Execute("UPDATE t_infohash SET filenum = @num WHERE infohash = @hash", new { num = item.FileNum, hash = item.InfoHash });
                Console.WriteLine(item.InfoHash);
            }
            Assert.True(true);
        }
    }
}
