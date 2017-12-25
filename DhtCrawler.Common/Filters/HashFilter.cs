using DhtCrawler.Common.Collections;

namespace DhtCrawler.Common.Filters
{
    public class HashFilter<T> : IFilter<T>
    {
        private readonly ConcurrentHashSet<T> _hashSet;

        public HashFilter()
        {
            _hashSet = new ConcurrentHashSet<T>();
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