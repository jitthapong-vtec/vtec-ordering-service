using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Service.Ordering.Owin.Models;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public class KDSHub : Hub
    {
        private static ConcurrentDictionary<string, KDSClient> KdsClients = new ConcurrentDictionary<string, KDSClient>();

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
