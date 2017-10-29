namespace DhtCrawler.BitTorrent.Message
{
    public abstract class Message
    {
        public abstract uint Length { get; }

        public abstract byte[] Encode();
    }
}
