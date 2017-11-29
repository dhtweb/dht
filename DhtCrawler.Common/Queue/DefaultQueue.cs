using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DhtCrawler.Common.Queue
{
    public class DefaultQueue<T> : IQueue<T>
    {
        private int _length;
        private readonly BufferBlock<T> _queue;

        public DefaultQueue()
        {
            _queue = new BufferBlock<T>();
        }

        public int Length => _length;
        public void Enqueue(T item)
        {
            if (_queue.Post(item))
                Interlocked.Increment(ref _length);
        }

        public T Dequeue()
        {
            T item = _queue.Receive();
            Interlocked.Decrement(ref _length);
            return item;
        }

        public async Task<T> DequeueAsync()
        {
            T item = await _queue.ReceiveAsync();
            Interlocked.Decrement(ref _length);
            return item;
        }
    }
}
