using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace XWidgetReborn.Shared
{
    /// <summary>
    /// Single authority for discovering, starting and commanding the original
    /// XWidget runtime. Reborn never starts the legacy host during normal
    /// startup; it is activated only when a legacy widget command is issued.
    /// </summary>
    public static class LegacyRuntimeManager
    {
        private const string SettingsSection = "Root";
        private const string HighPerformanceKey =
            "EnabledHighPerformanceMode";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileString(
            string section,
            string key,
            string defaultValue,
            StringBuilder value,
            uint size,
            string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WritePrivateProfileString(
            string section,
            string key,
            string value,
            string filePath);

        public sealed class Status
        {
            public bool IsInstalled { get; internal set; }
            public bool IsRunning { get; internal set; }
            public string ExecutablePath { get; internal set; }
            public int ProcessCount { get; internal set; }
        }

        public static Status GetStatus()
        {
            string executable = XWidgetLocator.FindExecutable();
            int count = 0;
            try
            {
                Process[] processes = Process.GetProcessesByName("xwidget");
                count = processes == null ? 0 : processes.Length;
                if (processes != null)
                {
                    foreach (Process process in processes)
                        process.Dispose();
                }
            }
            catch
            {
                count = 0;
            }

            return new Status
            {
                IsInstalled = !string.IsNullOrWhiteSpace(executable),
                IsRunning = count > 0,
                ExecutablePath = executable,
                ProcessCount = count
            };
        }

        public static bool EnsureRunning(out string error)
        {
            error = null;
            string performanceError;
            EnsureHighPerformanceMode(
                out performanceError);
            if (GetStatus().IsRunning)
                return true;

            string executable = XWidgetLocator.FindExecutable();
            if (string.IsNullOrWhiteSpace(executable))
            {
                error = "The original XWidget executable could not be found.";
                return false;
            }

            try
            {
                using (Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = Path.GetDirectoryName(executable),
                    UseShellExecute = false
                }))
                {
                    if (process == null)
                    {
                        error = "The original XWidget runtime could not be started.";
                        return false;
                    }

                    // The legacy executable is a single-instance GUI host. A process
                    // existing is not enough: its command receiver must be initialized
                    // before Reborn sends the first openWidget request.
                    try { process.WaitForInputIdle(5000); }
                    catch (InvalidOperationException) { }
                    catch (NotSupportedException) { }
                }

                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline)
                {
                    if (GetStatus().IsRunning)
                    {
                        // Allow the tray host/message window to finish registering.
                        Thread.Sleep(500);
                        return true;
                    }
                    Thread.Sleep(100);
                }

                error = "The original XWidget runtime did not become ready in time.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Enables the original engine's own cached high-performance rendering
        /// path. XWidget reads this setting when its single shared process
        /// starts; Reborn does not replace or intercept the legacy drag loop.
        /// </summary>
        public static bool EnsureHighPerformanceMode(
            out string error)
        {
            error = null;
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments),
                    "XWidget");
                Directory.CreateDirectory(folder);
                string settingsPath =
                    Path.Combine(folder, "Settings.ini");
                var current = new StringBuilder(32);
                GetPrivateProfileString(
                    SettingsSection,
                    HighPerformanceKey,
                    string.Empty,
                    current,
                    (uint)current.Capacity,
                    settingsPath);
                string value =
                    current.ToString().Trim();
                if (string.Equals(
                        value,
                        "1",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        value,
                        "true",
                        StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!WritePrivateProfileString(
                    SettingsSection,
                    HighPerformanceKey,
                    "1",
                    settingsPath))
                {
                    error =
                        "Windows could not update " +
                        settingsPath + ".";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void ExecuteWidgetCommand(string command, string target)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("A legacy widget command is required.", "command");
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("A legacy widget target is required.", "target");

            string executable = XWidgetLocator.FindExecutable();
            if (string.IsNullOrWhiteSpace(executable))
                throw new FileNotFoundException(
                    "The original XWidget application is required to control legacy widgets.");

            string startupError;
            if (!EnsureRunning(out startupError))
                throw new InvalidOperationException(
                    "The legacy compatibility runtime could not be started. " + startupError);

            string argument = command + ":" + target;
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "\"" + argument.Replace("\"", "\\\"") + "\"",
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                if (process == null)
                    throw new InvalidOperationException(
                        "The original XWidget command could not be started.");
                if (!process.WaitForExit(5000))
                    return;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        "The original XWidget command failed with exit code " +
                        process.ExitCode + ".");
            }
        }
    }
}
