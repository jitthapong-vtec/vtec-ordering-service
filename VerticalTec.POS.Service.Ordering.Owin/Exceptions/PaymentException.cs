using System;
using VerticalTec.POS.Service.Ordering.Owin.Models;

namespace VerticalTec.POS.Service.Ordering.Owin.Exceptions
{
    public class PaymentException : Exception
    {
        public ErrorCodes ErrorCode { get; set; }

        public PaymentException(ErrorCodes errCode, string message) : base(message)
        {
            ErrorCode = errCode;
        }
    }
}
