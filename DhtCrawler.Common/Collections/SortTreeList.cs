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

        private int _count;
        private readonly IComparer<T> _comparer;
        private TreeNode<T> _root;

        public SortTreeList() : this(Comparer<T>.Default)
        {

        }
        public SortTreeList(IComparer<T> comparer)
        {
            this._comparer = comparer;
        }
        public IEnumerator<T> GetEnumerator()
        {
            if (_root == null)
            {
                yield break;
            }
            var node = _root;
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
                if (_comparer.Compare(item, node.Data) >= 0)
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
            if (_root == null)
            {
                _root = new TreeNode<T>(item);
                _count++;
                return;
            }
            Add(_root, item);
            _count++;
        }

        public void Clear()
        {
            if (_root != null)
            {
                _root.Left = _root.Right = null;
            }
            _root = null;
            this._count = 0;
        }

        public bool Contains(T item)
        {
            if (_root == null)
                return false;
            var node = _root;
            while (node != null)
            {
                if (_comparer.Compare(item, node.Data) == 0)
                {
                    return true;
                }
                node = _comparer.Compare(item, node.Data) > 0 ? node.Right : node.Left;
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
            if (_root == null)
            {
                return false;
            }

            TreeNode<T> node = _root, parent = null;
            while (node != null)
            {
                var flag = _comparer.Compare(item, node.Data);
                if (flag == 0)
                {
                    TreeNode<T> nextNode;
                    if (node.Right == null)
                    {
                        nextNode = node.Left;
                    }
                    else if (node.Left == null)
                    {
                        nextNode = node.Right;
                    }
                    else
                    {
                        TreeNode<T> mvNode = node.Right, mvParent = node;
                        //找到右节点
                        while (mvNode.Left != null)
                        {
                            mvParent = mvNode;
                            mvNode = mvNode.Left;
                        }
                        node.Data = mvNode.Data;
                        if (mvNode == mvParent.Left)
                        {
                            mvParent.Left = mvNode.Right;
                        }
                        else
                        {
                            mvParent.Right = mvNode.Right;
                        }
                        _count--;
                        return true;
                    }
                    if (parent == null)
                    {
                        _root = nextNode;
                    }
                    else
                    {
                        var isLeft = node == parent.Left;
                        if (isLeft)
                        {
                            parent.Left = nextNode;
                        }
                        else
                        {
                            parent.Right = nextNode;
                        }
                    }
                    _count--;
                    return true;
                }
                parent = node;
                node = flag > 0 ? node.Right : node.Left;
            }
            return false;
        }

        public int Count => _count;
        public bool IsReadOnly => false;
        public int IndexOf(T item)
        {
            if (_root == null)
                return -1;
            var index = 0;
            foreach (var it in this)
            {
                if (_comparer.Compare(it, item) == 0)
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
            get
            {
                if (index >= _count)
                {
                    throw new IndexOutOfRangeException();
                }
                var i = 0;
                foreach (var it in this)
                {
                    if (i == index)
                    {
                        return it;
                    }
                    i++;
                }
                return default(T);
            }
            set => throw new NotSupportedException("the list is sorted,not suport this operation");
        }
    }
}
