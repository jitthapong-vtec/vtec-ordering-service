using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Printer
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
        private bool isEnabled = true;

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

        public bool IsEnabled
        {
            get
            {
                return isEnabled;
            }
            set
            {
                isEnabled = value;
            }
        }

        public void WriteLog(string log)
        {
            WriteLog(log, LogTypes.Information);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void WriteLog(string log, LogTypes logType)
        {
            if (!isEnabled)
                return;
            try
            {
                string logFile = $"{_logPath}{_prefixFileName}{DateTime.Now.ToString("yyyy-MM-dd", new CultureInfo("en-US"))}.txt";
                using (StreamWriter sw = new StreamWriter(logFile, true))
                {
                    if (logType == LogTypes.Error)
                        log = $"ERR! {log}";
                    sw.WriteLine($"{DateTime.Now.ToShortTimeString()}{log}");
                }
            }
            catch (Exception) { }
        }
    }
}
