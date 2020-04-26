using Google.Apis.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public static class LoggerExtensions
    {
        public static void LogInfo(this Logger logger, string msg)
        {
            Console.WriteLine(msg);
            logger.Info(msg);
        }

        public static void LogError(this Logger logger, string msg, Exception ex = null)
        {
            Console.WriteLine($"{msg} {ex?.Message}");
            if (ex != null)
                logger.Error(ex, msg);
            else 
                logger.Error(msg);
        }
    }
}
