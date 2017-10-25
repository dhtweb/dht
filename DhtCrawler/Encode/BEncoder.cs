using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DhtCrawler.Encode
{
    public static class BEncoder
    {
        private static class Flags
        {

            public const byte Number = (byte)'i';
            public const byte List = (byte)'l';
            public const byte Dictionary = (byte)'d';
            public const byte End = (byte)'e';
            public const byte Split = (byte)':';
            public const byte String0 = (byte)'0';
            public const byte String1 = (byte)'1';
            public const byte String2 = (byte)'2';
            public const byte String3 = (byte)'3';
            public const byte String4 = (byte)'4';
            public const byte String5 = (byte)'5';
            public const byte String6 = (byte)'6';
            public const byte String7 = (byte)'7';
            public const byte String8 = (byte)'8';
            public const byte String9 = (byte)'9';

        }

        #region Encode

        private static void WriteBytesToStream(this Stream stream, string str)
        {
            var bytes = Encoding.ASCII.GetBytes(str);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void EncodeBytes(byte[] item, Stream stream)
        {
            stream.WriteBytesToStream(item.Length + ":");
            stream.Write(item, 0, item.Length);
        }

        private static void EncodeDictionary(IDictionary<string, object> dictionary, Stream stream)
        {
            if (dictionary == null || dictionary.Count <= 0)
                return;
            stream.WriteBytesToStream("d");
            foreach (var kv in dictionary)
            {
                EncodeString(kv.Key, stream);
                EncodeObject(kv.Value, stream);
            }
            stream.WriteBytesToStream("e");
        }

        private static void EncodeList(IList<object> list, Stream stream)
        {
            if (list == null || list.Count <= 0)
                return;
            stream.WriteBytesToStream("l");
            foreach (var item in list)
            {
                EncodeObject(item, stream);
            }
            stream.WriteBytesToStream("e");
        }

        private static void EncodeNumber(long number, Stream stream)
        {
            stream.WriteBytesToStream("i" + number + "e");

        }

        private static void EncodeString(string str, Stream stream)
        {
            var length = Encoding.ASCII.GetByteCount(str);
            stream.WriteBytesToStream(length + ":" + str);

        }

        public static void EncodeObject(object item, Stream stream)
        {
            if (item is string)
            {
                EncodeString((string)item, stream);
            }
            else if (item is long || item is int || item is short || item is ushort)
            {
                EncodeNumber(Convert.ToInt64(item), stream);
            }
            else if (item is IList<object>)
            {
                EncodeList((IList<object>)item, stream);
            }
            else if (item is IDictionary<string, object>)
            {
                EncodeDictionary((IDictionary<string, object>)item, stream);
            }
            else if (item is byte[])
            {
                EncodeBytes((byte[])item, stream);
            }
            else
                throw new ArgumentException("the type must be string,number,list or dictionary");
        }

        public static byte[] EncodeObject(object item)
        {
            using (var stream = new MemoryStream(64))
            {
                EncodeObject(item, stream);
                return stream.ToArray();
            }
        }

        #endregion



        #region 暂注

        //private static string EncodeString(byte[] bytes)
        //{
        //    return bytes.Length + ":" + string.Concat(bytes.Select(b => (char)b));
        //}
        //private static string EncodeString(string str)
        //{
        //    var length = Encoding.ASCII.GetByteCount(str);
        //    return length + ":" + str;
        //}

        //private static string EncodeNumber(long number)
        //{
        //    return "i" + number + "e";
        //}

        //private static string EncodeList(IList<object> list)
        //{
        //    if (list == null || list.Count <= 0)
        //        return string.Empty;
        //    var sb = new StringBuilder("l");
        //    foreach (var item in list)
        //    {
        //        sb.Append(EncodeObject(item));
        //    }
        //    return sb.Append("e").ToString();
        //}

        //private static string EncodeDictionary(IDictionary<string, object> dictionary)
        //{
        //    if (dictionary == null || dictionary.Count <= 0)
        //        return string.Empty;
        //    var sb = new StringBuilder("d");
        //    foreach (var kv in dictionary)
        //    {
        //        sb.Append(EncodeString(kv.Key)).Append(EncodeObject(kv.Value));
        //    }
        //    return sb.Append("e").ToString();
        //}
        #endregion

        public static object Decode(byte[] data)
        {
            var index = 0;
            return Decode(data, ref index);
        }

        private static byte[] DecodeByte(byte[] data, ref int index)
        {
            var length = new StringBuilder();
            for (; index < data.Length; index++)
            {
                if (data[index] == Flags.Split)
                    break;
                length.Append((char)data[index]);
            }
            var startIndex = index + 1;
            var strlength = int.Parse(length.ToString());
            index = startIndex + strlength;
            var strBytes = new byte[strlength];
            Array.Copy(data, startIndex, strBytes, 0, strlength);
            return strBytes;
        }

        public static object Decode(byte[] data, ref int index)
        {
            switch (data[index])
            {
                case Flags.Number:
                    index++;
                    var number = new StringBuilder();
                    for (; index < data.Length; index++)
                    {
                        if (data[index] == Flags.End)
                            break;
                        number.Append((char)data[index]);
                    }
                    index++;
                    return long.Parse(number.ToString());
                case Flags.String0:
                case Flags.String1:
                case Flags.String2:
                case Flags.String3:
                case Flags.String4:
                case Flags.String5:
                case Flags.String6:
                case Flags.String7:
                case Flags.String8:
                case Flags.String9:
                    return DecodeByte(data, ref index);
                //return Encoding.ASCII.GetString();
                case Flags.List:
                    index++;
                    var list = new List<object>();
                    while (index < data.Length)
                    {
                        list.Add(Decode(data, ref index));
                        if (data[index] == Flags.End)
                            break;
                    }
                    index++;
                    return list;
                case Flags.Dictionary:
                    index++;
                    var dic = new Dictionary<string, object>();
                    while (index < data.Length)
                    {
                        var key = Encoding.ASCII.GetString(DecodeByte(data, ref index));
                        var value = Decode(data, ref index);
                        //var value = key == "id" || key == "ip" || key == "nodes" ? DecodeByte(data, ref index) : Decode(data, ref index);
                        dic.Add(key, value);
                        if (data[index] == Flags.End)
                            break;
                    }
                    index++;
                    return dic;
                default:
                    throw new ArgumentException($"unknown type flag byte:{data[index]}char:{(char)data[index]} ,cann't decode");
            }
        }
    }
}
