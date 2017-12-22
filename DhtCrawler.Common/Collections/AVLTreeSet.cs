using System;
using System.Collections;
using System.Collections.Generic;

namespace DhtCrawler.Common.Collections
{
    public enum Balance
    {
        EH, LH, RH
    }
    public class TreeNode<T>
    {
        public TreeNode<T> Left { get; set; }
        public TreeNode<T> Right { get; set; }
        public T Value { get; set; }
        public Balance Balance { get; set; }
    }
    public class AVLTreeSet<T> : ISet<T>
    {
        private TreeNode<T> _root;
        private IComparer<T> _comparer;
        private int _count;
        public AVLTreeSet(IComparer<T> comparer)
        {
            this._comparer = comparer;
        }

        public AVLTreeSet() : this(Comparer<T>.Default)
        {

        }
        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void ICollection<T>.Add(T item)
        {
            ((ISet<T>)this).Add(item);
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

        public static TreeNode<T> LeftRotate(TreeNode<T> node)
        {
            var next = node.Right;
            node.Right = next.Left;
            next.Left = node;
            return next;
        }

        public static TreeNode<T> RightRotate(TreeNode<T> node)
        {
            var next = node.Left;
            node.Left = next.Right;
            next.Right = node;
            return next;
        }

        private static TreeNode<T> RightRotateTree(TreeNode<T> node)
        {
            var left = node.Left;
            switch (left.Balance)
            {
                case Balance.LH:
                    node.Balance = left.Balance = Balance.EH;
                    return RightRotate(node);
                case Balance.RH:
                    //左旋后右旋
                    left.Balance = Balance.LH;
                    node.Left = LeftRotate(node.Left);
                    return RightRotate(node);
            }
            throw new ArgumentException("树结构错误");
        }

        public bool InsertNode(TreeNode<T> node, TreeNode<T> parent, T item)
        {
            var flag = _comparer.Compare(node.Value, item);
            if (flag == 0)
            {
                return false;
            }
            TreeNode<T> addNode;
            bool isLeft = false;
            if (flag > 0)
            {
                if (node.Right == null)
                {
                    switch (node.Balance)
                    {
                        case Balance.EH:
                            node.Balance = Balance.RH;
                            break;
                        case Balance.LH:
                            node.Balance = Balance.EH;
                            break;
                        case Balance.RH:
                            node.Balance = Balance.RH;
                            break;
                    }
                    node.Right = new TreeNode<T>() { Value = item, Balance = Balance.EH };
                    return true;
                }
                addNode = node.Right;
            }
            else
            {
                if (node.Left == null)
                {
                    switch (node.Balance)
                    {
                        case Balance.EH:
                            node.Balance = Balance.LH;
                            break;
                        case Balance.LH:
                            node.Balance = Balance.LH;
                            break;
                        case Balance.RH:
                            node.Balance = Balance.EH;
                            break;
                    }
                    node.Left = new TreeNode<T>() { Value = item };
                    return true;
                }
                addNode = node.Left;
                isLeft = true;
            }
            var result = InsertNode(node.Right, parent, item);
            if (result)
            {
                switch (addNode.Balance)
                {
                    case Balance.EH:
                        break;
                    case Balance.LH:
                        switch (node.Balance)
                        {
                            case Balance.EH: //
                                node.Balance = isLeft ? Balance.LH : Balance.RH;
                                break;
                            case Balance.LH:
                                if (isLeft)
                                {
                                    //右旋平衡
                                    RightRotateTree(node);
                                }
                                else
                                {
                                    node.Balance = Balance.EH;
                                }
                                break;
                            case Balance.RH:
                                if (isLeft)
                                {
                                    node.Balance = Balance.EH;
                                }
                                else
                                {
                                    //左旋平衡
                                }
                                break;
                        }
                        break;
                    case Balance.RH:
                        switch (node.Balance)
                        {
                            case Balance.EH:
                                node.Balance = Balance.RH;
                                break;
                            case Balance.LH:
                                node.Balance = Balance.EH;
                                break;
                            case Balance.RH:
                                //左旋进行平衡
                                break;
                        }
                        break;
                }
            }
            return result;
        }

        public bool Add(T item)
        {
            if (_root == null)
            {
                _root = new TreeNode<T>() { Value = item, Balance = 0 };
                return true;
            }
            TreeNode<T> node = _root, parent = null, rotate = null;
            while (node != null)
            {
                var flag = _comparer.Compare(item, node.Value);
                if (flag == 0)
                {
                    return false;
                }
                if (flag > 0)
                {
                    if (node.Right == null)
                    {
                        if (parent != null)
                        {
                            parent.Balance--;
                        }
                        node.Balance--;
                        node.Right = new TreeNode<T>() { Value = item };
                        break;
                    }
                    rotate = parent;
                    parent = node;
                    node = node.Right;
                }
                else
                {
                    if (node.Left == null)
                    {
                        if (parent != null)
                        {
                            parent.Balance++;
                        }
                        node.Balance++;
                        node.Left = new TreeNode<T>() { Value = item };
                        break;
                    }
                    rotate = parent;
                    parent = node;
                    node = node.Left;
                }
            }
            if (parent == null)
            {
                return true;
            }
            //if (parent.Balance == 2 || parent.Balance == -2)
            //{
            //    if (rotate == null)
            //    {
            //        _root = RotateTree(parent);
            //    }
            //    else
            //    {
            //        if (rotate.Left == parent)
            //        {
            //            rotate.Left = RotateTree(parent);
            //        }
            //        else
            //        {
            //            rotate.Right = RotateTree(parent);
            //        }
            //    }
            //}
            return true;
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            _count--;
            throw new NotImplementedException();
        }

        public int Count => _count;

        public bool IsReadOnly => false;
    }
}
