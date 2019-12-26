using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Share.LiveUpdate.SignalRHubs
{
    public interface ILiveUpdateClient
    {
        Task ClientInfo(VersionInfo versionInfo);

        Task ReceiveUpdate(UpdateInfo updateInfo);
    }
}
