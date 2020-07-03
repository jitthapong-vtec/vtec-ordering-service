using System;
using System.ComponentModel;
using System.Data;

namespace VerticalTec.POS.Database
{
    public static class DataReaderExtensions
    {
        private static bool IsNullableType(Type theValueType)
        {
            return (theValueType.IsGenericType && theValueType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
        }

        public static T GetValue<T>(this IDataReader theReader, string theColumnName)
        {
            object theValue = theReader[theColumnName];

            Type theValueType = typeof(T);

            if (DBNull.Value != theValue)
            {
                if (!IsNullableType(theValueType))
                {
                    return (T)Convert.ChangeType(theValue, theValueType);
                }
                else
                {
                    NullableConverter theNullableConverter = new NullableConverter(theValueType);

                    return (T)Convert.ChangeType(theValue, theNullableConverter.UnderlyingType);
                }
            }

            return default(T);
        }
    }
}
