using System;
using System.Collections.Generic;
using DhtCrawler.Common.Collections.Tree;

namespace DhtCrawler.Common.Collections
{
    public class SortTreeList<T> : AbstractBinaryTree<T>, IList<T>
    {
        private readonly IComparer<T> _comparer;

        public SortTreeList() : this(Comparer<T>.Default)
        {

        }
        public SortTreeList(IComparer<T> comparer)
        {
            this._comparer = comparer;
        }

        private void Add(TreeNode<T> node, T item)
        {
            while (true)
            {
                if (_comparer.Compare(item, node.Value) >= 0)
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

        public override bool Add(T item)
        {
            if (_root == null)
            {
                _root = new TreeNode<T>(item);
                _count++;
                return true;
            }
            Add(_root, item);
            _count++;
            return true;
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public bool Contains(T item)
        {
            if (_root == null)
                return false;
            var node = _root;
            while (node != null)
            {
                if (_comparer.Compare(item, node.Value) == 0)
                {
                    return true;
                }
                node = _comparer.Compare(item, node.Value) > 0 ? node.Right : node.Left;
            }
            return false;
        }

        public override bool Remove(T item)
        {
            if (_root == null)
            {
                return false;
            }

            TreeNode<T> node = _root, parent = null;
            while (node != null)
            {
                var flag = _comparer.Compare(item, node.Value);
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
                        node.Value = mvNode.Value;
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
            if (index >= _count)
            {
                throw new IndexOutOfRangeException();
            }
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
