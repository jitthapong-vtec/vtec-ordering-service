using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.DataSync.Owin.Services
{
    public interface IFailureDataSyncRecovery
    {
        Task RecoveryInventoryDataSync();

        Task RecoverySaleDataSync();
    }
}
