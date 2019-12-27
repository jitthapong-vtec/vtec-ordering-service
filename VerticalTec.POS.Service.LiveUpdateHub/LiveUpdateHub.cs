using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Share.LiveUpdate;
using VerticalTec.POS.Share.LiveUpdate.SignalRHubs;

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

        public async Task UpdateVersionInfo(VersionInfo info)
        {
            using(var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, info);
            }
        }
    }
}