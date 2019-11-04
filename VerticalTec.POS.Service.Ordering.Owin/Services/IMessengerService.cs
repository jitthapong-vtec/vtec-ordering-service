using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public interface IMessengerService
    {
        void SendMessage(string message = "102|101");
    }
}
