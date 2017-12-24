using System;
using System.Collections.Generic;
using DhtCrawler.Common.Collections.Tree;

namespace DhtCrawler.Common.Collections
{
    public enum Balance : byte
    {
        EH, LH, RH
    }
     class AvlTreeNode<T> : TreeNode<T>
    {
        public Balance Balance { get; set; }

        public AvlTreeNode(T val) : base(val)
        {
        }
    }
    public class AVLTreeSet<T> : AbstractBinaryTree<T>, ISet<T>
    {
        private IComparer<T> _comparer;

        public AVLTreeSet(IComparer<T> comparer)
        {
            _comparer = comparer;
        }

        public AVLTreeSet() : this(Comparer<T>.Default)
        {

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

        private static AvlTreeNode<T> LeftRotate(AvlTreeNode<T> node)
        {
            var next = node.Right;
            node.Right = next.Left;
            next.Left = node;
            return (AvlTreeNode<T>)next;
        }

        private static AvlTreeNode<T> RightRotate(AvlTreeNode<T> node)
        {
            var next = node.Left;
            node.Left = next.Right;
            next.Right = node;
            return (AvlTreeNode<T>)next;
        }

        private static AvlTreeNode<T> RightRotateTree(AvlTreeNode<T> node)
        {
            var left = (AvlTreeNode<T>)node.Left;
            switch (left.Balance)
            {
                case Balance.LH:
                    node.Balance = left.Balance = Balance.EH;
                    return RightRotate(node);
                case Balance.RH:
                    //左旋后右旋
                    node.Balance = left.Balance = Balance.EH;
                    node.Left = LeftRotate((AvlTreeNode<T>)node.Left);
                    return RightRotate(node);
            }
            throw new ArgumentException("树结构错误");
        }

        private static AvlTreeNode<T> LeftRotateTree(AvlTreeNode<T> node)
        {
            var right = (AvlTreeNode<T>)node.Right;
            switch (right.Balance)
            {
                case Balance.RH:
                    node.Balance = right.Balance = Balance.EH;
                    return LeftRotate(node);
                case Balance.LH:
                    //左旋后右旋
                    node.Balance = right.Balance = Balance.EH;
                    node.Right = RightRotate((AvlTreeNode<T>)node.Right);
                    return LeftRotate(node);
            }
            throw new ArgumentException("树结构错误");
        }

        private bool InsertNode(AvlTreeNode<T> node, AvlTreeNode<T> parent, T item, ref bool isHigh)
        {
            var flag = _comparer.Compare(item, node.Value);
            if (flag == 0)
            {
                return false;
            }
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
                    isHigh = node.Balance != Balance.EH;
                    node.Right = new AvlTreeNode<T>(item) { Balance = Balance.EH };
                    return true;
                }
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
                    isHigh = node.Balance != Balance.EH;
                    node.Left = new AvlTreeNode<T>(item);
                    return true;
                }
                isLeft = true;
            }
            var result = InsertNode((AvlTreeNode<T>)(isLeft ? node.Left : node.Right), node, item, ref isHigh);
            if (result)
            {
                if (isHigh)
                {
                    isHigh = false;
                    switch (node.Balance)//长高，需要平衡（需要判断长高的是左节点还是右节点）
                    {
                        case Balance.EH: //
                            node.Balance = isLeft ? Balance.LH : Balance.RH;
                            isHigh = true;
                            break;
                        case Balance.LH:
                            if (isLeft)
                            {
                                //右旋平衡
                                if (parent == null)
                                {
                                    _root = RightRotateTree(node);
                                }
                                else
                                {
                                    parent.Left = RightRotateTree(node);
                                }
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
                                if (parent == null)
                                {
                                    _root = LeftRotateTree(node);
                                }
                                else
                                {
                                    parent.Right = LeftRotateTree(node);
                                }
                            }
                            break;
                    }
                }
            }
            return result;
        }

        public override bool Add(T item)
        {
            if (_root == null)
            {
                _root = new AvlTreeNode<T>(item) { Balance = Balance.EH };
                return true;
            }
            bool top = false;
            if (InsertNode((AvlTreeNode<T>)_root, null, item, ref top))
            {
                _count++;
                return true;
            }
            return false;
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
            _count--;
            throw new NotImplementedException();
        }

    }
}
