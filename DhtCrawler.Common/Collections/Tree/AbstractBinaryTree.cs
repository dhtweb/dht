using System.Collections;
using System.Collections.Generic;

namespace DhtCrawler.Common.Collections.Tree
{
    public abstract class AbstractBinaryTree<T> : IEnumerable<T>
    {
        protected int _count;
        protected TreeNode<T> _root;

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
                yield return node.Value;
                while (stack.Count > 0 && node.Right == null)
                {
                    node = stack.Pop();
                    yield return node.Value;
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

        public abstract bool Add(T item);

        public abstract bool Remove(T item);

        public void Clear()
        {
            if (_root != null)
            {
                _root.Left = _root.Right = null;
            }
            _root = null;
            this._count = 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (var node in this)
            {
                array[arrayIndex++] = node;
            }
        }

        public int Count => _count;

        public bool IsReadOnly => false;
    }
}
