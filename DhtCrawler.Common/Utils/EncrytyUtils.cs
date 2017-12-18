using System.Security.Cryptography;
using System.Text;

namespace DhtCrawler.Common.Utils
{
    public static class EncrytyUtils
    {
        public static string GetMd5(byte[] array)
        {
            using (var provider = MD5.Create())
            {
                var buffer = provider.ComputeHash(array);
                return buffer.ToHex();
            }
        }

        public static string GetMd5(string str)
        {
            return GetMd5(Encoding.UTF8.GetBytes(str));
        }
    }
}
