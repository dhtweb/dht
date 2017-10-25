using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DhtCrawler.Encode;

namespace DhtCrawler.BitTorrent.Message
{
    public class ExtHandHackMessage : Message
    {
        private const byte BtMsgId = 20;
        private const byte ExtHandShakeId = 0;
        private static readonly byte[] ExtendHeader;

        static ExtHandHackMessage()
        {
            var headerAttribute = new Dictionary<string, object>(1) { { "ut_metadata", 1 } };
            var header = new Dictionary<string, object>(1) { { "m", headerAttribute } };
            ExtendHeader = BEncoder.EncodeObject(header);
        }

        public override uint Length => (uint)ExtendHeader.Length + 1 + 1;

        public override byte[] Encode()
        {
            using (var stream = new MemoryStream((int)Length))
            {
                stream.Write(BitConverter.IsLittleEndian ? BitConverter.GetBytes(Length).Reverse().ToArray() : BitConverter.GetBytes(Length), 0, 4);
                stream.WriteByte(BtMsgId);
                stream.WriteByte(ExtHandShakeId);
                stream.Write(ExtendHeader, 0, ExtendHeader.Length);
                return stream.ToArray();
            }
        }

        public void Decode(byte[] bytes)
        {
            var msgId = bytes[0];//读取消息Id
            var extId = bytes[1];//读取扩展Id
            var data = BEncoder.Decode(bytes.Skip(2).ToArray());
        }
    }
}
