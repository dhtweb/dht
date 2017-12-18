namespace DhtCrawler.Common.Filters
{
    public interface IFilter<in T>
    {
        bool Contain(T item);

        void Add(T item);
    }
}