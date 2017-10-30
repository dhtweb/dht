﻿using System;
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
            public string RouteId
            {
                get
                {
                    return Node == null ? string.Empty : Node.Host + ":" + Node.Port;
                }
            }
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

        private byte[] computeRouteDistance(byte[] sourceId, byte[] targetId)
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
            if (node.NodeId == null)
                return;
            var route = new Route()
            {
                Node = node,
                LastTime = DateTime.Now.Ticks
            };
            if (_kTable.Count >= _maxNodeSize && !_kTable.ContainsKey(route.RouteId))
            {
                return;
            }
            _kTable.AddOrUpdate(route.RouteId, route, (k, n) =>
            {
                n.LastTime = DateTime.Now.Ticks;
                return n;
            });
        }

        public IList<DhtNode> FindNodes(byte[] id)
        {
            var list = new SortedList<byte[], DhtNode>(8, RouteComparer.Instance);//大的排在前，小的排在后
            foreach (var item in _kTable.Values)
            {
                if (DateTime.Now.Ticks - item.LastTime > RouteLife.Ticks)
                {
                    _kTable.TryRemove(item.RouteId, out Route route);
                    continue;
                }
                var distance = computeRouteDistance(item.Node.NodeId, id);
                if (list.Count >= 8)
                {
                    if (RouteComparer.Instance.Compare(list.Keys[0], distance) >= 0)//keys<distance时=1 最大的距离大于新节点距离则跳过，如果
                    {
                        continue;
                    }
                    list.RemoveAt(0);
                }
                list.Add(distance, item.Node);
            }
            return list.Values;
        }

        #region IEnumerable
        public IEnumerator<DhtNode> GetEnumerator()
        {
            foreach (var item in _kTable.Values)
            {
                if (DateTime.Now.Ticks - item.LastTime > RouteLife.Ticks)
                {
                    _kTable.TryRemove(item.RouteId, out Route route);
                    continue;
                }
                yield return item.Node;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

    }
}