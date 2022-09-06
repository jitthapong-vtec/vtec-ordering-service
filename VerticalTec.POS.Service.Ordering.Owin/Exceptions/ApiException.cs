using System;
using VerticalTec.POS.Service.Ordering.Owin.Models;

namespace VerticalTec.POS.Service.Ordering.Owin.Exceptions
{
    public class ApiException : Exception
    {
        public ErrorCodes ErrorCode { get; set; }

        public ApiException(ErrorCodes errCode, string message) : base(message)
        {
            ErrorCode = errCode;
        }
    }
}
