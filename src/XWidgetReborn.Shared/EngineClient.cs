using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

namespace XWidgetReborn.Shared
{
    public enum WidgetLayerMode
    {
        AboveXWidgetReborn,
        NormalDesktop,
        AlwaysOnTop,
        Hidden
    }

    public static class EngineClient
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);
        public const string NativeClockWidgetId = "native.clock";

        public static bool OpenWidget(string widgetId)
        {
            // The Dashboard is normally the foreground process when the user
            // chooses Open. Explicitly grant the Engine permission to activate
            // the new widget window before signalling the cross-process command.
            Dictionary<string, string> status = ReadStatus();
            string pidText;
            int enginePid;
            if (status != null && status.TryGetValue("pid", out pidText) &&
                int.TryParse(pidText, out enginePid) && enginePid > 0)
            {
                try { AllowSetForegroundWindow(enginePid); }
                catch { }
            }

            return SignalWidgetCommand(widgetId, true);
        }

        public static bool OpenWidget(string widgetId, string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
                return OpenWidget(widgetId);
            Dictionary<string, string> status = ReadStatus();
            string pidText;
            int enginePid;
            if (status != null && status.TryGetValue("pid", out pidText) &&
                int.TryParse(pidText, out enginePid) && enginePid > 0)
            {
                try { AllowSetForegroundWindow(enginePid); }
                catch { }
            }
            return SignalCommand(
                "open-theme",
                widgetId + "\t" + theme);
        }

        public static bool CloseWidget(string widgetId)
        {
            return SignalWidgetCommand(widgetId, false);
        }

        public static bool ExitEngine()
        {
            return Signal(AppConstants.RuntimeExitEvent);
        }

        public static IList<WidgetDescriptor> ListAvailableWidgets()
        {
            Dictionary<string, string> status = ReadStatus();
            var result = new List<WidgetDescriptor>();
            if (status == null) return result;

            string value;
            if (!status.TryGetValue("availableWidgets", out value) || string.IsNullOrWhiteSpace(value))
                return result;

            foreach (string item in value.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(item)) continue;
                string[] fields = item.Split('|');
                if (fields.Length < 3) continue;
                try
                {
                    result.Add(new WidgetDescriptor(
                        Uri.UnescapeDataString(fields[0]),
                        Uri.UnescapeDataString(fields[1]),
                        Uri.UnescapeDataString(fields[2]),
                        fields.Length > 3 ? Uri.UnescapeDataString(fields[3]) : string.Empty,
                        fields.Length > 4 ? Uri.UnescapeDataString(fields[4]) : string.Empty,
                        fields.Length > 5 ? Uri.UnescapeDataString(fields[5]) : string.Empty,
                        fields.Length > 6 ? Uri.UnescapeDataString(fields[6]) : string.Empty,
                        fields.Length > 7 ? Uri.UnescapeDataString(fields[7]) : string.Empty,
                        fields.Length > 8 && fields[8] == "1",
                        fields.Length > 9 && fields[9] == "1",
                        fields.Length > 10 ? Uri.UnescapeDataString(fields[10]) : string.Empty,
                        fields.Length > 11 && fields[11].Length > 0
                            ? Uri.UnescapeDataString(fields[11]).Split(',')
                            : new string[0]));
                }
                catch { }
            }
            return result;
        }

        public static IList<string> ListOpenWidgets()
        {
            Dictionary<string, string> status = ReadStatus();
            var result = new List<string>();
            if (status == null) return result;

            string value;
            if (!status.TryGetValue("widgets", out value) || string.IsNullOrWhiteSpace(value))
                return result;

            foreach (string item in value.Split(','))
            {
                string id = item.Trim();
                if (id.Length > 0) result.Add(id);
            }
            return result;
        }

        public static bool IsWidgetOpen(string widgetId)
        {
            foreach (string id in ListOpenWidgets())
                if (string.Equals(id, widgetId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool SignalWidgetCommand(string widgetId, bool open)
        {
            if (string.IsNullOrWhiteSpace(widgetId) || widgetId.Length > 200)
                return false;

            if (SignalCommand(open ? "open" : "close", widgetId))
                return true;

            // During an in-place update, the new Dashboard can briefly be paired
            // with an older Engine that has no generic command event.
            if (string.Equals(widgetId, NativeClockWidgetId, StringComparison.OrdinalIgnoreCase))
                return Signal(open
                    ? AppConstants.RuntimeOpenClockEvent
                    : AppConstants.RuntimeCloseClockEvent);
            return false;
        }

        public static bool RefreshWidgets()
        {
            return SignalCommand("refresh", "registry");
        }

        // Provider selection is global, while each Weather widget keeps its
        // own refresh schedule. This wakes the open Weather widgets so a
        // Dashboard provider change is visible immediately.
        public static bool RefreshWeatherWidgets()
        {
            return SignalCommand("refresh-weather", "all");
        }

        public static bool RefreshDesignerLayouts()
        {
            return SignalCommand("refresh-designer", "all");
        }

        public static bool BringWidgetsAboveXWidgetReborn()
        {
            return BringWidgetsAboveXWidgetReborn(IntPtr.Zero);
        }

        public static bool BringWidgetsAboveXWidgetReborn(IntPtr rebornWindow)
        {
            return SignalCommand("raise", rebornWindow.ToInt64().ToString());
        }

        public static bool PlaceNormalWidgetsBehind(IntPtr applicationWindow)
        {
            return SignalCommand("arrange", applicationWindow.ToInt64().ToString());
        }

        public static WidgetLayerMode GetWidgetLayerMode()
        {
            try
            {
                string path = GetLayerPreferencePath();
                if (!File.Exists(path)) return WidgetLayerMode.NormalDesktop;
                WidgetLayerMode mode;
                if (!Enum.TryParse(File.ReadAllText(path).Trim(), true, out mode))
                    return WidgetLayerMode.NormalDesktop;
                return mode == WidgetLayerMode.AboveXWidgetReborn
                    ? WidgetLayerMode.NormalDesktop : mode;
            }
            catch { return WidgetLayerMode.NormalDesktop; }
        }

        public static bool SetWidgetLayerMode(WidgetLayerMode mode)
        {
            try
            {
                string path = GetLayerPreferencePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, mode.ToString());
            }
            catch { return false; }
            return SignalCommand("layer", mode.ToString());
        }

        private static string GetLayerPreferencePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppConstants.ProductName,
                "widget-layer.mode");
        }

        private static bool SignalCommand(string action, string target)
        {
            EventWaitHandle signal = null;
            string temporaryPath = null;
            try
            {
                // Opening the event first prevents commands from being left behind
                // while the Engine is offline and unexpectedly running on restart.
                signal = EventWaitHandle.OpenExisting(AppConstants.RuntimeCommandEvent);

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppConstants.ProductName,
                    AppConstants.RuntimeCommandDirectoryName);
                Directory.CreateDirectory(folder);

                string commandName = Guid.NewGuid().ToString("N");
                temporaryPath = Path.Combine(folder, commandName + ".tmp");
                string commandPath = Path.Combine(folder, commandName + ".command");
                string content =
                    action + Environment.NewLine +
                    Uri.EscapeDataString(target) + Environment.NewLine;

                File.WriteAllText(temporaryPath, content);
                File.Move(temporaryPath, commandPath);
                temporaryPath = null;
                signal.Set();
                return true;
            }
            catch
            {
                if (temporaryPath != null)
                {
                    try { File.Delete(temporaryPath); }
                    catch { }
                }

                return false;
            }
            finally
            {
                if (signal != null) signal.Dispose();
            }
        }

        private static bool Signal(string eventName)
        {
            try
            {
                using (EventWaitHandle signal = EventWaitHandle.OpenExisting(eventName))
                {
                    signal.Set();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, string> ReadStatus()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    AppConstants.RuntimeStatusFileName);
                if (!File.Exists(path)) return null;

                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(path))
                {
                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;
                    result[line.Substring(0, equals)] = line.Substring(equals + 1);
                }
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
