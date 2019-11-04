using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class GrcPaymentData
    {
        public Data data { get; set; }
        public Store store { get; set; }
        public OvoAccount account { get; set; }
        public string response_code_id { get; set; }
        public string response_code { get; set; }
        public string response_message { get; set; }
        public string response_description { get; set; }
        public string response_code_fintech { get; set; }
        public string response_status_fintech { get; set; }

        public class Data
        {
            public string transaction_id { get; set; }
            public string order_id { get; set; }
            public string transaction_status { get; set; }
            public string qrcode { get; set; }
            public string qr_title { get; set; }
        }

        public class Store
        {
            public string address1 { get; set; }
            public string address2 { get; set; }
            public string store_name { get; set; }
            public string store_code { get; set; }
        }

        public class OvoAccount
        {
            public string ovo_id { get; set; }
            public string ovo_points_earned { get; set; }
            public string cash_balance { get; set; }
            public string full_name { get; set; }
            public string ovo_points_used { get; set; }
            public string points_balance { get; set; }
        }
    }
}