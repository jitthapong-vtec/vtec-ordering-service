using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task SyncVersion();

        Task ReceiveSyncVersion(VersionInfo versionInfo, VersionDeploy versionDeploy);

        Task ReceiveUpdateVersionState(VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog liveUpdateLog);
    }
}
