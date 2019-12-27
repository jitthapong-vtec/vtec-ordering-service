using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Utils
{
    public static class DateTimeExtensions
    {
        public static object MinValueToDBNull(this DateTime dt)
        {
            return dt == DateTime.MinValue ? (object)DBNull.Value : dt;
        }
    }
}
