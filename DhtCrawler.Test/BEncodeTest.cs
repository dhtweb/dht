using System;
using System.Collections.Generic;
using System.IO;
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
                "64-32-3A-69-70-36-3A-6F-CB-A8-02-89-9F-31-3A-72-64-32-3A-69-64-32-30-3A-33-C5-23-EB-B3-A6-DB-3C-87-0C-3E-99-24-5E-0D-1C-06-B7-47-BB-35-3A-6E-6F-64-65-73-32-30-38-3A-33-C5-2E-F9-68-B5-AC-E5-1E-45-1A-78-1D-4B-DB-23-C6-F0-5E-DE-A3-17-71-04-22-5C-33-C5-2E-F1-BB-E9-EB-B3-A6-DB-3C-87-0C-3E-99-24-5E-0D-1C-49-5D-98-84-D7-93-C8-33-C5-2E-14-79-20-57-93-52-DA-CF-AE-10-3C-32-3D-4D-93-F6-BE-7B-80-45-1A-1D-E3-33-C5-2C-DA-A6-F3-CD-1F-72-AF-E3-C9-DF-C9-34-70-31-8F-3E-8E-31-91-77-D3-D2-E4-33-C5-2C-59-13-66-F7-78-E5-85-6C-1E-0C-F0-05-28-5D-A0-51-BE-6E-15-30-29-AA-5B-33-C5-2A-13-52-8D-A2-02-37-07-FE-DA-2F-E1-6A-E0-7B-28-58-0E-59-01-77-D3-A7-79-33-C5-2B-9A-7F-67-1A-31-0D-02-B3-61-C5-42-FB-B0-A5-7D-02-C9-4F-7E-82-00-6D-79-33-C5-29-D2-36-B6-90-00-0A-36-3E-5E-C7-C2-4E-62-D6-F8-C5-F9-5E-F9-37-F4-1A-E9-35-3A-74-6F-6B-65-6E-32-30-3A-27-35-A9-98-D6-2C-B2-78-C6-38-84-EC-3F-A3-24-A9-50-8B-FC-F6-65-31-3A-74-32-3A-00-02-31-3A-76-34-3A-55-54-AC-3A-31-3A-79-31-3A-72-65".Split('-');
            var bytes = new List<byte>();
            foreach (var s in str)
            {
                bytes.Add(Convert.ToByte(s, 16));
            }
            var dic = BEncoder.Decode(bytes.ToArray());
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
    }
}
