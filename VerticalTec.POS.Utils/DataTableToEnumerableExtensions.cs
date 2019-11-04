using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace VerticalTec.POS.Utils
{
    public static class DataTableToEnumerableExtensions
    {
        public static IEnumerable<DataRow> AsEnumerable(this DataTable dt)
        {
            foreach(DataRow row in dt.Rows)
            {
                yield return row;
            }
        }
    }
}
