using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Printer.Epson
{
    public class PrinterInfo : INotifyPropertyChanged
    {
        string _printerName;
        string _printerIp;
        bool _online;
        string _statusText;

        public int PrinterID { get; set; }

        public string PrinterName
        {
            get => _printerName;
            set
            {
                _printerName = value;
                NotifyPropertyChanged();
            }
        }

        public string PrinterIp
        {
            get => _printerIp;
            set
            {
                _printerIp = value;
                NotifyPropertyChanged();
            }
        }

        public bool Online
        {
            get => _online;
            set
            {
                _online = value;
                NotifyPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                NotifyPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
