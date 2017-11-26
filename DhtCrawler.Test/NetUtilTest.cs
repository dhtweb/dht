using System;
using System.Net;
using DhtCrawler.Common.Utils;
using Xunit;

namespace DhtCrawler.Test
{
    public class NetUtilTest
    {

        [Fact]
        static void IsPublicIpTest()
        {
            var ip = IPAddress.Parse("255.115.56.163");
            Console.WriteLine(ip.IsPublic());
            Assert.True(!ip.IsPublic());
        }
    }
}
