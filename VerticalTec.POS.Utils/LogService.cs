using NLog;

namespace VerticalTec.POS.Utils
{
    public class LogService : ILogService
    {
        private static ILogger logger = NLog.LogManager.GetCurrentClassLogger();

        public bool Enabled { get; set; } = true;

        public void LogDebug(string message)
        {
            if (Enabled)
                logger.Debug(message);
        }

        public void LogError(string message)
        {
            if (Enabled)
                logger.Error(message);
        }

        public void LogInfo(string message)
        {
            if (Enabled)
                logger.Info(message);
        }

        public void LogWarn(string message)
        {
            if (Enabled)
                logger.Warn(message);
        }
    }
}
