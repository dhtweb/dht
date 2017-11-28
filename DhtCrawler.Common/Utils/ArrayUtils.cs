using System;
using System.Text;

namespace DhtCrawler.Common.Utils
{
    public static class ArrayUtils
    {
        public static T[] CopyArray<T>(this T[] array)
        {
            var result = new T[array.Length];
            Array.Copy(array, result, result.Length);
            return result;
        }

        public static T[] CopyArray<T>(this T[] array, int index, int size)
        {
            var result = new T[size];
            Array.Copy(array, index, result, 0, size);
            return result;
        }

        public static string ToHex(this byte[] array)
        {
            var sb = new StringBuilder(array.Length * 2);
            foreach (byte t in array)
            {
                sb.Append(Convert.ToString(t, 16));
            }
            return sb.ToString();
        }
    }
}
