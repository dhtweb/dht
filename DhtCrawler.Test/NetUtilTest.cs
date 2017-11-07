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
            var ip = IPAddress.Parse("248.64.48.247");
            Assert.True(!ip.IsPublic());
        }
    }
}
