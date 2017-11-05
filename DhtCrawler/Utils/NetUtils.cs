using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DhtCrawler.Utils
{
    public static class NetUtils
    {
        public static int ToInt32(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 4)
                throw new ArgumentException();
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        public static byte[] GetBytes(int num)
        {
            var bytes = new byte[4];
            for (int i = 0, j = bytes.Length - 1; i < bytes.Length; i++, j--)
            {
                bytes[i] = (byte)((num >> (8 * j)) & 0xFF);
            }
            return bytes;
        }

        public static long ToInt64(this IPEndPoint endPoint)
        {
            var bytes = endPoint.Address.GetAddressBytes();
            long ipNum = ToInt32(bytes);
            return ipNum << 2 | endPoint.Port;
        }
    }
}
