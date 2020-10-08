using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Services
{
    public class ClientConnectionService : IClientConnectionService
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        CancellationTokenSource _retryConnectionTokenSource;

        public HubConnection HubConnection { get; private set; }

        string _hubUrl;

        public void InitConnection(string hubUrl)
        {
            _hubUrl = hubUrl;

            HubConnection = new HubConnectionBuilder()
                   .WithUrl(hubUrl)
                   .WithAutomaticReconnect()
                   .Build();
            HubConnection.Reconnecting += Reconnecting;
            HubConnection.Reconnected += Reconnected;
            HubConnection.Closed += Closed;
        }

        private async Task Closed(Exception arg)
        {
            _logger.Info("The connection was closed and retry to connect");
            _retryConnectionTokenSource?.Cancel();
            _retryConnectionTokenSource = new CancellationTokenSource();

            var token = _retryConnectionTokenSource.Token;
            await StartConnectionAsync(token);
        }

        private Task Reconnected(string arg)
        {
            _logger.Info($"Successfully reconnected {arg}");
            return Task.FromResult(true);
        }

        private Task Reconnecting(Exception arg)
        {
            _logger.Info($"Try reconnecting...{arg}");
            return Task.FromResult(true);
        }

        public void Subscribe(string methodName, Func<Task> handler)
        {
            HubConnection.On(methodName, handler);
        }

        public void Subscribe<T1>(string methodName, Func<T1, Task> handler)
        {
            HubConnection.On(methodName, handler);
        }

        public void Subscribe<T1, T2>(string methodName, Func<T1, T2, Task> handler)
        {
            HubConnection.On(methodName, handler);
        }

        public async Task StartConnectionAsync(CancellationToken token = default)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    break;
                try
                {
                    _logger.Info("Connect to live update server...");

                    await HubConnection.StartAsync(token);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Info($"Could not connect to live update server! {ex.Message}");

                    if (ex is ObjectDisposedException)
                        InitConnection(_hubUrl);
                    await Task.Delay(1000);
                }
            }
        }

        public Task StopConnectionAsync(CancellationToken token = default)
        {
            try
            {
                return HubConnection.DisposeAsync();
            }
            catch
            {
                return Task.FromResult(true);
            }
        }
    }
}
