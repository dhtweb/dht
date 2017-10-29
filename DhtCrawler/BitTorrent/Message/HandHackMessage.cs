using System;

namespace DhtCrawler.BitTorrent.Message
{
    public class HandHackMessage : Message
    {
        //BT_PROTOCOL = 
        const string Protocol = "BitTorrent protocol";
        private static readonly byte[] ReservedBytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00 };

        public byte[] InfoHash { get; private set; }
        private byte[] peerId;

        public override uint Length
        {
            get
            {
                return (uint)(1 + Protocol.Length + ReservedBytes.Length + 20 + 20);
            }
        }

        private HandHackMessage() { }
        public HandHackMessage(byte[] infoHash, byte[] peerId)
        {
            this.InfoHash = infoHash;
            this.peerId = peerId;
        }

        public override byte[] Encode()
        {
            var offset = 0;
            var buffer = new byte[this.Length];
            buffer[offset++] = (byte)Protocol.Length;
            for (var i = 0; i < Protocol.Length; i++)
            {
                buffer[offset++] = (byte)Protocol[i];
            }
            foreach (var array in new[] { ReservedBytes, InfoHash, peerId })
            {
                for (var i = 0; i < array.Length; i++)
                {
                    buffer[offset++] = array[i];
                }
            }
            return buffer;
        }

        public static HandHackMessage Decode(byte[] buffer)
        {
            var message = new HandHackMessage
            {
                InfoHash = new byte[20],
                peerId = new byte[20]
            };
            Array.Copy(buffer, Protocol.Length + ReservedBytes.Length, message.InfoHash, 0, 20);
            return message;
        }
    }
}
