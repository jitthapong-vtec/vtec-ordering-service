using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    [Flags]
    public enum VersionDeployBatchStatus
    {
        InActivate = 0,
        Actived = 1,
        Done = 2,
        Canceled = 99
    }
}
