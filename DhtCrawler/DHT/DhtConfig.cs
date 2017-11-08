using System;
using System.Collections.Generic;
using System.Net;

namespace DhtCrawler.DHT
{
    public class DhtConfig
    {
        public static readonly DhtConfig Default = new DhtConfig()
        {
            Port = 0,//随机
            NodeQueueMaxSize = 1024 * 10,
            ReceiveQueueMaxSize = 1024 * 10,
            SendQueueMaxSize = 1024 * 20,
            ReceiveRateLimit = 150,
            SendRateLimit = 150
        };
        /// <summary>
        /// 监听接口
        /// </summary>
        public ushort Port { get; set; }
        /// <summary>
        /// 查找node队列容量
        /// </summary>
        public int NodeQueueMaxSize { get; set; }
        /// <summary>
        /// 接收消息队列容量
        /// </summary>
        public int ReceiveQueueMaxSize { get; set; }
        /// <summary>
        /// 发送消息队列容量
        /// </summary>
        public int SendQueueMaxSize { get; set; }
        /// <summary>
        /// 发送速度限制（kb/s）
        /// </summary>
        public int SendRateLimit { get; set; }
        /// <summary>
        /// 接收速度限制（kb/s）
        /// </summary>
        public int ReceiveRateLimit { get; set; }
    }
}