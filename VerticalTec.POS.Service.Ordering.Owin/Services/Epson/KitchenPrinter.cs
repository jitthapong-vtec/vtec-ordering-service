using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Printer.Epson
{
    public class KitchenPrinter
    {
        internal event EventHandler<EpsonResponse> EpsonPrintEvent;

        DatabaseManager _dbManager;
        int _totalBatch = -1;
        int _batchIdx = -1;

        public KitchenPrinter()
        {
            _dbManager = new DatabaseManager();
            Printers = new List<Printer>();
        }

        public List<Printer> Printers { get; private set; }

        public bool InitKitchenPrintData(DataSet data)
        {
            var dtKitchenData = data.Tables["KitchenData"];
            var dtKitchenPrintData = data.Tables["KitchenPrintData"];
            if (dtKitchenData == null || dtKitchenPrintData == null)
            {
                LogManager.Instance.WriteLog("Not found kitchen data");
                return false;
            }

            var dtPrinters = _dbManager.GetPrinter();
            
            var dtKitchenHeaderClone = dtKitchenData.Clone();
            dtKitchenHeaderClone.Columns.Add("PrinterIp", typeof(string));
            dtKitchenHeaderClone.Columns.Add("IsOPOSPrinter", typeof(int));

            var kitchenWithPrinter = (from header in dtKitchenData.AsEnumerable()
                                      join printer in dtPrinters.AsEnumerable()
                                      on header.GetValue<int>("PrinterID") equals printer.GetValue<int>("PrinterID") into printerJoin
                                      from pj in printerJoin.DefaultIfEmpty()
                                      let pjArr = new object[]
                                      {
                                          pj != null ? pj["PrinterDeviceBackup"] : null,
                                          pj != null ? pj["IsOPOSPrinter"] : null,
                                      }
                                      select header.ItemArray.Concat(pjArr).ToArray()).ToList();
            foreach (object[] rows in kitchenWithPrinter)
            {
                dtKitchenHeaderClone.Rows.Add(rows);
            }

            var kitchenDatas = (from header in dtKitchenHeaderClone.AsEnumerable()
                                join detail in dtKitchenPrintData.AsEnumerable()
                                on header.GetValue<string>("BatchUUID") equals detail.GetValue<string>("BatchUUID") into headerJoin
                                select new { Header = header, Detail = headerJoin.CopyToDataTable() }).ToList();

            foreach (var kitchenData in kitchenDatas)
            {
                if (string.IsNullOrEmpty(kitchenData.Header.GetValue<string>("PrinterIp")))
                    continue;

                var printer = new Printer();
                printer.KitchenData = kitchenData.Header;
                printer.PrintData = kitchenData.Detail;
                printer.PaperSize = kitchenData.Header.GetValue<int>("IsOPOSPrinter") == 1 ? PaperSizes.Size58 : PaperSizes.Size80;
                Printers.Add(printer);
            }

            _totalBatch = Printers.Count() - 1;
            _batchIdx = 0;
            return Printers.Count > 0;
        }

        public void LoadUnsuccessPrinterData()
        {
            var unSuccessPrinter = (from printer in Printers
                                    where printer.PrintStatus != 2
                                    select printer).ToList();
            if (unSuccessPrinter.Count > 0)
            {
                Printers = unSuccessPrinter;
                _totalBatch = Printers.Count() - 1;
                _batchIdx = 0;
            }
            else
            {
                Printers.Clear();
            }
        }

        public async Task<EpsonResponse> PrintAsync()
        {
            var printer = Printers[_batchIdx];
            var printerIp = printer.Redirect ? printer.PrinterBackupIp : printer.PrinterIp;

            var response = await printer.CheckPrinterAsync(printerIp);
            if(response.Success)
                response = await printer.PrintAsync(printerIp);

            if (response.Success)
            {
                printer.PrintStatus = 2;
            }
            else
            {
                printer.PrintStatus = 4;
                if (!printer.Redirect && !string.IsNullOrEmpty(printer.PrinterBackupIp))
                {
                    printer.Redirect = true;
                    printer.PrintStatusText = $"{printer.PrinterName}: {response.Message}";
                }
                else
                {
                    printer.PrintStatusText = $"{printer.PrinterName}: {response.Message}";
                }
            }

            UpdatePrintStatus(printer);

            if (_batchIdx < _totalBatch)
            {
                _batchIdx++;
                await PrintAsync();
            }
            else
            {
                RaisePrintEvent(response);
            }
            return response;
        }

        void RaisePrintEvent(EpsonResponse response)
        {
            EpsonPrintEvent?.Invoke(this, response);
        }

        void UpdatePrintStatus(Printer printer)
        {
            var kitchenData = printer.KitchenData;
            var batchUuid = kitchenData.GetValue<string>("BatchUUID");
            var transactionId = kitchenData.GetValue<int>("TransactionID");
            var computerId = kitchenData.GetValue<int>("ComputerID");
            var printStatus = printer.PrintStatus;
            var errMessage = printStatus != 2 ? printer.PrintStatusText : "";

            _dbManager.UpdateOrderPrintStatus(batchUuid, printStatus, errMessage, transactionId, computerId);
        }
    }
}
