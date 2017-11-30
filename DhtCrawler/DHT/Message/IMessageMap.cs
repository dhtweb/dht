namespace DhtCrawler.DHT.Message
{
    public interface IMessageMap
    {
        bool RegisterMessage(DhtMessage message, DhtNode node);

        bool RequireRegisteredInfo(DhtMessage message, DhtNode node);
    }
}
