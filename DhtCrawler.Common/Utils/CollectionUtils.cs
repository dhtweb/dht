using System.Collections.Generic;

namespace DhtCrawler.Common.Utils
{
    public static class CollectionUtils
    {
        public static bool IsEmpty<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count <= 0;
        }
    }
}
