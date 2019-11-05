using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin
{
    public class AppConfig
    {
        static AppConfig _instance;

        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new AppConfig();
                return _instance;
            }
        }

        public string DbServer { get; set; }
        public string DbName { get; set; }
        public string DbPort { get; set; } = "3308";
        public bool EnableLog { get; set; }
    }
}
