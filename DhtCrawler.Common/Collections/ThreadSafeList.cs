using System.Collections;
using System.Collections.Generic;

namespace DhtCrawler.Common.Collections
{
    public class ThreadSafeList<T> : IList<T>
    {
        private readonly List<T> _innerList;
        public readonly object SyncRoot = new object();
        public ThreadSafeList()
        {
            _innerList = new List<T>();
        }

        public T this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return _innerList[index];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    _innerList[index] = value;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return _innerList.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            lock (SyncRoot)
            {
                _innerList.Add(item);
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                _innerList.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (SyncRoot)
            {
                return _innerList.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (SyncRoot)
            {
                _innerList.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            T[] snapShots;
            lock (SyncRoot)
            {
                snapShots = _innerList.ToArray();
            }
            for (int i = 0; i < snapShots.Length; i++)
            {
                yield return snapShots[i];
            }
        }

        public int IndexOf(T item)
        {
            lock (SyncRoot)
            {
                return _innerList.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (SyncRoot)
            {
                _innerList.Insert(index, item);
            }
        }

        public bool Remove(T item)
        {
            lock (SyncRoot)
            {
                return _innerList.Remove(item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (SyncRoot)
            {
                _innerList.RemoveAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
