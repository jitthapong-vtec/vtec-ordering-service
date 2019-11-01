using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS
{
    public interface IPaymentService
    {
        Task AddPaymentAsync(IDbConnection conn, PaymentData payment);
        Task DeletePaymentAsync(IDbConnection conn, int transactionId, int computerId, int payDetailId);
        Task FinalizeBillAsync(IDbConnection conn, int transactionId, int computerId, int terminalId, int shopId, int staffId, int langId, string printerIds, string printerNames);
        Task<DataTable> GetPaymentDetailAsync(IDbConnection conn, int transactionId, int computerId);
        Task<DataSet> GetPaymentDataAsync(IDbConnection conn, int computerId, int langId, int currencyId);
        Task<DataTable> GetPaymentCurrencyAsync(IDbConnection conn);
        Task<DataTable> GetPendingPaymentAsync(IDbConnection conn, int transactionId, int computerId, int payTypeId);
    }
}
