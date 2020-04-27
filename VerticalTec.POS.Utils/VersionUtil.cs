using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Utils
{
    public class VersionUtil
    {
        public static bool CompareVersion(string from, string to)
        {
            return from.Equals(to);
        }
    }
}
