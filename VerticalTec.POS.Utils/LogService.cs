using NLog;

namespace VerticalTec.POS.Utils
{
    public class LogService : ILogService
    {
        private static ILogger logger = NLog.LogManager.GetCurrentClassLogger();

        public LogService()
        {

        }

        public void LogDebug(string message)
        {
            logger.Debug(message);
        }

        public void LogError(string message)
        {
            logger.Error(message);
        }

        public void LogInfo(string message)
        {
            logger.Info(message);
        }

        public void LogWarn(string message)
        {
            logger.Warn(message);
        }
    }
}
