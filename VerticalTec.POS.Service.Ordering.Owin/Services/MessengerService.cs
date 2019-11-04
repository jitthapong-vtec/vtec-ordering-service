using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using VtecMessenger;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public class MessengerService : IMessengerService
    {
        public MessengerService()
        {
            var ip = AppConfig.Instance.DbServer;
            Messenger.Instance.SubscribeConnectionStatusEvent(ConnectionEvent);
            Messenger.Instance.Connect(string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip);
        }

        public void SendMessage(string message)
        {
            Messenger.Instance.Send(
                       new MessageObject.Builder(0, "OrderingApi")
                           .SetCommand(CommandTypes.Refresh)
                           .SetMessageText(message)
                           .MessageObj);
        }

        void ConnectionEvent(object sender, ConnectionStatus e)
        {
            if (e.Status == ConnectionStatus.SocketStatus.Connected)
            {
                Messenger.Instance.Send(
                       new MessageObject.Builder(0, "OrderingApi")
                           .MessageObj);
            }
        }
    }
}