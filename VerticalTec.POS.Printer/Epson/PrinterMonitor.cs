using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace VerticalTec.POS.Printer.Epson
{
    public class PrinterMonitor
    {
        internal event EventHandler<List<PrinterInfo>> EpsonPrinterStatus;

        DatabaseManager _dbManager;
        EposWebClient _eposClient;
        int _totalPrinter;
        int _printerIdx = -1;

        public PrinterMonitor()
        {
            _dbManager = new DatabaseManager();
            _eposClient = new EposWebClient(3);
        }

        internal List<PrinterInfo> Printers { get; } = new List<PrinterInfo>();

        internal async Task LoadPrinterData()
        {
            var allPrinter = _dbManager.GetPrinter();
            var kitchenPrinter = (from printer in allPrinter.AsEnumerable()
                                  let printerType = printer.GetValue<int>("PrinterTypeID")
                                  where (printerType == 1 || printerType == 2) && printer.GetValue<int>("PrinterID") > 1
                                  group printer by printer.GetValue<string>("PrinterDeviceBackup") into g
                                  select new
                                  {
                                      PrinterIp = g.Key,
                                      Printers = g.ToList()
                                  });

            foreach (var printer in kitchenPrinter)
            {
                var printerIp = "";
                try
                {
                    printerIp = printer.PrinterIp.Split(',')[0];
                }
                catch (Exception) { }
                
                var printerName = "";
                for (int i = 0; i < printer.Printers.Count; i++)
                {
                    printerName += printer.Printers[i].GetValue<string>("PrinterName");
                    if (i < printer.Printers.Count - 1)
                        printerName += ",";
                }
                Printers.Add(new PrinterInfo()
                {
                    PrinterName = printerName,
                    PrinterIp = printerIp
                });
            }
            _totalPrinter = Printers.Count;
            if (_totalPrinter > 0)
            {
                _printerIdx = 0;
                await SetPrintersStatus();
            }

            EpsonPrinterStatus?.Invoke(this, Printers);
        }

        async Task<bool> SetPrintersStatus()
        {
            var printer = Printers[_printerIdx++];
            IPAddress ip;
            if (IPAddress.TryParse(printer.PrinterIp, out ip))
            {
                _eposClient.EposDeviceName = printer.PrinterName;
                var uri = $"http://{printer.PrinterIp}/cgi-bin/epos/service.cgi?devid=local_printer&timeout=10000";
                var cmd = new PrinterCommand("local_printer");
                var epsonResponse = await _eposClient.SendRequest(new UriBuilder(uri).ToString(), true, cmd.Command);
                if (epsonResponse.Success)
                {
                    printer.Online = true;
                    printer.StatusText = "";
                }
                else
                {
                    if (epsonResponse.Code == "ConnectionError")
                    {
                        printer.Online = false;
                    }
                    else
                    {
                        printer.Online = true;
                        epsonResponse.Message = "";
                    }
                    printer.StatusText = epsonResponse.Message;
                }
            }
            else
            {
                printer.StatusText = $"Invalid printer ipaddress format of {printer.PrinterName}";
            }
            if (_printerIdx < _totalPrinter)
                await SetPrintersStatus();
            return true;
        }
    }
}
