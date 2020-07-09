using System;
using System.Data;

namespace VerticalTec.POS.Utils
{
    public static class DataRowExtensions
    {
        public static T GetValue<T>(this DataRow row, string columnName, T defaultValue = default(T))
        {
            var value = defaultValue;
            try
            {
                value = (T)Convert.ChangeType(row[columnName], typeof(T));
            }
            catch (Exception)
            {
            }
            return value;
        }
    }
}
