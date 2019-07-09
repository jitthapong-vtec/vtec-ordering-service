using System;
using System.Text;
using System.Xml.Linq;

namespace VerticalTec.POS.Printer.Epson
{
    class PrinterCommand
    {
        public static readonly XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        public static readonly XNamespace eposNs = "http://www.epson-pos.com/schemas/2011/03/epos-print";
        
        public const string FontA = "font_a";
        public const string FontB = "font_b";
        public const string FontC = "font_c";
        public const string FontD = "font_d";
        public const string AlignLeft = "left";
        public const string AlignCenter = "center";
        public const string AlignRight = "right";

        string _deviceId;
        int _timeout = 60000;
        string _lang;
        string _fontName;

        XElement _envelope;
        XElement _parameterElement;
        XElement _eposElement;

        public PrinterCommand(string deviceId, string lang = "en", string fontName = FontB)
        {
            _deviceId = deviceId;
            _lang = lang;
            _fontName = fontName;
            CreatePrintCommandXml();
        }

        public void SetDeviceId(string deviceId = "local_printer")
        {
            _parameterElement.Element("devid").Value = deviceId;
        }

        public void SetTimeout(int timeout = 6000)
        {
            _parameterElement.Element("timeout").Value = timeout.ToString();
        }

        public void SetPrintJobId(string jobId)
        {
            var jobElement = _parameterElement.Element(eposNs + "printjobid");
            if(jobElement == null)
            {
                _parameterElement.Add(new XElement(eposNs + "printjobid", jobId));
            }
            else
            {
                jobElement.Value = jobId;
            }
        }

        public void AddImage(string base64, int width=200, int height=100)
        {
            _eposElement.Add(new XElement(eposNs + "image", base64,
                new XAttribute("width", width),
                new XAttribute("height", height),
                new XAttribute("color", "color_1"),
                new XAttribute("mode", "gray16")));
        }

        public void AddText(string text)
        {
            _eposElement.Add(new XElement(eposNs + "text", text));
        }

        public void AddTextSize(int width, int height)
        {
            _eposElement.Add(new XElement(eposNs + "text",
                new XAttribute("width", width),
                new XAttribute("height", height)));
        }

        public void AddTextBold(bool bold)
        {
            _eposElement.Add(new XElement(eposNs + "text",
                new XAttribute("em", bold)));
        }

        public void AddTextFont(string fontName = FontB)
        {
            _eposElement.Add(new XElement(eposNs + "text",
                new XAttribute("font", fontName)));
        }

        public void AddTextAlign(string align = AlignLeft)
        {
            _eposElement.Add(new XElement(eposNs + "text",
                new XAttribute("align", align)));
        }

        public void AddSound(string pattern = "pattern_a")
        {
            _eposElement.Add(new XElement(eposNs + "sound",
                new XAttribute("pattern", pattern),
                new XAttribute("repeat", 1)));
        }

        public void AddFeed(int line)
        {
            _eposElement.Add(new XElement(eposNs + "feed", new XAttribute("line", line)));
        }

        public void AddCut()
        {
            _eposElement.Add(new XElement(eposNs + "cut"));
        }

        public void AddBarcode(string data, string type = "code93")
        {
            _eposElement.Add(new XElement(eposNs + "barcode",
                new XAttribute("hri", "below"),
                new XAttribute("height", "96"),
                new XAttribute("type", type), data));
        }

        public void AddQRcode(string data, string type = "qrcode_model_2")
        {
            _eposElement.Add(new XElement(eposNs + "symbol",
                new XAttribute("type", type),
                new XAttribute("level", "level_q"),
                new XAttribute("width", "4"), data));
        }

        public XElement Command
        {
            get
            {
                return _envelope;
            }
        }

        void CreatePrintCommandXml()
        {
            _parameterElement = new XElement(eposNs + "parameter",
                new XElement(eposNs + "devid", _deviceId),
                new XElement(eposNs + "timeout", _timeout));
            _eposElement = new XElement(eposNs + "epos-print", 
                new XElement(eposNs + "text", 
                new XAttribute("lang", _lang),
                new XAttribute("smooth", "true"),
                new XAttribute("font", _fontName)));
            _envelope = new XElement(soap + "Envelope", new XAttribute(XNamespace.Xmlns + "s", soap),
                new XElement(soap + "Header", _parameterElement),
                new XElement(soap + "Body", _eposElement));
        }
    }
}
