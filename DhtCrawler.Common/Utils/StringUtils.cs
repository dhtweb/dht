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
    }
}
