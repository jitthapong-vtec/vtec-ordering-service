using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class OnlinePaymentRequest
    {
        public string PlatformApiUrl { get; set; }
        public string ReqId { get; set; }
        public string AccessToken { get; set; }
        public int ShopId { get; set; }
        public string ShopKey { get; set; }
        public string ShopCode { get; set; }
        public string ShopName { get; set; }
        public string SaleDate { get; set; }
        public int TransactionId { get; set; }
        public int ComputerId { get; set; }
        public int StaffId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public int EdcType { get; set; }
        public string PaymentGatewayType { get; set; } = string.Empty;
        public decimal PayAmount { get; set; }
    }
}
