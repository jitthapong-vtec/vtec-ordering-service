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

        Task ClientDisconnect(string connectionId);
    }
}
