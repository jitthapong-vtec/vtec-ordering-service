using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public enum ErrorCodes
    {
        RequireParameter = -1,

        // serve 10 for order error
        NotFoundRegisteredDevice = 1,

        // 20 for 

        // 30 for printer
        PrinterError = 30,

        PaymentGatewayTimeout = 504,

        NoPaymentConfig = 8001,
        PaymentFunction = 8002,

        EDCComPort = 9000,
        EDCCreditPayment = 9001,
        EDCInquiry = 9002,
        EDCQRPayment = 9003,
        EDCCancelQR = 9004
    }
}
