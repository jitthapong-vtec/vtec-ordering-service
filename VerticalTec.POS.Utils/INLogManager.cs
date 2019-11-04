using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Utils
{
    public interface INLogManager
    {
        void LogInfo(string message);
        void LogWarn(string message);
        void LogDebug(string message);
        void LogError(string message);
    }
}
