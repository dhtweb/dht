namespace DhtCrawler.Common.Filters
{
    public class EmptyFilter<T> : IFilter<T>
    {

        public bool Contain(T item)
        {
            return false;
        }

        public void Add(T item)
        {
        }
    }
}