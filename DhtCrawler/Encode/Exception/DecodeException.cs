namespace DhtCrawler.Encode.Exception
{
    public class DecodeException : System.Exception
    {
        public byte[] ErrorBytes { get; }
        public int ErrorIndex { get; }

        public DecodeException(byte[] errorBytes, int errorIndex, string msg, System.Exception innerException) : base(msg, innerException)
        {
            ErrorBytes = errorBytes;
            ErrorIndex = errorIndex;
        }

        public DecodeException(byte[] errorBytes, int errorIndex, string msg) : this(errorBytes, errorIndex, msg, null)
        {

        }
    }
}
