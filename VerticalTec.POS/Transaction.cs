using System.Collections.Generic;

namespace VerticalTec.POS.Core
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.New;
        public int ComputerId { get; set; }
        public int ShopId { get; set; }
        public int StaffId { get; set; }
        public int SaleModeId { get; set; } = 1;
        public string Name { get; set; } = "";
        public string QueueName { get; set; }
        public int NoCustomer { get; set; }
        public int TableId { get; set; }
    }
}
