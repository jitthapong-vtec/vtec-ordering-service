using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace VerticalTec.POS.Core
{
    public interface IPOS
    {
        Task AddOrderAsync(IDbConnection conn, IEnumerable<Order> orders);

        Task AddPaymentAsync(IDbConnection conn, Payment payment);

        Task CalculateBillAsync(IDbConnection conn, int shopId, int transactionId, int computerId);

        Task DeletePaymentAsync(IDbConnection conn, int paymentId, int transactionId, int computerId);

        Task FinalizeAsync(IDbConnection con, int shopId, int transactionId, int computerId, int staffId, int terminalId);

        Task<DataTable> GetOrderDetailAsync(IDbConnection conn, int transactionId, int computerId, int orderDetailId, int langId);

        Task<DataTable> GetProgramProperty(IDbConnection conn, int propertyId, int shopId);

        Task<DateTime> GetSessionDate(int shopId);

        Task OpenTransactionAsync(IDbConnection conn, Transaction transaction);

        Task RefreshPromoAsync(IDbConnection conn, int transactionId, int computerId);
    }
}
