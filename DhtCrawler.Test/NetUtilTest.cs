using System;
using System.Net;
using DhtCrawler.Utils;
using Xunit;

namespace DhtCrawler.Test
{
    public class NetUtilTest
    {

        [Fact]
        static void IsPublicIpTest()
        {
            var ip = IPAddress.Parse("245.2.45.177");
            Console.WriteLine(ip.IsPublic());
            Assert.True(!ip.IsPublic());
        }
    }
}
