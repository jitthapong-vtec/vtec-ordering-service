using System;

namespace VerticalTec.POS.Utils
{
    public static class CurrencyCalculateExtensions
    {
        public static decimal CurrencyAmount(this decimal value, double rate, double ratio)
        {
            return value * Convert.ToDecimal(rate * ratio);
        }
    }
}
