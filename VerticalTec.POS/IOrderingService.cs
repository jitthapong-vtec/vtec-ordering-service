using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace VerticalTec.POS
{
    public interface IOrderingService
    {
        Task OpenTransactionAsync(IDbConnection conn, OrderTransaction tranData);
        Task OpenTransactionProcessAsync(IDbConnection conn, OrderTransaction tranData);
        Task ModifyOrderAsync(IDbConnection conn, OrderDetail orderDetail);
        Task<List<OrderDetail>> DeleteOrdersAsync(IDbConnection conn, List<OrderDetail> orders);
        Task DeleteChildComboAsync(IDbConnection conn, int transactionId, int computerId, int orderDetailId);
        Task AddOrderAsync(IDbConnection conn, OrderTransaction order);
        Task<DataSet> MoveOrderAsync(IDbConnection conn, TableManage tableManage);
        Task<DataSet> MoveTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText);
        Task<DataSet> MergeTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText);
        Task<DataSet> SplitTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText);
        Task UpdateTableStatusAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int langId = 1);
        Task<DataTable> GetModifierOrderAsync(IDbConnection conn, int shopId, int transactionId, int computerId, int parentOrderDetailId, string productCode = "", int langId=1, SaleModes saleMode = SaleModes.DineIn);
        Task<List<OrderDetail>> GetOrderDetailsAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int staffId = 2, int langId = 1);
        Task<object> GetOrderDataAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int langId = 1);
        Task<DataTable> GetChildOrderAsync(IDbConnection conn, int transactionId, int computerId, int parentOrderId);
        Task<string> GetBillHtmlAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int langId = 1);
        Task<DataSet> GetBillDetail(IDbConnection conn, int transactionId, int computerId, int shopId, int langId);
        Task<DataSet> CheckBillAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int terminalId, int staffId, int langId, bool bypassChkUnsubmit = false);
        Task<bool> SubmitSaleModeOrderAsync(IDbConnection conn, int transactionId, int computerId, string transactionName, int totalCustomer, TransactionStatus status);
        Task<bool> CancelTransactionAsync(IDbConnection conn, int transactionId, int computerId);
        Task<bool> SubmitOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int tableId);
        Task<string> GetOrRegenPincodeAsync(IDbConnection conn, string tranKey, int shopId, int tableId, int mode = 1, string saleDate = "");
        Task<string> UpdateBuffetAsync(IDbConnection conn, string tranKey, int shopId, int tableId);
    }
}
