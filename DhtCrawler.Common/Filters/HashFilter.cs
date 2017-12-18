using System.Collections.Generic;

namespace DhtCrawler.Common.Filters
{
    public class HashFilter<T> : IFilter<T>
    {
        private readonly HashSet<T> _hashSet;

        public HashFilter()
        {
            _hashSet = new HashSet<T>();
        }

        public bool Contain(T item)
        {
            return _hashSet.Contains(item);
        }

        public void Add(T item)
        {
            _hashSet.Add(item);
        }
    }
}