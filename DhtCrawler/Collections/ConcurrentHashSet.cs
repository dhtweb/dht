using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using DhtCrawler.Extension;

namespace DhtCrawler.Collections
{
    public class ConcurrentHashSet<T> : ISet<T>
    {
        private class WrapperComparer<T> : IEqualityComparer<T>
        {

            private readonly Func<T, T, bool> _compareFunc;
            private readonly Func<T, int> _hashFunc;
            public WrapperComparer(Func<T, T, bool> compareFunc, Func<T, int> hashFunc)
            {
                this._compareFunc = compareFunc;
                this._hashFunc = hashFunc;
            }

            public bool Equals(T x, T y)
            {
                return _compareFunc(x, y);
            }

            public int GetHashCode(T obj)
            {
                return _hashFunc(obj);
            }
        }
        private readonly ReaderWriterLockSlim rwLockSlim;
        private readonly HashSet<T> hashSet;
        public ConcurrentHashSet()
        {
            rwLockSlim = new ReaderWriterLockSlim();
            hashSet = new HashSet<T>();
        }

        public ConcurrentHashSet(Func<T, T, bool> compare, Func<T, int> hash)
        {
            rwLockSlim = new ReaderWriterLockSlim();
            hashSet = new HashSet<T>(new WrapperComparer<T>(compare, hash));
        }

        public IEnumerator<T> GetEnumerator()
        {
            try
            {
                rwLockSlim.EnterReadLock();
                return hashSet.GetEnumerator();
            }
            finally
            {
                rwLockSlim.ExitReadLock();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public void UnionWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool Add(T item)
        {
            using (rwLockSlim.EnterWrite())
            {
                return hashSet.Add(item);
            }
        }

        public void Clear()
        {
            using (rwLockSlim.EnterWrite())
                hashSet.Clear();
        }

        public bool Contains(T item)
        {
            using (rwLockSlim.EnterRead())
            {
                return hashSet.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            using (rwLockSlim.EnterRead())
            {
                hashSet.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(T item)
        {
            using (rwLockSlim.EnterWrite())
            {
                return hashSet.Remove(item);
            }
        }

        public int Count
        {
            get
            {
                using (rwLockSlim.EnterRead())
                {
                    return hashSet.Count;
                }
            }
        }

        public bool IsReadOnly => false;
    }
}
