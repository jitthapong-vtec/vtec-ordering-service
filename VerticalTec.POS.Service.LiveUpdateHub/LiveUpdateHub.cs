using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Share.LiveUpdate;

namespace VerticalTec.POS.Service.LiveUpdateHub
{
    public class LiveUpdateHub : Hub<ILiveUpdateClient>
    {
        IDatabase _db;
        LiveUpdateDbContext _liveUpdateCtx;

        public LiveUpdateHub(IDatabase db, LiveUpdateDbContext liveUpdateCtx)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Client(Context.ConnectionId).SendVersionInfo();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task UpdateVersionInfo(VersionInfo info)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, info);
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}