using System;
using System.Text.RegularExpressions;

namespace DhtCrawler.Common.Utils
{
    public static class StringUtils
    {
        private static readonly Regex NumbeRegex = new Regex("^[0-9]*$", RegexOptions.Compiled);
        public static bool IsBlank(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static bool IsNumber(this string str)
        {
            if (str.IsBlank())
                return false;
            return NumbeRegex.IsMatch(str);
        }

        public static byte[] HexStringToByteArray(this string str)
        {
            if (str.IsBlank())
                return new byte[0];
            if (str.Length % 2 == 1)
            {
                str = str + " ";
            }
            var bytes = new byte[str.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(str.Substring(2 * i, 2), 16);
            }
            return bytes;
        }
    }
}
