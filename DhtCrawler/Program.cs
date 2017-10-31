using DhtCrawler.DHT;
using System;
using System.Collections.Generic;
using System.Threading;
using DhtCrawler.Encode;

namespace DhtCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            TestBEncode();
            return;
            var locker = new ManualResetEvent(false);
            var dhtClient = new DhtClient(53386);
            Console.CancelKeyPress += (sender, e) =>
            {
                dhtClient.ShutDown();
                locker.Set();
                e.Cancel = true;
            };
            dhtClient.Run();
            locker.WaitOne();
        }

        static void TestBEncode()
        {
            var str =
                "64-31-3A-72-64-32-3A-69-64-32-30-3A-C7-40-59-75-1B-A8-34-E9-EC-F4-58-A4-81-1A-73-ED-1C-27-B5-27-35-3A-6E-6F-64-65-73-32-30-38-3A-28-50-FB-86-06-28-BE-C5-37-AD-69-EF-10-65-E2-B8-42-5E-78-EC-36-BD-2C-7D-3B-1C-2E-31-DD-8C-F0-B4-18-E9-E2-09-47-BA-81-6D-2C-C4-83-74-D4-EF-53-07-07-6F-62-CA-26-75-70-93-29-72-6B-47-BD-C1-E5-46-0B-F7-A2-6D-FF-24-8B-AC-6C-2E-E1-B4-C8-D5-27-18-2D-49-25-04-7E-80-1F-81-0B-0D-16-02-93-90-7B-47-AA-7E-76-20-9B-0D-AE-3B-26-68-D5-D6-AE-52-90-49-F1-F1-BB-E9-EB-B3-A6-DB-3C-87-0C-E1-77-C1-A4-EE-29-29-26-9F-4D-51-C7-3C-8C-87-84-91-2A-AF-50-E4-B5-16-16-72-05-7F-D3-33-5A-0C-A0-10-27-FD-D6-77-17-DB-26-37-14-96-0E-1F-D6-52-C3-E6-5F-C1-2A-73-5B-79-AB-98-CD-2C-26-D6-4D-EC-82-09-EF-2C-CF-32-F9-DE-3E-A0-4D-0C-21-A1-C2-74-5D-92-B9-91-1A-E9-65-31-3A-74-30-3A-31-3A-79-31-3A-72-65".Split('-');
            var bytes = new List<byte>();
            foreach (var s in str)
            {
                bytes.Add(Convert.ToByte(s, 16));
            }
            var dic = BEncoder.Decode(bytes.ToArray());
            Console.WriteLine(dic);
        }
    }
}
