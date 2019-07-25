using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Report.Dashboard.Models
{
    public class HashUtil
    {
        public static string SHA1(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);

            var sha1 = new SHA1CryptoServiceProvider();
            byte[] hashBytes = sha1.ComputeHash(bytes);

            return HexStringFromBytes(hashBytes);
        }

        static string HexStringFromBytes(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                var hex = b.ToString("x2");
                sb.Append(hex);
            }
            return sb.ToString().ToUpper();
        }
    }
}
