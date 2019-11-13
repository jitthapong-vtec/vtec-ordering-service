using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public class PrintService : IPrintService
    {
        IDatabase _db;
        IOrderingService _orderingService;
        ILogService _log;
        VtecPOSRepo _posRepo;

        public PrintService(IDatabase database, IOrderingService orderingService, ILogService log)
        {
            _db = database;
            _orderingService = orderingService;
            _log = log;
            _posRepo = new VtecPOSRepo(database);
        }

        public async Task<bool> PrintOrder(TransactionPayload payload)
        {
            using (var conn = await _db.ConnectAsync())
            {
                var myConn = conn as MySqlConnection;
                var posModule = new POSModule();
                int defaultDecimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                string saleDate = await _posRepo.GetSaleDateAsync(conn, payload.ShopID, true);
                DataSet dsSummaryData = new DataSet();
                DataSet dsSummaryOrderData = new DataSet();
                DataSet dsOrderData = new DataSet();
                var responseText = "";

                var ePosPrint = await _posRepo.GetPropertyValueAsync(conn, 1010, "ePosPrint", payload.ShopID);
                var mobileSummaryPrint = await _posRepo.GetPropertyValueAsync(conn, 1010, "MobileSummaryPrint", payload.ShopID);

                var isSuccess = false;
                if (mobileSummaryPrint == "1")
                {
                    isSuccess = posModule.Summary_Print(ref responseText, ref dsSummaryData, "front", payload.ShopID, saleDate,
                        payload.TransactionID, payload.ComputerID, payload.StaffID, payload.TerminalID, payload.PrinterIds, myConn);

                    if (!isSuccess)
                    {
                        _log.LogError(responseText);
                    }
                }

                int batchId = 0;
                isSuccess = posModule.Table_PrintSummaryOrder(ref responseText, ref batchId, "front", payload.PrinterIds,
                    payload.TransactionID, payload.ComputerID, payload.ShopID, saleDate, payload.StaffID,
                    payload.TerminalID, payload.TableID, payload.LangID, defaultDecimalDigit, myConn);
                if (isSuccess)
                {
                    isSuccess = posModule.Table_PrintSummaryOrderData(ref responseText, ref dsSummaryOrderData, batchId, "front",
                        payload.TransactionID, payload.ComputerID, payload.ShopID, saleDate, payload.LangID, myConn);
                    if (!isSuccess)
                    {
                        _log.LogError("An error occurred when Table_PrintSummaryOrderData " + responseText);
                    }
                }
                else
                {
                    _log.LogError("An error occurred when PrintSummaryOrder " + responseText);
                }

                batchId = 0;
                isSuccess = posModule.Table_PrintOrder(ref responseText, ref batchId, "front", payload.TransactionID,
                    payload.ComputerID, payload.ShopID, saleDate, payload.StaffID,
                    payload.TerminalID, payload.TableID, payload.LangID, defaultDecimalDigit, myConn);
                if (isSuccess)
                {
                    isSuccess = posModule.Table_PrintOrderData(ref responseText, ref dsOrderData, batchId, "front",
                        payload.TransactionID, payload.ComputerID, payload.ShopID, saleDate, payload.LangID, myConn);
                    if (!isSuccess)
                    {
                        _log.LogError(string.IsNullOrEmpty(responseText) ? "An error ocurred at PrintOrderDetail" : responseText);
                    }
                }
                else
                {
                    _log.LogError(string.IsNullOrEmpty(responseText) ? "An error ocurred at PrintOrders" : responseText);
                }

                posModule.Table_UpdateStatus(ref responseText, "front", payload.TransactionID, payload.ComputerID,
                    payload.ShopID, saleDate, payload.LangID, myConn);

                if (ePosPrint == "1")
                {
                    var summaryResponse = await Device.Printer.Epson.EpsonPrintManager.Instance.PrintKitcheniOrderAsync(dsSummaryData);
                    var orderSummaryResponse = await Device.Printer.Epson.EpsonPrintManager.Instance.PrintKitcheniOrderAsync(dsSummaryOrderData);
                    var orderResponse = await Device.Printer.Epson.EpsonPrintManager.Instance.PrintKitcheniOrderAsync(dsOrderData);
                    if (summaryResponse?.Success == false ||
                        orderSummaryResponse?.Success == false ||
                        orderResponse?.Success == false)
                    {
                        _log.LogError($"{summaryResponse?.Message}{orderSummaryResponse?.Message}{orderResponse?.Message}");
                    }
                }
                else
                {
                    try
                    {
                        CDBUtil dbUtil = new CDBUtil();
                        PrintingObjLib.PrintLib.PrintKdsDataFromDataSet(myConn, dbUtil, posModule, payload.ShopID,
                            payload.ComputerID, dsSummaryData);
                        PrintingObjLib.PrintLib.PrintKdsDataFromDataSet(myConn, dbUtil, posModule, payload.ShopID,
                            payload.ComputerID, dsSummaryOrderData);
                        PrintingObjLib.PrintLib.PrintKdsDataFromDataSet(myConn, dbUtil, posModule, payload.ShopID,
                            payload.ComputerID, dsOrderData);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex.Message);
                    }
                }
            }
            return true;
        }

        public async Task PrintBill(PrintData payload)
        {
            using (var conn = await _db.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.GetBillDetail(conn, payload.TransactionID, payload.ComputerID, payload.ShopID, payload.LangID);
                    await PrintAsync(payload.ShopID, payload.ComputerID, payload.PrinterIds, payload.PrinterNames, dsPrintData, payload.PaperSize);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message);
                }
            }
        }

        public async Task PrintCheckBill(TransactionPayload payload)
        {
            using (var conn = await _db.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.CheckBillAsync(conn, payload.TransactionID, payload.ComputerID,
                        payload.ShopID, payload.TerminalID, payload.StaffID, payload.LangID);
                    await PrintAsync(payload.ShopID, payload.ComputerID, payload.PrinterIds, payload.PrinterNames, dsPrintData, 80);

                    await _orderingService.UpdateTableStatusAsync(conn, payload.TransactionID, payload.ComputerID, payload.ShopID, payload.LangID);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message);
                }
            }
        }

        public async Task KioskPrintCheckBill(TransactionPayload payload)
        {
            using (var conn = await _db.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.CheckBillAsync(conn, payload.TransactionID, payload.ComputerID,
                        payload.ShopID, payload.TerminalID, payload.StaffID, payload.LangID, true);
                    await PrintAsync(payload.ShopID, payload.ComputerID, payload.PrinterIds, payload.PrinterNames, dsPrintData, 80);

                    await _orderingService.UpdateTableStatusAsync(conn, payload.TransactionID, payload.ComputerID, payload.ShopID, payload.LangID);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message);
                }
            }
        }

        public async Task PrintAsync(int shopId, int computerId, DataSet dsPrintData)
        {
            if (dsPrintData.Tables.Count > 0)
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var posModule = new POSModule();
                    CDBUtil dbUtil = new CDBUtil();
                    PrintingObjLib.PrintLib.PrintKdsDataFromDataSet(conn as MySqlConnection, dbUtil, posModule,
                        shopId, computerId, dsPrintData);
                }
            }
        }

        public async Task PrintAsync(int shopId, int computerId, string printerIds, string printerNames, DataSet dsPrintData, int paperSize = 80)
        {
            try
            {
                IPAddress ip;
                var isIPFormat = IPAddress.TryParse(printerNames, out ip);
                if (isIPFormat)
                {
                    Device.Printer.Epson.EpsonResponse response = null;
                    var size = Device.Printer.Epson.PaperSizes.Size80;
                    if (paperSize == 58)
                        size = Device.Printer.Epson.PaperSizes.Size58;
                    response = await Device.Printer.Epson.EpsonPrintManager.Instance.PrintBillDetail(
                        dsPrintData, printerIds, printerNames, size);
                    if (response != null && response.Success == false)
                        _log.LogError($"Printer error => {response.Message}");
                }
                else
                {
                    var dbServer = AppConfig.Instance.DbServer;
                    var dbName = AppConfig.Instance.DbName;
                    var dbPort = AppConfig.Instance.DbPort;

                    var posModule = new POSModule();
                    if (!string.IsNullOrEmpty(printerNames))
                    {
                        PrintingObjLib.PrintLib.PrintDataFromDataSet(posModule, shopId,
                            computerId, dsPrintData, printerNames, dbServer, dbName, "3308");
                    }
                    else
                    {
                        PrintingObjLib.PrintLib.PrintDataFromDataSet(posModule, shopId,
                            computerId, dsPrintData, dbServer, dbName, dbPort);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"Printer error => {ex.Message}");
            }
        }
    }
}