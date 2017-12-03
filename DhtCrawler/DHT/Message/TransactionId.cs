using System;
using System.Linq;

namespace DhtCrawler.DHT.Message
{
    public class TransactionId
    {
        internal static readonly TransactionId FindNode = new byte[] { 0, 0 };
        internal static readonly TransactionId Ping = new byte[] { 0, 1 };
        internal static readonly TransactionId AnnouncePeer = new byte[] { 0, 2 };
        //internal static readonly TransactionId GetPeers = new byte[] { 0, 2 };
        private readonly byte[] _bytes;
        public TransactionId(byte[] bytes)
        {
            _bytes = bytes ?? throw new ArgumentException("bytes array's length must not be null");
        }

        public byte this[int index] => _bytes[index];
        public int Length => _bytes.Length;

        public override string ToString()
        {
            return string.Concat(_bytes);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TransactionId) && !(obj is byte[]))
                return false;
            var it = (TransactionId)obj;
            return _bytes.Length == it._bytes.Length && _bytes.SequenceEqual(it._bytes);
        }

        public override int GetHashCode()
        {
            return _bytes.Length >= 2 ? (_bytes[0] << 8) | _bytes[1] : 0;
        }

        public static implicit operator TransactionId(byte[] bytes)
        {
            return new TransactionId(bytes);
        }

        public static implicit operator byte[] (TransactionId id)
        {
            return id._bytes;
        }
    }
}
