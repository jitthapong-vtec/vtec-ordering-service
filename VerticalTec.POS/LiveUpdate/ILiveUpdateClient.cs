using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task ReceiveConnectionEstablished();

        Task ReceiveVersionDeploy(List<VersionDeploy> versionsDeploy);

        Task ReceiveCmdUpdateVersion();

        Task ReceiveCmdBackup();
    }
}
