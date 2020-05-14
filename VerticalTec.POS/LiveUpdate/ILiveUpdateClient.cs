﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task ReceiveConnectionEstablished();

        Task ReceiveVersionDeploy(VersionDeploy versionDeploy, VersionLiveUpdate versionLiveUpdates);

        Task ReceiveSyncVersion(VersionInfo versionInfo);

        Task ReceiveSyncUpdateVersionState(VersionLiveUpdate state);

        Task ReceiveCmd(LiveUpdateCommands cmd, object param = default);
    }
}
