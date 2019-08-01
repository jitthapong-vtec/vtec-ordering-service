using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
{
    public class SaleController : ApiController
    {
        const string LogPrefix = "Sale_";

        IDatabase _database;
        POSModule _posModule;

        public SaleController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posModule = posModule;
        }

        [HttpGet]
        [Route("v1/sale/sendtohq")]
        public async Task<IHttpActionResult> SendSaleAsync(int shopId)
        {
            await LogManager.Instance.WriteLogAsync($"Call v1/sale/sendtohq?shopId={shopId}", LogPrefix);

            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync() as MySqlConnection)
                {
                    var prop = new ProgramProperty(_database);
                    var vdsUrl = "";
                    try
                    {
                        vdsUrl = prop.GetVdsUrl(conn);
                    }
                    catch (Exception)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = "vdsurl parameter in property 1050 is not set or did not enabled this property";
                        return result;
                    }

                    var importApiUrl = $"{vdsUrl}/v1/sale/import";
                    var respText = "";
                    var dtSale = new DataTable();
                    var actionType = 0;
                    var shopData = new ShopData(_database);
                    var merchantId = 0;
                    var brandId = 0;
                    try
                    {
                        var shop = await shopData.GetShopDataAsync(conn, shopId);
                        merchantId = shop.GetValue<int>("MerchantID");
                        brandId = shop.GetValue<int>("BrandID");
                    }
                    catch (Exception) { }
                    var success = _posModule.SaleLogBranch(ref respText, ref dtSale, shopId, actionType, conn);
                    if (!success)
                    {
                        result.Message = respText;
                        return result;
                    }

                    List<KeyValuePair<string, string>> exportSales = new List<KeyValuePair<string, string>>();
                    foreach (DataRow saleRow in dtSale.Rows)
                    {
                        var dataSet = new DataSet();
                        var jsonSale = "";
                        var saleDate = saleRow.GetValue<string>("SaleDateString");
                        success = _posModule.ExportData(ref respText, ref dataSet, ref jsonSale, 0, $"'{saleDate}'", shopId, 0, 0, merchantId, brandId, conn);
                        if (success)
                        {
                            KeyValuePair<string, string> saleData = new KeyValuePair<string, string>(saleDate, jsonSale);
                            exportSales.Add(saleData);

                            await LogManager.Instance.WriteLogAsync($"Export sale {saleDate} => {jsonSale}", LogPrefix);
                        }
                    }

                    try
                    {
                        //TODO: handle some sale not success
                        foreach (var exportSale in exportSales)
                        {
                            await LogManager.Instance.WriteLogAsync($"Begin send {exportSale.Key}", LogPrefix);
                            var syncJsonSale = await HttpClientManager.Instance.VDSPostAsync<string>(importApiUrl, exportSale.Value);
                            success = _posModule.SyncUpdate(ref respText, syncJsonSale, conn);
                            if (success)
                                await LogManager.Instance.WriteLogAsync($"Import {exportSale.Key} successfully", LogPrefix);
                            else
                                await LogManager.Instance.WriteLogAsync($"Import {exportSale.Key} fail", LogPrefix, LogManager.LogTypes.Error);
                        }
                        result.Success = true;
                        result.Message = "Send sale data to hq successfully";
                    }
                    catch (Exception ex)
                    {
                        if (ex is HttpRequestException)
                        {
                            var reqEx = (ex as HttpRequestException);
                            result.StatusCode = HttpStatusCode.RequestTimeout;
                            result.Message = $"{reqEx.InnerException.Message} {vdsUrl}";
                        }
                        else if (ex is HttpResponseException)
                        {
                            var respEx = (ex as HttpResponseException);
                            result.StatusCode = respEx.Response.StatusCode;
                            result.Message = $"{(ex as HttpResponseException).Response.ReasonPhrase}";
                        }
                        else
                        {
                            result.Message = ex.Message;
                        }
                        await LogManager.Instance.WriteLogAsync($"{result.Message}", LogPrefix, LogManager.LogTypes.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

    }
}
