using System;
using System.Collections;
using System.Linq;

namespace DhtCrawler.Common.Filters
{
    public class BloomFilter<T> : IFilter<T>
    {
        private readonly BitArray _bitArray;
        private readonly int[] _seeds;
        private readonly Func<int, T, int> _hashFunc;
        public BloomFilter(int size, int hashSize, Func<int, T, int> hash)
        {
            _bitArray = new BitArray(size * hashSize);
            _seeds = new int[hashSize];
            var rand = new Random();
            for (var i = 0; i < _seeds.Length; i++)
            {
                int ri;
                do
                {
                    ri = rand.Next();
                } while (_seeds.Contains(ri) || !IsSt(ri));
                _seeds[i] = ri;
            }
            _hashFunc = hash;
        }
        /// <summary>
        /// 是否素数
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private static bool IsSt(int n)
        {
            int m = (int)Math.Sqrt(n);
            for (int i = 2; i <= m; i++)
            {
                if (n % i == 0 && i != n)
                    return false;
            }
            return true;
        }

        public bool Contain(T item)
        {
            for (var i = 0; i < _seeds.Length; i++)
            {
                var hash = _hashFunc(_seeds[i], item) % _bitArray.Count;
                if (!_bitArray[hash])
                {
                    return false;
                }
            }
            return true;
        }

        public void Add(T item)
        {
            for (var i = 0; i < _seeds.Length; i++)
            {
                var hash = _hashFunc(_seeds[i], item) % _bitArray.Count;
                _bitArray[hash] = true;
            }
        }
    }
}