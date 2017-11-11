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
    }
}
