using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

namespace VerticalTec.POS.Printer.Epson
{
    public class ReceiptPrinter
    {
        DatabaseManager _databaseManager;

        Printer _printer;
        
        public ReceiptPrinter()
        {
            _databaseManager = new DatabaseManager();
            _printer = new Printer();
        }

        public string PrinterIds { get; set; }

        public string PrinterIp { get; set; }

        public PaperSizes PaperSize { get; set; } = PaperSizes.Size80;

        public async Task<EpsonResponse> PrintBillDetailAsync(DataSet data)
        {
            var response = new EpsonResponse();
            try
            {
                _printer.PaperSize = PaperSize;
                if (!string.IsNullOrEmpty(PrinterIds))
                {
                    var printerData = _databaseManager.GetPrinter(PrinterIds);
                    PrinterIp = printerData.Rows[0].GetValue<string>("PrinterDeviceBackup").Split(',')[0];
                    if (printerData.Rows[0].GetValue<int>("IsOposPrinter") == 1)
                        _printer.PaperSize = PaperSizes.Size58;
                }
                _printer.PrintData = data.Tables["ReceiptPrintData"];
                response = await _printer.PrintAsync(PrinterIp);
            }
            catch (Exception ex) {
                response.Message = ex.Message;
            }
            return response;
        }
    }
}
