using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using DhtCrawler.Common.Compare;
using DhtCrawler.Common.Extension;

namespace DhtCrawler.Common.Collections
{
    public class ConcurrentHashSet<T> : ISet<T>
    {
        public enum ConcurrentLevel
        {
            Default = 4
        }

        private readonly ReaderWriterLockSlim[] rwLockSlims;
        private readonly HashSet<T>[] hashSets;
        private volatile int count;


        public ConcurrentHashSet() : this(ConcurrentLevel.Default)
        {

        }

        public ConcurrentHashSet(ConcurrentLevel concurrentLevel) : this(concurrentLevel, null, null)
        {

        }

        public ConcurrentHashSet(ConcurrentLevel concurrentLevel, Func<T, T, bool> compare, Func<T, int> hash)
        {
            var level = (int)concurrentLevel;
            rwLockSlims = new ReaderWriterLockSlim[level];
            hashSets = new HashSet<T>[level];
            for (byte i = 0; i < level; i++)
            {
                rwLockSlims[i] = new ReaderWriterLockSlim();
                if (compare != null && hash != null)
                {
                    hashSets[i] = new HashSet<T>(new WrapperEqualityComparer<T>(compare, hash));
                }
                else
                {
                    hashSets[i] = new HashSet<T>();
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < hashSets.Length; i++)
            {
                var rwLockSlim = rwLockSlims[i];
                var hashSet = hashSets[i];
                using (rwLockSlim.EnterRead())
                {
                    foreach (var item in hashSet)
                    {
                        yield return item;
                    }
                }
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

        private bool DoInvoke(T item, Func<ReaderWriterLockSlim, HashSet<T>, T, bool> excutor)
        {
            var hashCode = item.GetHashCode();
            var index = Math.Abs(hashCode % rwLockSlims.Length);
            var rwLockSlim = rwLockSlims[index];
            var hashSet = hashSets[index];
            return excutor(rwLockSlim, hashSet, item);
        }

        public bool Add(T item)
        {
            return DoInvoke(item, (rwLockSlim, hashSet, it) =>
            {
                var flag = false;
                using (rwLockSlim.EnterWrite())
                {
                    flag = hashSet.Add(it);
                }
                if (flag)
                {
                    Interlocked.Increment(ref count);
                }
                return flag;
            });
        }

        public void Clear()
        {
            for (int i = 0; i < hashSets.Length; i++)
            {
                var rwLockSlim = rwLockSlims[i];
                var hashSet = hashSets[i];
                using (rwLockSlim.EnterWrite())
                {
                    var size = hashSet.Count;
                    hashSet.Clear();
                    while (size > 0)
                    {
                        var pre = count;
                        if (Interlocked.CompareExchange(ref count, pre - size, pre) == pre)
                        {
                            break;
                        }
                    }
                }

            }
        }

        public bool Contains(T item)
        {
            return DoInvoke(item, (rwLockSlim, hashSet, it) =>
            {
                using (rwLockSlim.EnterRead())
                {
                    return hashSet.Contains(it);
                }
            });

        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            return DoInvoke(item, (rwLockSlim, hashSet, it) =>
            {
                var flag = false;
                using (rwLockSlim.EnterWrite())
                {
                    flag = hashSet.Remove(it);
                }
                if (flag)
                {
                    Interlocked.Increment(ref count);
                }
                return flag;
            });
        }

        public int Count
        {
            get
            {
                return count;
            }
        }

        public bool IsReadOnly => false;
    }
}
