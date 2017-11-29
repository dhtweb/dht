using System.Threading.Tasks;

namespace DhtCrawler.Common.Queue
{
    public interface IQueue<T>
    {
        int Length { get; }
        void Enqueue(T item);
        T Dequeue();
        Task<T> DequeueAsync();
    }
}
