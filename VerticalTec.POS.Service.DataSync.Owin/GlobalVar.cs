using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.DataSync.Owin
{
    public class GlobalVar
    {
        static GlobalVar _instance;
        public static GlobalVar Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GlobalVar();
                return _instance;
            }
        }

        public string DbServer { get; set; }
        public string DbName { get; set; }
    }
}
