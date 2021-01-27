using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Extensions
{
    public static class HashExtensions
    {
        public static string ToSha1(this string val)
        {
            using (var sha256 = SHA1.Create())
            {
                var varhashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(val));
                return BitConverter.ToString(varhashedBytes).Replace("-", "").ToUpper();
            }
        }
    }
}
