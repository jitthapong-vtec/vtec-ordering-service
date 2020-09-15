using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public interface IDbstructureUpdateService
    {
        Task UpdateStructureAsync();
    }
}
