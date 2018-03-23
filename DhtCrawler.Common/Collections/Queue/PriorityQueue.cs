using System;
using System.Collections;
using System.Collections.Generic;

namespace DhtCrawler.Common.Collections.Queue
{
    public class PriorityQueue<T> : ICollection<T>
    {
        const int DefaultCapacity = 16;
        private IComparer<T> comparer;
        private int size;
        private int capacity;
        private T[] array;

        public int Count => size;

        public bool IsReadOnly => false;

        public PriorityQueue() : this(Comparer<T>.Default, DefaultCapacity) { }

        public PriorityQueue(int capacity) : this(Comparer<T>.Default, capacity) { }

        public PriorityQueue(IComparer<T> comparer, int capacity)
        {
            this.comparer = comparer;
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "");
            this.capacity = capacity;
            this.array = new T[capacity];
        }

        private PriorityQueue(IComparer<T> comparer, T[] array)
        {
            this.comparer = comparer;
            this.array = array;
            this.size = array.Length;
        }

        public T Peek()
        {
            if (size <= 0)
            {
                throw new ArgumentException("this queue is empty");
            }
            return array[0];
        }

        public bool TryPeek(out T item)
        {
            if (size <= 0)
            {
                item = default(T);
                return false;
            }
            item = array[0];
            return true;
        }

        private void HeapDown(T[] array, int index, int lastIndex)
        {
            var left = (index << 1) + 1;
            var mvNode = array[index];
            while (left < lastIndex)
            {
                //大顶堆                
                if (left < lastIndex && comparer.Compare(array[left], array[left + 1]) < 0)
                {
                    left++;
                }
                if (comparer.Compare(mvNode, array[left]) > 0)
                {
                    break;
                }
                array[index] = array[left];
                index = left;
                left = (index << 1) + 1;
            }
            array[index] = mvNode;
        }

        private void HeapUp(T[] array, int index)
        {
            var mvNode = array[index];
            var parent = (index - 1) >> 1;
            while (true)
            {
                if (comparer.Compare(array[parent], mvNode) >= 0)
                {
                    break;
                }
                array[index] = array[parent];
                index = parent;
                if (index <= 0)
                {
                    break;
                }
                parent = (index - 1) >> 1;
            }
            array[index] = mvNode;
        }

        public T Dequeue()
        {
            if (size <= 0)
            {
                throw new ArgumentException("this queue is empty");
            }
            var item = array[0];
            size--;
            if (size > 0)
            {
                array[0] = array[size];
                HeapDown(array, 0, size - 1);
            }
            array[size] = default(T);
            return item;
        }

        public void Enqueue(T item)
        {
            if (size >= array.Length)
            {
                var newArray = new T[array.Length * 2];
                Array.Copy(array, newArray, array.Length);
                array = newArray;
            }
            array[size] = item;
            if (size > 0)
            {
                HeapUp(array, size);
            }
            size++;
        }

        public void Add(T item)
        {
            Enqueue(item);
        }

        public void Clear()
        {
            size = 0;
            array = new T[capacity];
        }

        public bool Contains(T item)
        {
            for (int i = 0; i < size; i++)
            {
                var n = array[i];
                if (comparer.Compare(item, n) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(T[] destArray, int arrayIndex)
        {
            Array.Copy(array, 0, destArray, arrayIndex, size);
        }

        public bool Remove(T item)
        {
            for (int i = 0; i < size; i++)
            {
                var n = array[i];
                if (comparer.Compare(item, n) == 0)
                {
                    size--;
                    array[i] = array[size];
                    HeapDown(array, i, size - 1);
                    array[size] = default(T);
                    return true;
                }
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var backArray = new T[size];
            Array.Copy(array, backArray, size);
            var enumerableQueue = new PriorityQueue<T>(comparer, backArray);
            while (enumerableQueue.Count > 0)
            {
                yield return enumerableQueue.Dequeue();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
