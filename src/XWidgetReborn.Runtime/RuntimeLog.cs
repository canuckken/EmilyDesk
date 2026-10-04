using System;
using System.IO;
using System.Text;

namespace XWidgetReborn.Runtime
{
    internal static class RuntimeLog
    {
        private static readonly object Sync = new object();

        public static string LogPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    "Logs");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "runtime.log");
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(
                        LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                        " [PID " + System.Diagnostics.Process.GetCurrentProcess().Id + "] " +
                        message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never prevent the runtime from starting.
            }
        }
    }
}
