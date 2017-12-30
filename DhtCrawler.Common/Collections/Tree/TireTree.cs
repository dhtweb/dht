using System.Collections.Generic;

namespace DhtCrawler.Common.Collections.Tree
{
    class TireTreeNode<T>
    {
        public IDictionary<T, TireTreeNode<T>> Next { get; set; }
        public bool IsOver { get; set; }
    }
    public class TireTree<T, TI> where T : IList<TI>
    {
        private IDictionary<TI, TireTreeNode<TI>> _head;
        private readonly EqualityComparer<TI> _comparer;
        public int Count { get; private set; }
        public TireTree() : this(EqualityComparer<TI>.Default)
        {

        }

        public TireTree(EqualityComparer<TI> comparer)
        {
            _comparer = comparer;
        }

        public bool Contain(T item)
        {
            if (_head == null || item == null)
            {
                return false;
            }
            var next = _head;
            for (int i = 0, l = item.Count - 1; i < item.Count; i++)
            {
                var it = item[i];
                if (!next.TryGetValue(it, out var node))
                {
                    return false;
                }
                if (node.IsOver && i == l)//item最后的节点标记为是word
                {
                    return true;
                }
                next = node.Next;
            }
            return false;
        }

        public void Add(T item)
        {
            if (_head == null)
            {
                _head = new Dictionary<TI, TireTreeNode<TI>>(_comparer);
            }
            var next = _head;
            for (int i = 0, j = item.Count - 1; i < item.Count; i++)
            {
                var it = item[i];
                if (next.TryGetValue(it, out var node))
                {
                    next = node.Next;
                }
                else
                {
                    node = next[it] = new TireTreeNode<TI>() { Next = new Dictionary<TI, TireTreeNode<TI>>() };
                    next = next[it].Next;
                }
                if (i == j && !node.IsOver)
                {
                    node.IsOver = true;
                    Count++;
                }
            }
        }
    }
}