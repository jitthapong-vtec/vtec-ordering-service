using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Core
{
    public class Payment
    {
        public int PaymentId { get; set; }
        public int TransactionId { get; set; }
        public int ComputerId { get; set; }
        public int ShopId { get; set; }
        public int PayTypeId { get; set; }
        public decimal PayAmount { get; set; }
        public int CreditCardType { get; set; }
    }
}
