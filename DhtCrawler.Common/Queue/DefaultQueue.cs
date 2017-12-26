using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DhtCrawler.Common.Queue
{
    public class DefaultQueue<T> : IQueue<T>
    {
        private readonly BufferBlock<T> _queue;

        public DefaultQueue()
        {
            _queue = new BufferBlock<T>();
        }

        public int Length => _queue.Count;
        public void Enqueue(T item)
        {
            _queue.Post(item);
        }

        public T Dequeue()
        {
            return _queue.Receive();
        }

        public async Task<T> DequeueAsync()
        {
            return await _queue.ReceiveAsync();
        }

        public async Task<T> DequeueAsync(TimeSpan timeout)
        {
            return await _queue.ReceiveAsync(timeout);
        }
    }
}
