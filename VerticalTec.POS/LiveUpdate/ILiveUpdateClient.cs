using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task SendVersionInfo();

        Task StartUpdate();

        Task CancelUpdate();

        Task SendUpdateStatus();

        Task ReceiveSyncVersionInfo(VersionInfo versionInfo);

        Task ReceiveSyncVersionDeploy(VersionDeploy versionDeploy);

        Task ReceiveUpdateStatus(VersionLiveUpdate liveUpdate);
    }
}
