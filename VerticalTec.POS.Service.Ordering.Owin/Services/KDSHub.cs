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

        public override async Task OnConnected()
        {
        }

        public Task HandshakeAsync()
        {
            return Task.CompletedTask;
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
