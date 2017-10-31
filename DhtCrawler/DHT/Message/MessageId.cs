using System;
using System.Linq;

namespace DhtCrawler.DHT.Message
{
    public class MessageId
    {
        private readonly byte[] _bytes;
        public MessageId(byte[] bytes)
        {
            _bytes = bytes ?? throw new ArgumentException("bytes array's length must not be null");
        }

        public override string ToString()
        {
            return string.Concat(_bytes);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MessageId) && !(obj is byte[]))
                return false;
            var it = (MessageId)obj;
            if (_bytes.Length != it._bytes.Length)
            {
                return false;
            }
            return !_bytes.Where((t, i) => t != it._bytes[i]).Any();
        }

        public override int GetHashCode()
        {
            return (_bytes[0] << 8) | _bytes[1];
        }

        public static implicit operator MessageId(byte[] bytes)
        {
            return new MessageId(bytes);
        }

        public static implicit operator byte[] (MessageId id)
        {
            return id._bytes;
        }
    }
}
