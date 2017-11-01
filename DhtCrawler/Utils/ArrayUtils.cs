using System;

namespace DhtCrawler.Utils
{
    public static class ArrayUtils
    {
        public static T[] CopyArray<T>(this T[] array)
        {
            var result = new T[array.Length];
            Array.Copy(array, result, result.Length);
            return result;
        }
    }
}
