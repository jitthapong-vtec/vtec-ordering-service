using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Utils
{
    public class LogManager
    {
        private static LogManager instance;
        private static object syncRoot = new object();

        public static LogManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new LogManager();
                    }
                }
                return instance;
            }
        }

        public enum LogTypes
        {
            Information,
            Error
        }

        private string _logPath;
        private string _prefixFileName;

        private LogManager() { }

        public void InitLogManager(string logPath, string prefixFileName)
        {
            _logPath = logPath;
            _prefixFileName = prefixFileName;
            if (!_logPath.EndsWith("/"))
                _logPath += "/";
            try
            {
                if (!Directory.Exists(logPath))
                    Directory.CreateDirectory(logPath);
            }
            catch (Exception) { }
        }

        public bool EnableLog { get; set; } = true;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void WriteLog(string log, LogTypes logType = LogTypes.Information)
        {
            if (!EnableLog) return;
            try
            {
                string logFile = GetFilePath();
                using (StreamWriter sw = new StreamWriter(logFile, true))
                {
                    if (logType == LogTypes.Error)
                        log = $"ERR! {log}";
                    sw.WriteLine($"[{DateTime.Now.ToShortTimeString()}]: {log}");
                }
            }
            catch (Exception) { }
        }

        public async Task WriteLogAsync(string log, LogTypes logType = LogTypes.Information)
        {
            if (!EnableLog) return;
            var logFile = GetFilePath();
            if (logType == LogTypes.Error)
                log = $"ERR! {log}";
            log = $"[{ DateTime.Now.ToShortTimeString()}]: {log}\n\r";
            try
            {
                byte[] encodedText = Encoding.Unicode.GetBytes(log);
                using (FileStream sourceStream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
                };
            }
            catch (Exception) { }
        }

        string GetFilePath()
        {
            return $"{_logPath}{_prefixFileName}{DateTime.Now.ToString("yyyy-MM-dd", new CultureInfo("en-US"))}.txt";
        }
    }
}
