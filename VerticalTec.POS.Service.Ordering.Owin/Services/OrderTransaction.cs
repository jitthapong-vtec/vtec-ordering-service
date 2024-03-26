using System.Collections.Generic;

namespace VerticalTec.POS
{
    public class OrderTransaction : Transaction
    {
        public List<OrderDetail> Orders { get; set; } = new List<OrderDetail>();
        public List<QuestionOption> Questions { get; set; } = new List<QuestionOption>();

        public OrderTransaction DeepCopy()
        {
            OrderTransaction orderData = (OrderTransaction)this.MemberwiseClone();
            orderData.Orders = new List<OrderDetail>();
            return orderData;
        }
    }
}
