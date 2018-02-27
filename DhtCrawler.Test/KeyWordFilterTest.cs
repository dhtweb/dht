using DhtCrawler.Common.Filters;
using Xunit;

namespace DhtCrawler.Test
{
    public class KeyWordFilterTest
    {
        private const string testStr = "2014weekly36_新唐人电视台_中国禁闻_马三家_活摘器官_翻墙_软件_石涛评述_新闻_退党_9ping_神韵_希望之声_大纪元.rar";

        [Fact]
        static void ContainTest()
        {
            var filter = new KeyWordFilter("退党", "新唐人电视台", "大纪元");
            Assert.True(filter.Contain(testStr));
        }
    }
}
