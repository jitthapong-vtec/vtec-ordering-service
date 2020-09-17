using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Services
{
    public interface IClientConnectionService
    {
        HubConnection HubConnection { get; }
        
        void InitConnection(string hubUrl);

        Task StartConnectionAsync(CancellationToken token = default);

        Task StopConnectionAsync(CancellationToken token = default);

        void Subscribe(string methodName, Func<Task> handler);

        void Subscribe<T1>(string methodName, Func<T1, Task> handler);

        void Subscribe<T1, T2>(string methodName, Func<T1, T2, Task> handler);
    }
}
