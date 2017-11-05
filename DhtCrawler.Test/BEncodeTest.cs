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
                "64-31-3A-72-64-32-3A-69-64-32-30-3A-3A-FB-DB-2A-5A-75-8A-D5-A5-E1-1E-AC-45-D9-B8-4E-4B-E3-1A-EC-35-3A-6E-6F-64-65-73-32-30-38-3A-3A-FB-E9-67-1C-95-C9-84-E6-85-0C-A0-5A-D7-DA-CE-38-00-F0-C2-65-AD-57-44-AA-5B-3A-FB-E3-CB-11-2B-15-17-4C-7C-55-3B-8F-8E-48-19-A5-43-B6-0A-4F-76-5D-D8-1A-E1-3A-FB-FE-D6-AE-52-90-49-F1-F1-BB-E9-EB-B3-A6-DB-3C-87-0C-E1-55-AB-E4-E9-F2-24-3A-FB-F9-79-31-29-40-18-14-E6-27-D1-31-57-ED-AC-86-5D-48-61-4F-AD-22-3E-23-27-3A-FB-F8-E2-A2-55-58-95-C7-21-6D-7B-64-35-E4-E7-5F-DE-33-AB-C2-98-1E-99-81-17-3A-FB-F8-B8-0A-50-BD-EA-DC-49-34-97-43-8A-40-6A-87-9B-62-6C-6F-0E-2D-F2-A4-C1-3A-FB-F7-EB-B3-A6-DB-3C-87-0C-3E-99-24-5E-0D-1C-06-B7-47-BB-27-3C-65-7B-71-67-3A-FB-F2-77-38-13-16-94-66-C9-F9-23-68-71-3C-0F-2B-08-B7-CC-5F-F5-56-7F-C9-21-35-3A-74-6F-6B-65-6E-38-3A-1C-4F-F8-8C-81-A6-9D-4F-36-3A-76-61-6C-75-65-73-6C-36-3A-9A-2D-D8-EC-04-2C-36-3A-52-F7-85-DC-00-01-65-65-31-3A-74-32-3A-B3-04-31-3A-79-31-3A-72-65".Split('-');
            var bytes = new List<byte>();
            foreach (var s in str)
            {
                bytes.Add(Convert.ToByte(s, 16));
            }
            var dic = BEncoder.Decode(bytes.ToArray());
            Console.WriteLine(dic);
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
