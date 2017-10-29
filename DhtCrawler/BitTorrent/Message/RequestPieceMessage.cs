using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DhtCrawler.Encode;

namespace DhtCrawler.BitTorrent.Message
{
    public class RequestPieceMessage : Message
    {
        const string MsgTypeKey = "msg_type";
        const string PieceKey = "piece";
        const byte MsgType = 0;
        private IDictionary<string, object> Parameters;
        public int PieceID
        {
            get { return Convert.ToInt32(Parameters[PieceKey]); }
            set { Parameters[PieceKey] = value; }
        }
        public byte ExtTypeID { get; protected set; }
        public RequestPieceMessage()
        {
            Parameters = new Dictionary<string, object>();
            Parameters[MsgTypeKey] = MsgType;
        }
        public RequestPieceMessage(byte ut_metadata, int piece) : this()
        {
            PieceID = piece;
            ExtTypeID = ut_metadata;
        }

        public override uint Length
        {
            get { return (uint)(4 + 1 + 1 + BEncoder.EncodeObject(Parameters).Length); }
        }

        public override byte[] Encode()
        {
            //1.写入包长度
            //2.写入msgId
            //3.写入extTypeId
            //4.写入字典
            using (var stream = new MemoryStream())
            {
                stream.Write(BitConverter.IsLittleEndian ? BitConverter.GetBytes(Length).Reverse().ToArray() : BitConverter.GetBytes(Length), 0, 4);
                stream.WriteByte(20);
                stream.WriteByte(ExtTypeID);
                BEncoder.EncodeObject(Parameters, stream);
                return stream.ToArray();
            }
        }
    }
}
