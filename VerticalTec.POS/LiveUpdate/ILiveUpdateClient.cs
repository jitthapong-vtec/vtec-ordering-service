using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task SyncVersion();

        Task ReceiveSyncVersion(VersionInfo versionInfo, VersionDeploy versionDeploy, VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog liveUpdateLog);

        Task UpdateVersion();

        Task ReceiveUpdateVersionState(VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog liveUpdateLog);
    }
}
