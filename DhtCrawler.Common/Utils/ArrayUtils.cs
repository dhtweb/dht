using System;

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
    }
}
