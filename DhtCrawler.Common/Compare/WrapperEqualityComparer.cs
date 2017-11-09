using System;
using System.Collections.Generic;

namespace DhtCrawler.Common.Compare
{
    public class WrapperEqualityComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, T, bool> _compareFunc;
        private readonly Func<T, int> _hashFunc;
        public WrapperEqualityComparer(Func<T, T, bool> compareFunc, Func<T, int> hashFunc)
        {
            _compareFunc = compareFunc;
            _hashFunc = hashFunc;
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
}
