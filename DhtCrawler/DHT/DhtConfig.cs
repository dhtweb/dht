namespace DhtCrawler.DHT
{
    public class DhtConfig
    {
        /// <summary>
        /// 监听接口(默认随机选择接口)
        /// </summary>
        public ushort Port { get; set; } = 0;

        /// <summary>
        /// 查找node队列容量
        /// </summary>
        public int NodeQueueMaxSize { get; set; } = 10240;

        /// <summary>
        /// 接收消息队列容量
        /// </summary>
        public int ReceiveQueueMaxSize { get; set; } = 10240;
        /// <summary>
        /// 请求消息队列容量
        /// </summary>
        public int RequestQueueMaxSize { get; set; } = 10240;
        /// <summary>
        /// 响应消息队列容量
        /// </summary>
        public int ResponseQueueMaxSize { get; set; } = 10240;
        /// <summary>
        /// 发送消息队列容量
        /// </summary>
        public int SendQueueMaxSize { get; set; } = 10240;

        /// <summary>
        /// 发送速度限制（kb/s）
        /// </summary>
        public int SendRateLimit { get; set; } = 150;
        /// <summary>
        /// 接收速度限制（kb/s）
        /// </summary>
        public int ReceiveRateLimit { get; set; } = 150;

        /// <summary>
        /// 请求消息处理线程数
        /// </summary>
        public int ProcessRequestThreadNum { get; set; } = 1;
        /// <summary>
        /// 响应消息处理线程数
        /// </summary>
        public int ProcessResponseThreadNum { get; set; } = 1;
        /// <summary>
        /// KTable节点大小
        /// </summary>
        public int KTableSize { get; set; } = 2048;

    }
}