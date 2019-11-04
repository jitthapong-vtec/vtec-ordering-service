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
        Task ModifyOrderAsync(IDbConnection conn, OrderDetail orderDetail);
        Task<List<OrderDetail>> DeleteOrdersAsync(IDbConnection conn, List<OrderDetail> orders);
        Task DeleteChildComboAsync(IDbConnection conn, int transactionId, int computerId, int orderDetailId);
        Task AddOrderAsync(IDbConnection conn, OrderTransaction order);
        Task<DataSet> MoveOrderAsync(IDbConnection conn, TableManage tableManage);
        Task<DataSet> MoveTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, string saleDate, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText);
        Task<DataSet> MergeTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, string saleDate, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText);
        Task<DataSet> SplitTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, string saleDate, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText);
        Task<DataTable> GetModifierOrderAsync(IDbConnection conn, int shopId, int transactionId, int computerId, int parentOrderDetailId, string productCode = "", SaleModes saleMode = SaleModes.DineIn);
        Task<List<OrderDetail>> GetOrderDetailsAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int staffId = 2, int langId = 1);
        Task<object> GetOrderDataAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int langId = 1);
        Task<DataTable> GetChildOrderAsync(IDbConnection conn, int transactionId, int computerId, int parentOrderId);
        Task<string> GetBillHtmlAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int langId = 1);
        Task<DataSet> GetBillDetail(IDbConnection conn, int transactionId, int computerId, int shopId, int langId);
        Task<DataSet> CheckBillAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int terminalId, int staffId, int langId);
        Task<bool> SubmitSaleModeOrderAsync(IDbConnection conn, Transaction transaction);
        Task<bool> CancelTransactionAsync(IDbConnection conn, int transactionId, int computerId);
        Task<bool> SubmitOrderAsync(IDbConnection conn, Transaction transaction);
    }
}
