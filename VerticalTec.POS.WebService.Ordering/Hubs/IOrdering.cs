using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.WebService.Ordering.Hubs
{
    public interface IOrdering
    {
        Task GetKioskMenu(string shopId);

        Task ReceiveKioskMenu(string menus);

        Task AddOrderAsync(string payload);

        Task GetBillDataHtmlAsync(string transactionId, string computerId);
    }
}
