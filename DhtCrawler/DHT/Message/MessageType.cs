namespace DhtCrawler.DHT.Message
{
    public enum MessageType
    {
        UnKnow,
        /// <summary>
        /// 请求消息
        /// </summary>
        Request,
        /// <summary>
        /// 响应消息
        /// </summary>
        Response,
        /// <summary>
        /// 错误
        /// </summary>        
        Exception
    }
}
