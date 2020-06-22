using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.LiveUpdateConsole.Hubs
{
    public class ConsoleHub : Hub<IConsoleHub>
    {
        IHubContext<LiveUpdateHub, ILiveUpdateClient> _hubContext;

        public ConsoleHub(IHubContext<LiveUpdateHub, ILiveUpdateClient> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task GetClientInfo(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
                await _hubContext.Clients.All.ReceiveCmd(LiveUpdateCommands.ReceiveVersionDeploy);
            else
                await _hubContext.Clients.Client(connectionId).ReceiveCmd(LiveUpdateCommands.ReceiveVersionDeploy, connectionId);
        }

        public async Task SendDownloadFileCommand(string connectionId)
        {
            await _hubContext.Clients.Client(connectionId).ReceiveCmd(LiveUpdateCommands.DownloadFile);
        }

        public async Task SendBackupCommand(string connectionId)
        {
            await _hubContext.Clients.Client(connectionId).ReceiveCmd(LiveUpdateCommands.BackupFile);
        }
    }
}
