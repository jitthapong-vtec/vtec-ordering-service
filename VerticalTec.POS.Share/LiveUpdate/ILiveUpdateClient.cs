using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Share.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task SendVersionInfo();

        Task StartUpdate();

        Task CancelUpdate();

        Task SendUpdateStatus();

        Task ReceiveUpdateStatus(VersionLiveUpdate liveUpdate);
    }
}
