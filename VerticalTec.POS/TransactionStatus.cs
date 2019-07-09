using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Core
{
    public enum TransactionStatus
    {
        New = 1,
        Success = 2,
        Hold = 9,
        Cancel = 97
    }
}
