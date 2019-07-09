using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VerticalTec.POS.Printer.Epson;

namespace VerticalTec.POS.Printer.Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            EpsonPrintManager.Instance.PrintStatus += (s, e) =>
            {
                MessageBox.Show(e.Message);
            };

            EpsonPrintManager.Instance.PrinterStatus += (sender, printers) =>
            {
                EpsonPrintManager.Instance.GetPrintersStatus();
            };
        }
    }
}
