using System.Collections.Generic;

namespace DhtCrawler.Common.Collections.Extension
{
    public static class CollectionUtils
    {
        public static bool IsEmpty<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count <= 0;
        }
    }
}
