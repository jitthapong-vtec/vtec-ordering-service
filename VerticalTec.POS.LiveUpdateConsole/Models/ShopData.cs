﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Models
{
    public class ShopData
    {
        public int BrandId { get; set; }
        public int ShopCatId { get; set; }
        public int ShopId { get; set; }
        public string ShopCode { get; set; }
        public string ShopName { get; set; }
        public bool Selected { get; set; }
    }
}
