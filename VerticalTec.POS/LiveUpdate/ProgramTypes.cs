using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    [Flags]
    public enum ProgramTypes
    {
        All = 0,
        Front = 1,
        Backoffice = 2,
        KDS = 3
    }
}
