using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdateAgent.Events
{
    public enum UpdateEvents
    {
        Updating,
        UpdateSuccess,
        UpdateFail
    }

    public class VersionUpdateEvent : PubSubEvent<UpdateEvents>
    {
    }
}
