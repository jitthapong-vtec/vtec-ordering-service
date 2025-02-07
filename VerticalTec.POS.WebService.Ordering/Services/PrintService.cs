﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using VerticalTec.POS.WebService.Ordering.Models;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;

namespace VerticalTec.POS.OrderingApi.Services
{
    public class PrintService
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

        public async Task<bool> PrintOrder(Transaction transaction)
        {
            using (var conn = await _db.ConnectAsync())
            {
                var myConn = conn as MySqlConnection;
                var posModule = new POSModule();
                int defaultDecimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                string saleDate = await _posRepo.GetSaleDateAsync(conn, transaction.ShopID, true);
                DataSet dsSummaryData = new DataSet();
                DataSet dsSummaryOrderData = new DataSet();
                DataSet dsOrderData = new DataSet();
                var responseText = "";

                var ePosPrint = await _posRepo.GetPropertyValueAsync(conn, 1010, "ePosPrint", transaction.ShopID);
                var mobileSummaryPrint = await _posRepo.GetPropertyValueAsync(conn, 1010, "MobileSummaryPrint", transaction.ShopID);

                var isSuccess = false;
                if (mobileSummaryPrint == "1")
                {
                    isSuccess = posModule.Summary_Print(ref responseText, ref dsSummaryData, "front", transaction.ShopID, saleDate,
                        transaction.TransactionID, transaction.ComputerID, transaction.StaffID, transaction.TerminalID, transaction.PrinterIds, myConn);

                    if (!isSuccess)
                    {
                        _log.LogError(responseText);
                    }
                }

                int batchId = 0;
                isSuccess = posModule.Table_PrintSummaryOrder(ref responseText, ref batchId, "front", transaction.PrinterIds,
                    transaction.TransactionID, transaction.ComputerID, transaction.ShopID, saleDate, transaction.StaffID,
                    transaction.TerminalID, transaction.TableID, transaction.LangID, defaultDecimalDigit, myConn);
                if (isSuccess)
                {
                    isSuccess = posModule.Table_PrintSummaryOrderData(ref responseText, ref dsSummaryOrderData, batchId, "front",
                        transaction.TransactionID, transaction.ComputerID, transaction.ShopID, saleDate, transaction.LangID, myConn);
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
                isSuccess = posModule.Table_PrintOrder(ref responseText, ref batchId, "front", transaction.TransactionID,
                    transaction.ComputerID, transaction.ShopID, saleDate, transaction.StaffID,
                    transaction.TerminalID, transaction.TableID, transaction.LangID, defaultDecimalDigit, myConn);
                if (isSuccess)
                {
                    isSuccess = posModule.Table_PrintOrderData(ref responseText, ref dsOrderData, batchId, "front",
                        transaction.TransactionID, transaction.ComputerID, transaction.ShopID, saleDate, transaction.LangID, myConn);
                    if (!isSuccess)
                    {
                        _log.LogError(string.IsNullOrEmpty(responseText) ? "An error ocurred at PrintOrderDetail" : responseText);
                    }
                }
                else
                {
                    _log.LogError(string.IsNullOrEmpty(responseText) ? "An error ocurred at PrintOrders" : responseText);
                }

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
                        PrintingObjLib.PrintLib.PrintKdsDataFromDataSet(myConn, dbUtil, posModule, transaction.ShopID,
                            transaction.ComputerID, dsSummaryData);
                        PrintingObjLib.PrintLib.PrintKdsDataFromDataSet(myConn, dbUtil, posModule, transaction.ShopID,
                            transaction.ComputerID, dsSummaryOrderData);
                        PrintingObjLib.PrintLib.PrintKdsDataFromDataSet(myConn, dbUtil, posModule, transaction.ShopID,
                            transaction.ComputerID, dsOrderData);

                        posModule.Table_UpdateStatus(ref responseText, "front", transaction.TransactionID, transaction.ComputerID,
                            transaction.ShopID, saleDate, transaction.LangID, myConn);
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
                    await Print(payload.ShopID, payload.ComputerID, payload.PrinterIds, payload.PrinterNames, dsPrintData, payload.PaperSize);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message);
                }
            }
        }

        public async Task PrintCheckBill(Transaction payload)
        {
            using (var conn = await _db.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.CheckBillAsync(conn, payload);
                    await Print(payload.ShopID, payload.ComputerID, payload.PrinterIds, payload.PrinterNames, dsPrintData, payload.PaperSize);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message);
                }
            }
        }

        Task Print(int shopId, int computerId, string printerIds, string printerNames, DataSet dsPrintData, int paperSize = 80)
        {
            try
            {
                IPAddress ip;
                var isIPFormat = IPAddress.TryParse(printerNames, out ip);
                if (isIPFormat)
                {
                    Device.Printer.Epson.EpsonResponse response = null;
                    Task.Run(async () =>
                    {
                        var size = Device.Printer.Epson.PaperSizes.Size80;
                        if (paperSize == 58)
                            size = Device.Printer.Epson.PaperSizes.Size58;
                        response = await Device.Printer.Epson.EpsonPrintManager.Instance.PrintBillDetail(
                            dsPrintData, printerIds, printerNames, size);
                    }).Wait();
                    if (response != null && response.Success == false)
                        _log.LogError($"Printer error => {response.Message}");
                }
                else
                {
                    var dbServer = ConfigurationManager.AppSettings["DBServer"];
                    var dbName = ConfigurationManager.AppSettings["DBName"];
                    var dbPort = "3308";

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
            return Task.FromResult(true);
        }
    }
}