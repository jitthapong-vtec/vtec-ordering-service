using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Printer.Epson
{
    public class EpsonResponse
    {
        public bool Success { get; set; }
        public string JobId { get; set; }
        public string Code { get; set; }
        public int Status { get; set; }
        public string Message { get; set; }
    }
}
