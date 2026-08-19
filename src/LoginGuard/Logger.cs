using System;
using System.IO;

namespace LoginGuard
{
    // Basit, kilitli dosya logu. ProgramData\LoginGuard\loginguard.log
    public static class Logger
    {
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Config.DataDir);
                lock (_lock)
                {
                    File.AppendAllText(Config.LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
                }
            }
            catch { /* log yazilamazsa sessiz gec */ }
        }
    }
}
