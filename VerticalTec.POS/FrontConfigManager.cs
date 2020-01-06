using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Database;

namespace VerticalTec.POS
{
    public class FrontConfigManager
    {
        public FrontConfigManager()
        {
            POSDataSetting = new POSDataSetting();
        }

        public async Task LoadConfig(string path)
        {
            using(var reader = File.OpenText(path))
            {
                var content = await reader.ReadToEndAsync();
                var frontConfig = JsonConvert.DeserializeObject<FrontConfig>(content);
                POSDataSetting = frontConfig.POSDataSetting;
            }
        }

        public POSDataSetting POSDataSetting { get; set; }
    }

    public class FrontConfig
    {
        public POSDataSetting POSDataSetting { get; set; }
    }

    public class POSDataSetting
    {
        public int ShopID { get; set; }
        public int ComputerID { get; set; }
    }
}
