using Microsoft.AspNetCore.SignalR.Client;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
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

        public OrderServiceWorker(string dbServer, string dbName, string orderingServiceUrl="http://127.0.0.1:9500")
        {
            _dbServer = dbServer;
            _dbName = dbName;
            _orderingServiceUrl = orderingServiceUrl;
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
                    _connection.On("ThirdpartyInquiryOrder", async(string orderId) => await OnInquiryOrder(orderId));

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

        private async Task<string> OnSubmitOrder(string jsonData)
        {
            _logger.Info("Received order {0}", jsonData);
            var respJson = "";
            using (var httpClient = new HttpClient())
            {
                try
                {
                    httpClient.BaseAddress = new Uri(_orderingServiceUrl);
                    var reqMsg = new HttpRequestMessage(HttpMethod.Post, "v1/orders/thirdparty")
                    {
                        Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
                    };
                    var resp = await httpClient.SendAsync(reqMsg);
                    resp.EnsureSuccessStatusCode();
                    respJson = await resp.Content.ReadAsStringAsync();
                    _logger.Info("v1/orders/thirdparty", respJson);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "v1/orders/thirdparty");
                    var err = new
                    {
                        Code = "500.000",
                        Message = ex.Message
                    };
                    respJson = JsonConvert.SerializeObject(err);
                }
            }
            return respJson;
        }

        private async Task<string> OnInquiryOrder(string orderId)
        {
            var respJson = "";
            using (var httpClient = new HttpClient())
            {
                try
                {
                    httpClient.BaseAddress = new Uri(_orderingServiceUrl);
                    var reqMsg = new HttpRequestMessage(HttpMethod.Get, $"v1/orders/thirdparty/inquiry?orderId={orderId}");
                    var resp = await httpClient.SendAsync(reqMsg);
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
