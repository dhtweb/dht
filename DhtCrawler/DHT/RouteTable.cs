using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DhtCrawler.DHT
{
    public class RouteTable : IEnumerable<DhtNode>
    {
        private class Route
        {
            public DhtNode Node { get; set; }
            public long LastTime { get; set; }
            public string RouteId => Node == null ? string.Empty : Node.Host + ":" + Node.Port;
        }

        private class RouteComparer : IComparer<byte[]>
        {
            private RouteComparer() { }
            public int Compare(byte[] x, byte[] y)
            {
                var length = Math.Min(x.Length, y.Length);
                for (var i = 0; i < length; i++)
                {
                    if (x[i] == y[i])
                        continue;
                    return x[i] > y[i] ? -1 : 1;

                }
                return x.Length > y.Length ? -1 : 1;
            }

            public static readonly IComparer<byte[]> Instance = new RouteComparer();
        }

        private static readonly TimeSpan RouteLife = TimeSpan.FromMinutes(15);

        private readonly int _maxNodeSize;
        private readonly ConcurrentDictionary<string, Route> _kTable;
        private long _minLastTime = DateTime.Now.Ticks;

        private static byte[] ComputeRouteDistance(byte[] sourceId, byte[] targetId)
        {
            var result = new byte[20];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (byte)(sourceId[i] ^ targetId[i]);
            }
            return result;
        }

        public RouteTable(int nodeSize)
        {
            this._kTable = new ConcurrentDictionary<string, Route>();
            this._maxNodeSize = nodeSize;
        }

        public int Count => _kTable.Count;

        public void AddNode(DhtNode node)
        {
            if (node.NodeId == null || _kTable.Count >= _maxNodeSize)
                return;
            var route = new Route()
            {
                Node = node,
                LastTime = DateTime.Now.Ticks
            };
            _kTable.TryAdd(route.RouteId, route);
        }

        public void AddNodes(IEnumerable<DhtNode> nodes)
        {
            foreach (var node in nodes)
            {
                AddNode(node);
            }
        }

        public void AddOrUpdateNode(DhtNode node)
        {
            if (node.NodeId == null)
                return;
            if (_kTable.Count >= _maxNodeSize && _minLastTime + RouteLife.Ticks < DateTime.Now.Ticks)
            {
                lock (this)
                {
                    if (_minLastTime + RouteLife.Ticks < DateTime.Now.Ticks)
                        ClearExpireNode();
                }
            }
            if (_kTable.Count >= _maxNodeSize)
                return;
            var route = new Route()
            {
                Node = node,
                LastTime = DateTime.Now.Ticks
            };
            _kTable.AddOrUpdate(route.RouteId, route, (k, n) =>
            {
                n.Node = route.Node;
                n.LastTime = DateTime.Now.Ticks;
                return n;
            });
        }

        private void ClearExpireNode()
        {
            var minTime = DateTime.MaxValue.Ticks;
            foreach (var item in _kTable.Values)
            {
                if (DateTime.Now.Ticks - item.LastTime > RouteLife.Ticks)
                {
                    _kTable.TryRemove(item.RouteId, out Route remove);
                    continue;
                }
                minTime = Math.Min(_minLastTime, item.LastTime);
            }
            _minLastTime = Math.Max(minTime, _minLastTime);
        }

        public IList<DhtNode> FindNodes(byte[] id)
        {
            if (_kTable.Count <= 8)
                return _kTable.Values.Take(8).Select(route => route.Node).ToArray();
            var list = new SortedList<byte[], DhtNode>(8, RouteComparer.Instance);//大的排在前，小的排在后
            var minTime = DateTime.MaxValue.Ticks;
            var tableFull = _kTable.Count >= _maxNodeSize;
            foreach (var item in _kTable.Values)
            {
                if (tableFull && DateTime.Now.Ticks - item.LastTime > RouteLife.Ticks)
                {
                    _kTable.TryRemove(item.RouteId, out Route route);
                    continue;
                }
                var distance = ComputeRouteDistance(item.Node.NodeId, id);
                if (list.Count >= 8)
                {
                    if (RouteComparer.Instance.Compare(list.Keys[0], distance) >= 0)//keys<distance时=1 最大的距离大于新节点距离则跳过，如果
                    {
                        continue;
                    }
                    list.RemoveAt(0);
                }
                list.Add(distance, item.Node);
                minTime = Math.Min(_minLastTime, item.LastTime);
            }
            _minLastTime = Math.Max(minTime, _minLastTime);
            return list.Values;
        }

        #region IEnumerable
        public IEnumerator<DhtNode> GetEnumerator()
        {
            var minTime = DateTime.MaxValue.Ticks;
            var tableFull = _kTable.Count >= _maxNodeSize;
            foreach (var item in _kTable.Values)
            {
                if (tableFull && DateTime.Now.Ticks - item.LastTime > RouteLife.Ticks)
                {
                    _kTable.TryRemove(item.RouteId, out Route route);
                    continue;
                }
                minTime = Math.Min(_minLastTime, item.LastTime);
                yield return item.Node;
            }
            _minLastTime = Math.Max(minTime, _minLastTime);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}