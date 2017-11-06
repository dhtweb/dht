using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using DhtCrawler.Encode;
using Xunit;

namespace DhtCrawler.Test
{
    public class BEncodeTest
    {

        [Fact]
        static void EncodeTest()
        {
            var str =
                "64-31-BA-04-64-32-3A-69-64-32-30-3A-CD-E3-77-4B-B7-3A-98-7A-3A-C9-33-BB-E0-68-66-A0-F7-F9-0A-58-36-3A-74-61-72-67-65-74-32-30-3A-81-0C-B9-91-16-DF-BB-43-D4-11-CE-91-F6-02-A7-C7-BC-83-96-35-65-31-3A-71-39-3A-66-69-6E-64-5F-6E-6F-64-65-31-3A-74-32-3A-00-00-31-3A-79-31-3A-71-65".Split('-');
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
            var lines = File.ReadAllLines(@"E:\code\DhtCrawler\DhtCrawler\torrent\FD1A5EB1254BADC3313C571152B0258D1B56E5BA");
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
    }
}
