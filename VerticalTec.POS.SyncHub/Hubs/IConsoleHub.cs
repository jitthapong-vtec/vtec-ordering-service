using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.SyncHub.Hubs
{
    public interface IConsoleHub
    {
        Task ClientUpdateInfo(VersionInfo info);

        Task ClientUpdateVersionState(VersionLiveUpdate state);

        Task ClientDisconnect(string connectionId);
    }
}
