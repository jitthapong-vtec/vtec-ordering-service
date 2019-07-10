using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using vtecPOS.GlobalFunctions;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
{
    public class SyncController : ApiController
    {
        IDatabase _database;
        POSModule _posModule;

        public SyncController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posModule = posModule;
        }

        [HttpGet]
        [Route("v1/sync/inv")]
        public async Task<IHttpActionResult> SyncInvAsync(int shopId = 0, string docDate = "")
        {
            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var respText = "";
                    var exportJson = "";
                    var dataSet = new DataSet();
                    var exportType = 100;
                    var documentId = 0;
                    var keyShopId = 0;
                    var merchantId = 0;
                    var brandId = 0;

                    var success = _posModule.ExportInventData(ref respText, ref dataSet, ref exportJson, exportType, docDate, shopId,
                        documentId, keyShopId, merchantId, brandId, conn as MySqlConnection);
                    if (success)
                    {
                        var url = "";
                        try
                        {
                            var vdsUrl = GetPropertyValue(conn, 1050, "vdsurl");
                            vdsUrl = $"{vdsUrl}/v1/import/inv";
                            Uri uriResult;
                            var isValidUrl = Uri.TryCreate(vdsUrl, UriKind.Absolute, out uriResult)
                                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                            url = vdsUrl;
                            if (!isValidUrl)
                                url = $"http://{vdsUrl}";
                        }
                        catch (Exception) { }
                        if (string.IsNullOrEmpty(url))
                        {
                            result.StatusCode = HttpStatusCode.InternalServerError;
                            result.Message = "vds parameter in property 1050 is not set or did not enabled this property";
                            return result;
                        }

                        try
                        {
                            var syncJson = await HttpClientManager.Instance.PostAsync<string>(url, exportJson);
                            success = _posModule.SyncInventUpdate(ref respText, syncJson, conn as MySqlConnection);
                            result.Success = success;
                            if (success)
                            {
                                result.Message = $"Sync inventory data {exportType} successfully";
                            }
                            else
                            {
                                result.Message = respText;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.StatusCode = HttpStatusCode.RequestTimeout;
                            if (ex is HttpRequestException)
                                result.Message = $"Connecton timeout {url}";
                            else if (ex is HttpResponseException)
                                result.Message = (ex as HttpResponseException).Response.ReasonPhrase;
                            else
                                result.Message = ex.Message;
                        }
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.OK;
                        result.Message = respText;
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

        string GetPropertyValue(IDbConnection conn, int propertyId, string param, int shopId = 0, int computerId = 0)
        {
            var dtProp = GetProgramProperty(conn, propertyId);
            if (dtProp.Rows.Count == 0)
                return "";
            var propRow = dtProp.Rows[0];
            if (propRow.GetValue<int>("PropertyValue") == 0)
                throw new Exception($"Property {propertyId} is disabled");
            if (dtProp.Rows.Count > 1)
            {
                var keyId = 0;
                var propLevel = propRow.GetValue<int>("PropertyLevelID");

                if (propLevel == 1)
                    keyId = shopId;
                else if (propLevel == 2)
                    keyId = computerId;

                var propLevelShop = dtProp.AsEnumerable().Where(row => row.GetValue<int>("KeyID") == keyId).FirstOrDefault();
                if (propLevelShop != null)
                    propRow = propLevelShop;
            }
            var dict = ExtractPropertyParameter(propRow.GetValue<string>("PropertyTextValue"));
            var val = dict.FirstOrDefault(x => x.Key == param).Value;
            return val;
        }

        DataTable GetProgramProperty(IDbConnection conn, int propertyId)
        {
            string sqlQuery = "select a.*, b.PropertyLevelID from programpropertyvalue a" +
                " left join programproperty b" +
                " on a.PropertyID=b.PropertyID" +
                " where a.PropertyID=@propertyId";
            IDbCommand cmd = _database.CreateCommand(sqlQuery, conn);
            cmd.Parameters.Add(_database.CreateParameter("@propertyId", propertyId));
            DataTable dtResult = new DataTable();
            using (IDataReader reader = cmd.ExecuteReader())
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        Dictionary<string, string> ExtractPropertyParameter(string propParams)
        {
            var props = propParams.Split(';').AsParallel().Select(x => x.Split('=')).ToArray();
            var dict = new Dictionary<string, string>();
            foreach (var prop in props)
            {
                try
                {
                    if (!dict.Keys.Contains(prop[0]))
                        dict.Add(prop[0], prop[1]);
                }
                catch (Exception) { }
            }
            return dict;
        }

    }
}
