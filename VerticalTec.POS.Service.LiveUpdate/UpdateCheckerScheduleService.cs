using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class UpdateCheckerScheduleService : IHostedService
    {
        IClientConnectionService _clientConnectionService;
        FrontConfigManager _fontConfigManager;

        Timer _timer;

        public UpdateCheckerScheduleService(IClientConnectionService clientConnectionService, FrontConfigManager frontConfigManager)
        {
            _clientConnectionService = clientConnectionService;
            _fontConfigManager = frontConfigManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero,
               TimeSpan.FromSeconds(30));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            var posSetting = _fontConfigManager.POSDataSetting;
            _clientConnectionService.HubConnection.InvokeAsync("SendVersionDeploy", posSetting);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}
