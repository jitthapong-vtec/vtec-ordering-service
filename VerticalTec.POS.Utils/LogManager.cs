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

        private LogManager() { }

        public void InitLogManager(string logPath)
        {
            _logPath = logPath;
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
        public void WriteLog(string log, string prefixFileName = "", LogTypes logType = LogTypes.Information)
        {
            if (!EnableLog) return;
            try
            {
                string logFile = GetFilePath(prefixFileName);
                using (StreamWriter sw = new StreamWriter(logFile, true))
                {
                    if (logType == LogTypes.Error)
                        log = $"ERR! {log}";
                    sw.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}]: {log}");
                }
            }
            catch (Exception) { }
        }

        public async Task WriteLogAsync(string log, string prefixFileName = "", LogTypes logType = LogTypes.Information)
        {
            if (!EnableLog) return;
            var logFile = GetFilePath(prefixFileName);
            if (logType == LogTypes.Error)
                log = $"ERR! {log}";
            log = $"[{ DateTime.Now.ToString("HH:mm:ss")}]: {log}\n\r";
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

        string GetFilePath(string prefixFileName)
        {
            return $"{_logPath}{prefixFileName}{DateTime.Now.ToString("yyyy-MM-dd", new CultureInfo("en-US"))}.txt";
        }
    }
}
