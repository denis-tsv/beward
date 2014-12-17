using System;
using System.Linq;
using System.Net;

namespace TestTask.IPChecker
{
    public class IpConverter
    {
        public static long IPAddressToLong(IPAddress address)
        {
            return BitConverter.ToInt32(address.GetAddressBytes().Reverse().ToArray(), 0);
        }

        public static string LongToString(long ip)
        {
            return IPAddress.Parse(ip.ToString()).ToString();
        }
    }
}
