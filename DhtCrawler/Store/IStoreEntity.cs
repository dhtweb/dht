namespace DhtCrawler.Store
{
    public interface IStoreEntity
    {
        byte[] ToBytes();

        void ReadBytes(byte[] data);
    }
}
