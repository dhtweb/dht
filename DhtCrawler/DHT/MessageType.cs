using System;
using System.Collections.Generic;
using System.Text;

namespace DhtCrawler.DHT
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
