using Microsoft.AspNet.SignalR;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public class KDSHub : Hub
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetLogger("logordering");

        private static ConcurrentDictionary<string, KDSClient> KdsClients = new ConcurrentDictionary<string, KDSClient>();

        private IDatabase _database;
        private VtecPOSRepo _vtecRepo;
        private IPrintService _printService;

        public KDSHub(IDatabase database, VtecPOSRepo vtecRepo, IPrintService printService)
        {
            _database = database;
            _vtecRepo = vtecRepo;
            _printService = printService;
        }

        public IEnumerable<object> RegisterClient(string computerId, string computerName)
        {
            try
            {
                var client = new KDSClient
                {
                    ComputerId = Convert.ToInt32(computerId),
                    ComputerName = computerName,
                    ConnectionId = Context.ConnectionId
                };
                KdsClients.AddOrUpdate(computerId, client, (key, oldClient) => oldClient = client);
                Clients.Client(Context.ConnectionId).RegisterComplete();
                return KdsClients.Values;
            }
            catch (Exception ex)
            {
                Clients.Client(Context.ConnectionId).RegisterError(ex.Message);
            }
            return null;
        }

        public async Task<string> GetKDSData(int kdsId, int shopId)
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                try
                {
                    using (_ = new InvariantCultureScope())
                    {
                        var saleDate = await _vtecRepo.GetSaleDateAsync(conn, shopId, true, true);
                        var posModule = new POSModule();
                        var respText = "";
                        var ds = new DataSet();
                        var success = posModule.KDS_Data(ref respText, ref ds, kdsId, 0, 0, shopId, saleDate, "front", conn);
                        if (!success)
                            throw new Exception(respText);
                        var json = JsonConvert.SerializeObject(ds);
                        return json;
                    }
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }
        }

        public async Task<string> KDSCheckout(dynamic[] kdsOrders, int kdsId, int shopId, int staffId = 2)
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                using (_ = new InvariantCultureScope())
                {
                    var saleDate = await _vtecRepo.GetSaleDateAsync(conn, shopId, true, true);
                    var posModule = new POSModule();

                    foreach (var order in kdsOrders)
                    {
                        var respText = "";
                        var ds = new DataSet();

                        var tid = (int)order.TransactionID;
                        var cid = (int)order.ComputerID;
                        var oid = (int)order.OrderDetailID;

                        var success = posModule.KDS_Click(ref respText, ref ds, tid, cid, oid, kdsId, shopId, saleDate, "front", staffId, conn);
                        if (success)
                        {
                            await _printService.PrintAsync(shopId, cid, ds);
                        }
                        else
                        {
                            _logger.Error($"KDS_Click => {respText}");
                        }
                    }
                    return await GetKDSData(kdsId, shopId);
                }
            }
        }

        public override Task OnConnected()
        {
            Clients.Client(Context.ConnectionId).Connected();
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected()
        {
            return base.OnReconnected();
        }
    }
}
