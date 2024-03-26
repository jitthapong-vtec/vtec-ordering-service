using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Printer.Epson
{
    public class EpsonPrintManager
    {
        static EpsonPrintManager _instance;
        static object sync = new object();

        public static EpsonPrintManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (sync)
                    {
                        if (_instance == null)
                            _instance = new EpsonPrintManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// event for receive print status
        /// </summary>
        event EventHandler<PrintStatus> _printStatus;

        /// <summary>
        /// event for receive printer status monitor
        /// </summary>
        event EventHandler<List<PrinterInfo>> _printerStatus;

        public event EventHandler<PrintStatus> PrintStatus
        {
            add
            {
                if (_printStatus == null)
                {
                    _printStatus += value;
                }
            }
            remove
            {
                _printStatus -= value;
            }
        }

        public event EventHandler<List<PrinterInfo>> PrinterStatus
        {
            add
            {
                if (_printerStatus == null)
                {
                    _printerStatus += value;
                }
            }
            remove
            {
                _printerStatus -= value;
            }
        }

        EpsonPrintManager()
        {
            LogManager.Instance.InitLogManager("ePosPrint");
        }

        public void Init(string dbAddress, string dbName,
                         string dbPort, int shopId, string saleDate, int langId = 1)
        {
            ShopId = shopId;
            SaleDate = saleDate;
            LangId = langId;

            DbAddress = dbAddress;
            DbName = dbName;
            DbPort = dbPort;

            Task.Run(async () => await LoadLogoImageAsync());
        }

        public int ShopId { get; set; }

        public string SaleDate { get; set; } = "{ d '" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "' }";

        public int LangId { get; set; }

        public string DbAddress { get; private set; }

        public string DbName { get; private set; }

        public string DbPort { get; private set; }

        internal string LogoBase64 { get; private set; }

        internal int LogoWidth { get; private set; }

        internal int LogoHeight { get; private set; }

        public async Task<EpsonResponse> PrintBillDetail(DataSet data, string printerIds, string printerIp, PaperSizes paperSize = PaperSizes.Size80)
        {
            ReceiptPrinter printer = new ReceiptPrinter();
            printer.PrinterIds = printerIds;
            printer.PrinterIp = printerIp;
            printer.PaperSize = paperSize;
            var response = await printer.PrintBillDetailAsync(data);
            if (!response.Success)
                response = await printer.PrintBillDetailAsync(data);
            return response;
        }

        public EpsonResponse PrintKitchenOrderAsync(DataSet data)
        {
            var responseText = "";
            var printStatus = new PrintStatus();
            var kitchenPrinter = new KitchenPrinter();
            var totalTryPrintOrder = 0;
            kitchenPrinter.EpsonPrintEvent += (s, response) =>
            {
                if (++totalTryPrintOrder < 2)
                {
                    kitchenPrinter.LoadUnsuccessPrinterData();
                    if (kitchenPrinter.Printers.Count > 0)
                    {
                        kitchenPrinter.PrintAsync();
                    }
                    else
                    {
                        printStatus.Success = true;
                        _printStatus?.Invoke(this, printStatus);
                    }
                }
                else
                {
                    kitchenPrinter.LoadUnsuccessPrinterData();
                    if (kitchenPrinter.Printers.Count > 0)
                    {
                        printStatus.Success = false;
                        var printers = kitchenPrinter.Printers.GroupBy(
                            printer => printer.PrinterId, (key, g) => new { Printer = g.FirstOrDefault() }).ToList();
                        foreach (var printer in printers)
                        {
                            printStatus.Message += $"{printer.Printer.PrintStatusText}\n";
                        }
                    }
                    else
                    {
                        printStatus.Success = true;
                    }
                    _printStatus?.Invoke(this, printStatus);
                }
            };
            if (kitchenPrinter.InitKitchenPrintData(data))
            {
                kitchenPrinter.PrintAsync();
            }
            else
            {
                printStatus.Success = true;
                printStatus.Message = "There is no kitchen data";
                _printStatus?.Invoke(this, printStatus);
            }
            var epsonResponse = new EpsonResponse();
            epsonResponse.Success = true;
            return epsonResponse;
        }

        public async Task<EpsonResponse> PrintKitcheniOrderAsync(DataSet data)
        {
            var response = new EpsonResponse();
            var kitchenPrinter = new KitchenPrinter();
            if (kitchenPrinter.InitKitchenPrintData(data))
            {
                response = await kitchenPrinter.PrintAsync();
                if (!response.Success)
                {
                    kitchenPrinter.LoadUnsuccessPrinterData();
                    if (kitchenPrinter.Printers.Count > 0)
                    {
                        response = await kitchenPrinter.PrintAsync();
                        if (!response.Success)
                        {
                            var printers = kitchenPrinter.Printers.GroupBy(
                                printer => printer.PrinterId, (key, g) => new { Printer = g.FirstOrDefault() }).ToList();
                            if (printers.Count > 0)
                            {
                                response.Message = "";
                                foreach (var printer in printers)
                                {
                                    response.Message += $"{printer.Printer.PrintStatusText}\n";
                                }
                            }
                        }
                    }
                }
            }
            return response;
        }

        public void GetPrintersStatus()
        {
            var monitor = new PrinterMonitor();
            monitor.EpsonPrinterStatus += (s, printers) => {
                _printerStatus?.Invoke(this, printers);
            };
            monitor.LoadPrinterData();
        }

        internal async Task LoadLogoImageAsync()
        {
            if (string.IsNullOrEmpty(LogoBase64))
            {
                try
                {
                    var dbManager = new DatabaseManager();
                    var rootWebDir = dbManager.GetPropertyValue(1012, "RootWebDir");
                    var backoffice = dbManager.GetPropertyValue(1012, "BackOfficePath");
                    var logoUrl = $"{rootWebDir}/{backoffice}/images/front/receiptlogo.jpg";

                    var uri = new UriBuilder(logoUrl).ToString();
                    var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync(uri);
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        return;

                    var stream = await response.Content.ReadAsStreamAsync();

                    var bmp = new Bitmap(stream);
                    LogoWidth = bmp.Width;
                    LogoHeight = bmp.Height;

                    var r = bmp.ToGrayScaleImage();
                    LogoBase64 = Convert.ToBase64String(r);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
