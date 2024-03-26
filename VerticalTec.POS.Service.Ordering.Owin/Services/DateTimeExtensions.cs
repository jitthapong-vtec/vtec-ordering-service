using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VerticalTec.POS.Utils
{
    public static class DateTimeExtensions
    {
        public static object MinValueToDBNull(this DateTime dt)
        {
            return dt == DateTime.MinValue ? (object)DBNull.Value : dt;
        }

        public static string ToISODate(this DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string ToISODateTime(this DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
