using System;
using System.Text;

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

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Decode Error Data:{BitConverter.ToString(ErrorBytes)}").Append(Environment.NewLine);
            sb.Append($"Decode Error Index:{ErrorIndex}").Append(Environment.NewLine);
            sb.Append(base.ToString());
            return sb.ToString();
        }
    }
}
