using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.WebService.Ordering.Models
{
    public interface IOrdering
    {
        Task<IEnumerable<object>> GetProductsAsync();
        
        Task AddOrderAsync();
    }
}
