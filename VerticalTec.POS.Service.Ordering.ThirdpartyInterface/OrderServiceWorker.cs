using Microsoft.AspNetCore.SignalR.Client;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.ThirdpartyInterface.Worker
{
    public class OrderServiceWorker : IDisposable
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private HubConnection _connection;

        private string _dbServer;
        private string _dbName;
        private string _orderingServiceUrl;

        private string _shopKey;

        private HttpClient _orderingServiceClient;

        public OrderServiceWorker(string dbServer, string dbName, string orderingServiceUrl = "http://127.0.0.1:9500")
        {
            _dbServer = dbServer;
            _dbName = dbName;
            _orderingServiceUrl = orderingServiceUrl;

            _orderingServiceClient = new HttpClient
            {
                BaseAddress = new UriBuilder(_orderingServiceUrl).Uri
            };
        }

        public async Task InitConnectionAsync()
        {
            _logger.Info("InitConnectionAsync...");

            try
            {
                using (var conn = new MySqlConnection(MySQLConnectionString))
                {
                    await conn.OpenAsync();

                    var cmdText = "select a.*, b.* from shop_data a join merchant_data b on a.MerchantID=b.MerchantID where a.ShopID=(select ShopID from computername where Deleted=0 and ComputerType=0 limit 1);" +
                        "select PropertyID, PropertyValue, PropertyTextValue from programpropertyvalue where PropertyID in (9,10,24,1011,1130);";

                    var dsData = await MySqlHelper.ExecuteDatasetAsync(conn, cmdText);
                    var dtShopData = dsData.Tables[0];
                    var dtProperty = dsData.Tables[1];

                    var shopData = dtShopData.AsEnumerable().Select(r => new
                    {
                        MerchantKey = (string)r["MerchantKey"],
                        ShopKey = (string)r["ShopKey"]
                    }).First();

                    _shopKey = shopData.ShopKey;

                    var apiConfig = dtProperty.AsEnumerable().Where(r => (int)r["PropertyID"] == 1130);
                    if (apiConfig?.Any() == false)
                        throw new Exception("Not found property 1130!");

                    var apiBaseUrl = "";
                    try
                    {
                        var apiData = apiConfig.Select(r =>
                        {
                            var textValue = (string)r["PropertyTextValue"];

                            apiBaseUrl = textValue.Split(';').Where(key => key.StartsWith("ApiBaseServerUrl")).Select(key => key.Split('=')[1]).First();
                            if (apiBaseUrl.EndsWith("/") == false)
                                apiBaseUrl += "/";

                            return new
                            {
                                ApiBaseUrl = apiBaseUrl
                            };
                        }).First();
                    }
                    catch { }

                    if (string.IsNullOrEmpty(apiBaseUrl))
                        throw new Exception("Please check ApiBaseServerUrl in property 1130!");

                    _logger.Info("ApiPlatformBaseUrl: {0}", apiBaseUrl);

                    _connection = new HubConnectionBuilder()
                        .WithUrl($"{apiBaseUrl}orderingservice")
                        .Build();

                    _connection.Closed += _connection_Closed;

                    _connection.On("ThirdpartySubmitOrder", async (string order) => await OnSubmitOrder(order));
                    _connection.On("ThirdpartyInquiryOrder", async (string orderId) => await OnInquiryOrder(orderId));

                    _connection.On("IOrderCheckDevice", async (string deviceId) => await CheckDeviceAsync(deviceId));
                    _connection.On("IOrderListTable", async (string shopId, string terminalId) => await ListTableAsync(shopId, terminalId));
                    _connection.On("IOrderListProduct", async (string shopId, string terminalId) => await ListProductAsync(shopId, terminalId));
                    _connection.On("IOrderOpenTable", async (string data) => await OpenTableAsync(data));
                    _connection.On("IOrderAddOrder", async (string data) => await AddOrderAsync(data));
                    _connection.On("IOrderUpdateOrder", async (string data) => await UpdateOrderAsync(data));
                    _connection.On("IOrderDeleteOrder", async (string data) => await DeleteOrderAsync(data));
                    _connection.On("IOrderSubmitOrder", async (string data) => await SubmitOrderAsync(data));
                    _connection.On("IOrderListOrder", async (string shopId, string transactionId, string computerId) => await ListOrderAsync(shopId, transactionId, computerId));

                    await StartConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InitConnectionAsync");
                throw;
            }
        }

        private async Task _connection_Closed(Exception arg)
        {
            await Task.Delay(5000);
            await StartConnectionAsync();
        }

        private async Task StartConnectionAsync()
        {
            while (true)
            {
                if (_connection?.State == HubConnectionState.Connected)
                    return;

                try
                {
                    _logger.Info("Connecting...");
                    await _connection.StartAsync();
                    var resp = await _connection.InvokeAsync<object>("RegisterClient", _shopKey);
                    _logger.Info("Connected width connectionId {0}", _connection.ConnectionId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Info("Connection error {0}", ex.Message);
                    await Task.Delay(5000);
                }
            }
        }

        private async Task<string> CheckDeviceAsync(string deviceId)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                respMsg = await _orderingServiceClient.GetAsync($"v1/devices/mobile?deviceId={deviceId}");
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "CheckDevice");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> ListTableAsync(string shopId, string terminalId)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                respMsg = await _orderingServiceClient.GetAsync($"v1/tables?shopId={shopId}&terminalId={terminalId}");
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ListTable");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> OpenTableAsync(string data)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "v1/tables/open")
                {
                    Content = new StringContent(data, Encoding.UTF8, "application/json")
                };
                respMsg = await _orderingServiceClient.SendAsync(reqMsg);
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "OpenTable");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> ListProductAsync(string shopId, string terminalId)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                respMsg = await _orderingServiceClient.GetAsync($"v1/products?shopId={shopId}&terminalId={terminalId}");
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ListProduct");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> AddOrderAsync(string data)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "v1/orders")
                {
                    Content = new StringContent(data, Encoding.UTF8, "application/json")
                };
                respMsg = await _orderingServiceClient.SendAsync(reqMsg);
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "AddOrder");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> UpdateOrderAsync(string data)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "v1/orders/update")
                {
                    Content = new StringContent(data, Encoding.UTF8, "application/json")
                };
                respMsg = await _orderingServiceClient.SendAsync(reqMsg);
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "UpdateOrder");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> DeleteOrderAsync(string data)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "v1/orders/delete")
                {
                    Content = new StringContent(data, Encoding.UTF8, "application/json")
                };
                respMsg = await _orderingServiceClient.SendAsync(reqMsg);
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DeleteOrder");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> SubmitOrderAsync(string data)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "v1/orders/submit")
                {
                    Content = new StringContent(data, Encoding.UTF8, "application/json")
                };
                respMsg = await _orderingServiceClient.SendAsync(reqMsg);
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DeleteOrder");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }

        private async Task<string> ListOrderAsync(string shopId, string transactionId, string computerId)
        {
            var result = "";
            HttpResponseMessage respMsg = null;
            try
            {
                respMsg = await _orderingServiceClient.GetAsync($"v1/orders?transactionId={transactionId}&computerId={computerId}&shopId={shopId}");
                result = await respMsg.Content.ReadAsStringAsync();
                respMsg.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ListOrder");
                result = CreateOrderingServiceErrorObj(result, respMsg, ex);
            }
            return result;
        }


        private static string CreateOrderingServiceErrorObj(string result, HttpResponseMessage respMsg, Exception ex)
        {
            var errMsg = "";
            try
            {
                errMsg = JObject.Parse(result)["message"].ToString();
            }
            catch { }

            result = JsonConvert.SerializeObject(new
            {
                isError = true,
                statusCode = respMsg.StatusCode,
                message = string.IsNullOrEmpty(errMsg) ? ex.Message : errMsg
            });
            return result;
        }

        private async Task<string> OnSubmitOrder(string jsonData)
        {
            _logger.Info("Received order {0}", jsonData);

            var result = "";
            try
            {
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "v1/orders/thirdparty")
                {
                    Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
                };
                var resp = await _orderingServiceClient.SendAsync(reqMsg);
                resp.EnsureSuccessStatusCode();
                var respJson = await resp.Content.ReadAsStringAsync();
                _logger.Info("v1/orders/thirdparty {0}", respJson);

                var jObj = JObject.Parse(respJson);
                if (jObj["Code"]?.ToString().StartsWith("200") == false)
                    throw new Exception(jObj["Message"]?.ToString() ?? "");

                var billHtml = jObj["Data"]["BillHtml"].ToString();
                var tranKey = jObj["Data"]["TranKey"].ToString();
                var receiptNumber = jObj["Data"]["ReceiptNumber"].ToString();

                try
                {
                    var tid = Convert.ToInt32(tranKey.Split(':')[0]);
                    var cid = Convert.ToInt32(tranKey.Split(':')[1]);
                    SendMessageSyncClient(tid, cid);
                }
                catch { }

                var respObj = new
                {
                    Code = "200.200",
                    Data = new
                    {
                        tranKey = tranKey,
                        billHtml = billHtml,
                        receiptNumber = receiptNumber
                    }
                };

                result = JsonConvert.SerializeObject(respObj);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "v1/orders/thirdparty");
                var err = new
                {
                    Code = "500.000",
                    Message = ex.Message
                };
                result = JsonConvert.SerializeObject(err);
            }
            return result;
        }

        public void SendMessageSyncClient(int iTransID, int iCompID)
        {
            Task.Run(() =>
            {
                try
                {
                    var iSyncClientPort = 7001;
                    IPAddress ServerIP = ServerIP = IPAddress.Parse("127.0.0.1");

                    var netClient = new TcpClient();
                    var result = netClient.BeginConnect(ServerIP.ToString(), iSyncClientPort, null, null);
                    result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    if (netClient.Connected)
                    {
                        NetworkStream clientSockStream = netClient.GetStream();
                        var clientStreamWriter = new StreamWriter(clientSockStream);

                        if (clientSockStream.CanWrite)
                        {
                            var InvC = System.Globalization.CultureInfo.InvariantCulture;
                            string szSaleDate = DateTime.Today.ToString("yyyy-MM-dd", InvC);
                            var szSndMsg = "100|" + iCompID + "|ThirdpartyInterface|" + iTransID + "|" + iCompID + "|" + szSaleDate + "|" + "\0";

                            clientStreamWriter.WriteLine(szSndMsg);
                            clientStreamWriter.Flush();
                        }

                        clientStreamWriter.Close();
                        clientSockStream.Close();
                        netClient.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Call sync client");
                }
            });
        }

        private async Task<string> OnInquiryOrder(string orderId)
        {
            var respJson = "";
            try
            {
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, $"v1/orders/thirdparty/inquiry?orderId={orderId}");
                var resp = await _orderingServiceClient.SendAsync(reqMsg);
                resp.EnsureSuccessStatusCode();
                respJson = await resp.Content.ReadAsStringAsync();
                _logger.Info($"v1/orders/thirdparty/inquiry?orderId={orderId}", respJson);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"v1/orders/thirdparty/inquiry?orderId={orderId}");

                var err = new
                {
                    Code = "500.000",
                    Message = ex.Message
                };
                respJson = JsonConvert.SerializeObject(err);
            }
            return respJson;
        }


        public void Dispose()
        {
            _connection?.DisposeAsync();
        }

        public string MySQLConnectionString => new MySqlConnectionStringBuilder
        {
            Server = _dbServer,
            Database = _dbName,
            UserID = "vtecPOS",
            Password = "vtecpwnet",
            Port = 3308,
            AllowUserVariables = true,
            DefaultCommandTimeout = 60 * 5,
            SslMode = MySqlSslMode.None,
            OldGuids = true
        }.ConnectionString;
    }
}
