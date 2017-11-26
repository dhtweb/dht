using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DhtCrawler.Common.Utils
{
    public static class NetUtils
    {
        public static int ToInt32(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 4)
                throw new ArgumentException();
            return ((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        public static long ToInt64(byte[] array)
        {
            long result = 0;
            for (int i = array.Length - 1; i >= 0; i--)
            {
                long ipItem = array[array.Length - 1 - i];
                result |= ipItem << (8 * i);
            }
            return result;
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
            long ipNum = endPoint.Address.ToInt64();
            return ipNum << 2 | (long)endPoint.Port;
        }

        public static long ToInt64(this IPAddress address)
        {
            var array = address.GetAddressBytes();
            return ToInt64(array);
        }

        private static readonly List<Tuple<long, long>> ReserveIpRange;

        static NetUtils()
        {
            var ipInfos = new[]
            {
                "0.0.0.0/8",
                "10.0.0.0/8",
                "100.64.0.0/10",
                "127.0.0.0/8",
                "169.254.0.0/16",
                "172.16.0.0/12",
                "192.0.0.0/24",
                "192.0.0.0/29",
                "192.0.0.8/32",
                "192.0.0.9/32",
                "192.0.0.10/32",
                "192.0.0.170/32",
                "192.0.0.171/32",
                "192.0.2.0/24",
                "192.31.196.0/24",
                "192.52.193.0/24",
                "192.88.99.0/24",
                "192.168.0.0/16",
                "192.175.48.0/24",
                "198.18.0.0/15",
                "198.51.100.0/24",
                "203.0.113.0/24",
                "224.0.0.0/4",
                "240.0.0.0/4",
                "255.255.255.255/32"
            };
            ReserveIpRange = new List<Tuple<long, long>>(ipInfos.Length);
            foreach (var ipInfo in ipInfos)
            {
                var info = ComputeIpInfo(ipInfo);
                ReserveIpRange.Add(info);
            }
        }

        private static Tuple<long, long> ComputeIpInfo(string ipInfo)
        {
            var arrays = ipInfo.Split('/');
            byte[] ipArray = arrays[0].Split('.').Select(byte.Parse).ToArray();
            var netlength = int.Parse(arrays[1]);
            byte[] maskArray = { 0, 0, 0, 0 }, startArray = { 0, 0, 0, 0 }, endArray = { 255, 255, 255, 255 };

            for (int i = 0, j = netlength; i < maskArray.Length && j > 0; i++, j -= 8)
            {
                if (j > 8)
                {
                    maskArray[i] = 255;
                }
                else
                {
                    maskArray[i] = (byte)(255 << (8 - j));
                }
            }
            for (var i = 0; i < ipArray.Length && netlength > 0; i++, netlength -= 8)
            {
                if (netlength >= 8)
                {
                    startArray[i] = endArray[i] = ipArray[i];
                }
                else
                {
                    startArray[i] = (byte)(ipArray[i] & maskArray[i]);
                    endArray[i] = (byte)(ipArray[i] | (~ipArray[i] & 255));
                }
            }
            return new Tuple<long, long>(ToInt64(startArray), ToInt64(endArray));
        }

        public static bool IsPublic(this IPAddress address)
        {
            var ipNumber = address.ToInt64();
            return ReserveIpRange.All(range => range.Item1 > ipNumber || range.Item2 < ipNumber);
        }
    }
}
