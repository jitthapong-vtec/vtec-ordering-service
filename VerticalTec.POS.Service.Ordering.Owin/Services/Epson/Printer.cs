using System;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using VerticalTec.Device.Printer.Epson;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Printer.Epson
{
    public class Printer
    {
        EposWebClient _eposClient;

        int MaxChar = 55;
        int Col3MaxChar = 14;
        int Col2MaxChar = 6;
        int Col1MaxChar = 35;

        public Printer(int timeout = 5)
        {
            _eposClient = new EposWebClient(timeout);
        }

        internal PaperSizes PaperSize
        {
            set
            {
                if (value == PaperSizes.Size58)
                {
                    MaxChar = 42;
                    Col3MaxChar = 12;
                    Col2MaxChar = 4;
                    Col1MaxChar = MaxChar - (Col3MaxChar + Col2MaxChar);
                }
            }
        }

        internal DataRow KitchenData { get; set; }

        internal DataTable PrintData { get; set; }

        internal int PrinterId { get => KitchenData.GetValue<int>("PrinterID"); }

        internal string PrintJobId { get; set; }

        internal bool AutoCut { get; set; } = true;

        internal bool Redirect { get; set; }

        internal int PrintStatus { get; set; } = 3;

        internal string PrintStatusText { get; set; }

        internal string PrinterIp
        {
            get
            {
                var ip = "";
                try
                {
                    ip = KitchenData.GetValue<string>("PrinterIp").Split(',')[0];
                }
                catch (Exception) { }
                return ip;
            }
        }

        internal string PrinterBackupIp
        {
            get
            {
                var ip = "";
                try
                {
                    ip = KitchenData.GetValue<string>("PrinterIp").Split(',')[1];
                }
                catch (Exception) { }
                return ip;
            }
        }

        internal string PrinterName
        {
            get => KitchenData.GetValue<string>("PrinterName");
        }

        internal async Task<EpsonResponse> CheckPrinterAsync(string printerIp)
        {
            var epsonResponse = new EpsonResponse();
            IPAddress ip;
            if (IPAddress.TryParse(printerIp, out ip))
            {
                _eposClient.EposDeviceName = PrinterName;
                var uri = $"http://{printerIp}/cgi-bin/epos/service.cgi?devid=local_printer&timeout=10000";
                var printCmd = new PrinterCommand("local_printer");
                epsonResponse = await _eposClient.SendRequest(new UriBuilder(uri).ToString(), true, printCmd.Command);
            }
            else
            {
                PrintStatusText = $"Invalid printer ipaddress format of {PrinterName} with ip {printerIp}";
                epsonResponse.Message = PrintStatusText;
            }
            return epsonResponse;
        }

        internal async Task<EpsonResponse> PrintAsync(string printerIp)
        {
            var epsonResponse = new EpsonResponse();
            IPAddress ip;
            if (IPAddress.TryParse(printerIp, out ip))
            {
                _eposClient.EposDeviceName = PrinterName;
                var uri = $"http://{printerIp}/cgi-bin/epos/service.cgi?devid=local_printer&timeout=60000";
                var printCmd = BuildCommand();
                epsonResponse = await _eposClient.SendRequest(new UriBuilder(uri).ToString(), false, printCmd.Command);
            }
            else
            {
                PrintStatusText = $"Invalid printer ipaddress format of {PrinterName} with ip {printerIp}";
                epsonResponse.Message = PrintStatusText;
            }
            return epsonResponse;
        }

        PrinterCommand BuildCommand()
        {
            var printCmd = new PrinterCommand("local_printer");

            if (!string.IsNullOrEmpty(PrintJobId))
                printCmd.SetPrintJobId(PrintJobId);

            printCmd.AddTextAlign(PrinterCommand.AlignCenter);
            foreach (DataRow r in PrintData.Rows)
            {
                var dataType = r.GetValue<sbyte>("DataType");
                var noCol = r.GetValue<sbyte>("NoCol");
                var textAlignCol1 = r.GetValue<sbyte>("TextAlignCol1");
                var textAlignCol2 = r.GetValue<sbyte>("TextAlignCol2");
                var fontStyle = r.GetValue<string>("FontStyle");
                var textCol1 = r.GetValue<string>("TextCol1");
                var textCol2 = r.GetValue<string>("TextCol2");
                var textCol3 = r.GetValue<string>("TextCol3");
                var isTextLine = dataType < 2;
                var isLogo = dataType == 2;
                var isCenter = noCol == 1 && textAlignCol1 == 1;
                var isLeft = (noCol == 0 || noCol == 1) && textAlignCol1 == 0;
                var isSeparator = dataType == 3;
                var is2colLeftRight = noCol == 2 && (textAlignCol2 == 0 || textAlignCol2 == 2);
                var is3col = noCol == 3;
                var isNormalFont = fontStyle == "N";
                var isHeader1 = fontStyle == "H1";
                var isHeader2 = fontStyle == "H2";
                var isBarcode = dataType == 4;
                var isQrCode = dataType == 5;

                if (isLogo)
                {
                    printCmd.AddImage(EpsonPrintManager.Instance.LogoBase64,
                        EpsonPrintManager.Instance.LogoWidth,
                        EpsonPrintManager.Instance.LogoHeight);
                }
                printCmd.AddTextFont(PrinterCommand.FontB);
                printCmd.AddTextBold(false);
                printCmd.AddTextSize(1, 1);
                if (isHeader1 || isHeader2)
                {
                    printCmd.AddTextBold(true);
                    printCmd.AddTextSize(2, 2);
                    if (isHeader1)
                    {
                        printCmd.AddTextBold(false);
                        printCmd.AddTextFont(PrinterCommand.FontC);
                    }
                }
                if (isSeparator)
                {
                    string separater = "";
                    separater = separater.PadRight(MaxChar, '-');
                    printCmd.AddText(separater + '\n');
                }
                if (isTextLine)
                {
                    if (isCenter)
                    {
                        printCmd.AddText($"{textCol1}\n");
                    }
                    else if (isLeft)
                    {
                        textCol1 = textCol1.PadRightIgnoreVowel(MaxChar) + "\n";
                        printCmd.AddText(textCol1);
                    }
                    else if (is2colLeftRight)
                    {
                        var col1MaxChar = Col1MaxChar;
                        if (isHeader1 || isHeader2)
                            col1MaxChar = Col1MaxChar - 24;
                        textCol1 = textCol1.PadRightIgnoreVowel(col1MaxChar);
                        textCol2 = textCol2.PadLeft(Col2MaxChar + Col3MaxChar);
                        printCmd.AddText($"{textCol1}{textCol2}\n");
                    }
                    else if (is3col)
                    {
                        textCol1 = textCol1.PadRightIgnoreVowel(Col1MaxChar);
                        textCol2 = textCol2.PadLeft(Col2MaxChar);
                        textCol3 = textCol3.PadLeft(Col3MaxChar);
                        printCmd.AddText($"{textCol1}{textCol2}{textCol3}\n");
                    }
                }
                if (isBarcode || isQrCode)
                {
                    printCmd.AddTextFont(PrinterCommand.FontA);
                    printCmd.AddTextBold(false);
                    printCmd.AddTextSize(1, 1);
                    if (isBarcode)
                    {
                        printCmd.AddBarcode($"{textCol1}");
                        printCmd.AddText("\n");
                    }
                    if (isQrCode)
                    {
                        printCmd.AddQRcode($"{textCol1}");
                        //printCmd.AddText($"{textCol1}\n");
                    }
                }
            }
            printCmd.AddFeed(1);
            if (Redirect)
            {
                printCmd.AddTextAlign(PrinterCommand.AlignLeft);
                printCmd.AddTextSize(1, 1);
                printCmd.AddTextBold(false);
                printCmd.AddText($"Redirect from {PrinterIp}\n");
            }
            if (AutoCut)
                printCmd.AddCut();
            return printCmd;
        }
    }
}
