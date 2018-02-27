using DhtCrawler.Common.Filters;
using Xunit;

namespace DhtCrawler.Test
{
    public class KeyWordFilterTest
    {
        private const string testStr = "2014weekly36_�����˵���̨_�й�����_������_��ժ����_��ǽ_���_ʯ������_����_�˵�_9ping_����_ϣ��֮��_���Ԫ.rar";

        [Fact]
        static void ContainTest()
        {
            var filter = new KeyWordFilter("�˵�", "�����˵���̨", "���Ԫ");
            Assert.True(filter.Contain(testStr));
        }
    }
}
