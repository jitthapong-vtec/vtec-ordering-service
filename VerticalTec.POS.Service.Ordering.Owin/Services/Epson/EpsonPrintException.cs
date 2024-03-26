using System;

namespace VerticalTec.POS.Printer.Epson
{
    public class EpsonPrintException : Exception
    {
        public EpsonPrintException(string message) : base(message) { }
        public EpsonPrintException(string message, Exception inner) : base(message, inner) { }
    }
}
