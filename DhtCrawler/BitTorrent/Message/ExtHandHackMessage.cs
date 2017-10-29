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
        private const string UtMetadataKey = "ut_metadata";
        private const string MethodKey = "m";
        private const string MetadataSizeKey = "metadata_size";
        private static readonly byte[] ExtendHeader;
        private IDictionary<string, object> _dictionary;

        static ExtHandHackMessage()
        {
            var headerAttribute = new Dictionary<string, object>(1) { { UtMetadataKey, 1 } };
            var header = new Dictionary<string, object>(1) { { MethodKey, headerAttribute } };
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

        public ExtHandHackMessage() { }
        private ExtHandHackMessage(IDictionary<string, object> dictionary)
        {
            this._dictionary = dictionary;
        }


        public bool SupportUtMetadata
        {
            get
            {
                return ((IDictionary<string, object>)_dictionary[MethodKey]).Keys.Contains(UtMetadataKey);
            }
        }
        public byte UtMetadata
        {
            get
            {
                return SupportUtMetadata ? Convert.ToByte(((IDictionary<string, object>)_dictionary[MethodKey])[UtMetadataKey]) : byte.MinValue;

            }

        }
        public long MetadataSize
        {
            get
            {
                if (!_dictionary.TryGetValue(MetadataSizeKey, out object size))
                    return -1;
                return (long)size;
            }
        }

        public static ExtHandHackMessage Decode(byte[] bytes)
        {
            var msgId = bytes[0];//读取消息Id
            var extId = bytes[1];//读取扩展Id
            var data = (IDictionary<string, object>)BEncoder.Decode(bytes.Skip(2).ToArray());
            var msg = new ExtHandHackMessage(data);
            return msg;
        }
    }
}
