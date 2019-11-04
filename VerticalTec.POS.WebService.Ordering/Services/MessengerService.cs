using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using VtecMessenger;

namespace VerticalTec.POS.WebService.Ordering.Services
{
    public class MessengerService
    {
        static object lockSync = new object();
        static MessengerService _instance;

        public static MessengerService Instance
        {
            get
            {
                if(_instance == null)
                {
                    lock (lockSync)
                    {
                        if (_instance == null)
                            _instance = new MessengerService();
                    }
                }
                return _instance;
            }
        }

        public void SendMessage(string message = "102|101")
        {
            Messenger.Instance.Send(
                       new MessageObject.Builder(0, "OrderingApi")
                           .SetCommand(CommandTypes.Refresh)
                           .SetMessageText(message)
                           .MessageObj);
        }

        public void SetupMessenger()
        {
            string kdsManagerIp = ConfigurationManager.AppSettings["DBServer"];
            Messenger.Instance.SubscribeConnectionStatusEvent(ConnectionEvent);
            Messenger.Instance.Connect(string.IsNullOrEmpty(kdsManagerIp) ? "127.0.0.1" : kdsManagerIp);
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