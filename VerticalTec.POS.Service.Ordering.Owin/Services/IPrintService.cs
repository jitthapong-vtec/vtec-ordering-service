using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Service.Ordering.Owin.Models;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public interface IPrintService
    {
        Task<bool> PrintOrder(TransactionPayload payload);
        Task PrintBill(PrintData payload);
        Task PrintCheckBill(TransactionPayload payload);
        Task KioskPrintCheckBill(TransactionPayload payload);
        Task Print(int shopId, int computerId, string printerIds, string printerNames, DataSet dsPrintData, int paperSize = 80);
    }
}
