namespace DhtCrawler.DHT.Message
{
    public enum CommandType
    {
        UnKnow,
        /// <summary>
        /// 发送PING，判断节点是否存活
        /// </summary>
        Ping,
        /// <summary>
        /// 查找目标节点，返回目标节点或是最近节点
        /// </summary>
        Find_Node,
        /// <summary>
        /// 查找种子相关的节点
        /// </summary>        
        Get_Peers,
        /// <summary>
        /// 申请下载
        /// </summary>
        Announce_Peer
    }
}
