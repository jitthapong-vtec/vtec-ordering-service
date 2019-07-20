using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using VerticalTec_POS_Report_Dashboard.Models;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace VerticalTec_POS_Report_Dashboard.Controllers {

    [Route("api/[controller]")]
    public class OrdersController : Controller {

        [HttpGet]
        public object Get(DataSourceLoadOptions loadOptions) {
            return DataSourceLoader.Load(SampleData.Orders, loadOptions);
        }

    }
}