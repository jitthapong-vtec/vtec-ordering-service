using Microsoft.AspNetCore.SignalR.Client;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.Ordering.ThirdpartyInterface
{
    public class OrderServiceWorker : IDisposable
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private HubConnection _connection;

        private string _dbServer;
        private string _dbName;

        private string _shopKey;

        public OrderServiceWorker(string dbServer, string dbName)
        {
            _dbServer = dbServer;
            _dbName = dbName;
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

                    var apiData = dtProperty.AsEnumerable().Where(r => (int)r["PropertyID"] == 1130).Select(r =>
                    {
                        var textValue = (string)r["PropertyTextValue"];

                        var apiBaseUrl = textValue.Split(';').Where(key => key.StartsWith("ApiBaseServerUrl")).Select(key => key.Split('=')[1]).First();
                        if (apiBaseUrl.EndsWith("/") == false)
                            apiBaseUrl += "/";

                        return new
                        {
                            ApiBaseUrl = apiBaseUrl
                        };
                    }).First();

                    _connection = new HubConnectionBuilder().WithUrl($"{apiData.ApiBaseUrl}orderingservice").Build();

                    _connection.On<string>("ThirdpartySubmitOrder", OnSubmitOrder);

                    await StartConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InitConnectionAsync");
                throw;
            }
        }

        private async Task StartConnectionAsync()
        {
            while (true)
            {
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

        private void OnSubmitOrder(string orderJson)
        {
            _logger.Info("Received order {0}", orderJson);
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
