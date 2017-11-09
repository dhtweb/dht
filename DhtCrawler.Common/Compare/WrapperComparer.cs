using System;
using System.Collections.Generic;

namespace DhtCrawler.Common.Compare
{
    public class WrapperComparer<T> : IComparer<T>
    {
        private readonly Func<T, T, int> _compareFunc;

        public WrapperComparer(Func<T, T, int> compareFunc)
        {
            _compareFunc = compareFunc;
        }

        public int Compare(T x, T y)
        {
            return _compareFunc(x, y);
        }
    }
}
