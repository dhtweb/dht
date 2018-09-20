using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace DhtCrawler.Common.Collections.Queue
{
    public class DelayQueue : ICollection<Action>
    {
        private class DelayQueueItem
        {
            public long DelayTime { get; set; }
            public Action Method { get; set; }
        }
        private object syncRoot = new object();
        private int size = 0;
        private PriorityQueue<DelayQueueItem> queue;

        public int Count => size;

        public bool IsReadOnly => false;

        public DelayQueue()
        {
            queue = new PriorityQueue<DelayQueueItem>(Comparer<DelayQueueItem>.Create((x, y) =>
            {
                var r = x.DelayTime - y.DelayTime;
                if (r == 0)
                {
                    return 0;
                }
                else if (r > 0)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }), 16);
        }


        public Action Peek()
        {
            bool hasLock = false;
            try
            {
                Monitor.Enter(syncRoot, ref hasLock);
                while (true)
                {
                    if (queue.TryPeek(out var item))
                    {
                        return item.Method;
                    }
                    Monitor.Wait(syncRoot);
                }
            }
            finally
            {
                if (hasLock)
                {
                    Monitor.Exit(syncRoot);
                }
            }
        }

        public Action Dequeue()
        {
            return Dequeue(Timeout.Infinite);
        }

        public Action Dequeue(long waitTime)
        {
            bool hasLock = false, infiniteWait = Timeout.Infinite == waitTime;
            try
            {
                Monitor.Enter(syncRoot, ref hasLock);
                while (true)
                {
                    var delayTime = 0;
                    if (queue.TryPeek(out var delayItem))
                    {
                        var now = DateTime.Now.Ticks;
                        delayTime = (int)(delayItem.DelayTime - now) / 10000;
                        if (delayTime <= 1)
                        {
                            size--;
                            delayItem = queue.Dequeue();
                            return delayItem.Method;
                        }
                    }
                    else if (infiniteWait)
                    {
                        Monitor.Wait(syncRoot);
                        continue;
                    }
                    if (waitTime <= 0)
                    {
                        return null;
                    }
                    var waitMillis = (int)(waitTime / 10000);
                    delayTime = delayTime > 0 && delayTime < waitMillis ? delayTime : waitMillis;
                    var waitBefore = DateTime.Now.Ticks;
                    Monitor.Wait(syncRoot, delayTime);
                    var waitAfter = DateTime.Now.Ticks;
                    waitTime = waitTime - (waitAfter - waitBefore);
                }
            }
            finally
            {
                if (hasLock)
                {
                    Monitor.Exit(syncRoot);
                }
            }
        }

        public void Enqueue(Action item, long delayTime)
        {
            bool hasLock = false;
            try
            {
                Monitor.Enter(syncRoot, ref hasLock);
                size++;
                queue.Enqueue(new DelayQueueItem() { Method = item, DelayTime = DateTime.Now.Ticks + delayTime });
                Monitor.Pulse(syncRoot);
            }
            finally
            {
                if (hasLock)
                {
                    Monitor.Exit(syncRoot);
                }
            }
        }

        public void Add(Action item)
        {
            Enqueue(item, 0);
        }

        public void Clear()
        {
            lock (queue)
            {
                queue.Clear();
            }
        }

        public bool Contains(Action item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Action[] destArray, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(Action item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<Action> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
