using System;
using System.Collections;
using System.Collections.Generic;

namespace DhtCrawler.Common.Collections
{
    public class SortTreeList<T> : IList<T>
    {
        private class TreeNode<TN>
        {
            public TN Data { get; set; }
            public TreeNode<TN> Left { get; set; }
            public TreeNode<TN> Right { get; set; }
            public TreeNode(TN data)
            {
                this.Data = data;
            }
        }

        private int count;
        private IComparer<T> comparer;
        private TreeNode<T> root;
        public SortTreeList(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }
        public IEnumerator<T> GetEnumerator()
        {
            if (root == null)
            {
                yield break;
            }
            var node = root;
            var stack = new Stack<TreeNode<T>>();
            stack.Push(node);
            while (stack.Count > 0)
            {
                while (node.Left != null)
                {
                    stack.Push(node.Left);
                    node = node.Left;
                }
                node = stack.Pop();
                yield return node.Data;
                while (stack.Count > 0 && node.Right == null)
                {
                    node = stack.Pop();
                    yield return node.Data;
                }
                if (node.Right == null)
                {
                    break;
                }
                stack.Push(node.Right);
                node = node.Right;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void Add(TreeNode<T> node, T item)
        {
            while (true)
            {
                if (comparer.Compare(item, node.Data) >= 0)
                {
                    if (node.Right == null)
                    {
                        node.Right = new TreeNode<T>(item);
                        return;
                    }
                    node = node.Right;
                }
                else
                {
                    if (node.Left == null)
                    {
                        node.Left = new TreeNode<T>(item);
                        return;
                    }
                    node = node.Left;
                }
            }
        }
        public void Add(T item)
        {
            if (root == null)
            {
                root = new TreeNode<T>(item);
                return;
            }
            Add(root, item);
        }

        public void Clear()
        {
            this.count = 0;
            if (root != null)
            {
                root.Left = root.Right = null;
            }
            root = null;
        }

        public bool Contains(T item)
        {
            if (root == null)
                return false;
            var node = root;
            while (node != null)
            {
                if (comparer.Compare(item, node.Data) == 0)
                {
                    return true;
                }
                node = comparer.Compare(item, node.Data) > 0 ? node.Right : node.Left;
            }
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (var node in this)
            {
                array[arrayIndex++] = node;
            }
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public int Count => count;
        public bool IsReadOnly => false;
        public int IndexOf(T item)
        {
            if (root == null)
                return -1;
            var index = 0;
            foreach (var it in this)
            {
                if (comparer.Compare(it, item) == 0)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException("the list is sorted,not suport this operation");
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public T this[int index]
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
    }
}
