using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Share.LiveUpdate
{
    public interface ILiveUpdateClient
    {
        Task SendVersionInfo();

        Task StartLiveUpdate();

        Task CancelLiveUpdate();

        Task SendUpdateStatus();

        Task ReceiveUpdateStatus(VersionLiveUpdate liveUpdate);
    }
}
