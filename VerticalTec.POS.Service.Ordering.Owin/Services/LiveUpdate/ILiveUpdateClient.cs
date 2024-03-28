using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task OnConnected();

        Task ReceiveVersionDeploy(VersionDeploy versionDeploy);

        Task ReceiveVersionInfo(VersionInfo versionInfo);

        Task ReceiveVersionLiveUpdate(VersionLiveUpdate versionLiveUpdate);

        Task ReceiveCmd(LiveUpdateCommands cmd, object param = default);
    }
}
