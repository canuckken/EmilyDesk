using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using XWidgetReborn.Runtime.Core;
using XWidgetReborn.Shared;

namespace XWidgetReborn.Runtime
{
    internal sealed class RuntimeApplicationContext : ApplicationContext
    {
        private readonly RuntimeHost _host;
        private readonly IRuntimeLoggingService _logging;
        private readonly EngineHostWindow _engineHostWindow;
        private readonly EventWaitHandle _openClock;
        private readonly EventWaitHandle _closeClock;
        private readonly EventWaitHandle _widgetCommand;
        private readonly EventWaitHandle _exitEngine;
        private readonly System.Windows.Forms.Timer _commandTimer;
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private bool _exitThreadCoreEntered;
        private IntPtr _activeRebornWindow = IntPtr.Zero;
        private IntPtr _lastLayerForeground = IntPtr.Zero;
        private IntPtr _lastDiagnosticForeground = IntPtr.Zero;

        private delegate bool EnumWindowsCallback(
            IntPtr window,
            IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hWnd,
            out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(
            EnumWindowsCallback callback,
            IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(
            string className,
            string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(
            IntPtr hWnd,
            StringBuilder className,
            int maximumCount);

        public RuntimeApplicationContext(
            string widgetPath,
            IRuntimeLoggingService logging)
        {
            if (logging == null) throw new ArgumentNullException("logging");
            _logging = logging;
            _logging.Debug("RuntimeApplicationContext constructing.");
            // Anchor the application lifetime to a dedicated invisible host, not to
            // any widget window. This allows the Engine to remain alive with zero
            // widgets loaded and continue servicing IPC commands.
            _engineHostWindow = new EngineHostWindow(_logging);
            MainForm = _engineHostWindow;
            _engineHostWindow.CreateControl();
            _logging.Debug("EngineHostWindow created. Handle=" + _engineHostWindow.Handle + "; MainForm assigned=true.");

            bool ignored;
            _openClock = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.RuntimeOpenClockEvent, out ignored);
            _closeClock = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.RuntimeCloseClockEvent, out ignored);
            _widgetCommand = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.RuntimeCommandEvent, out ignored);
            _exitEngine = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.RuntimeExitEvent, out ignored);

            _logging.Debug("IPC event handles opened.");

            _host = new RuntimeHost(_logging);
            _host.Changed += delegate { WriteStatus(); };
            _host.Start(widgetPath);

            _commandTimer = new System.Windows.Forms.Timer();
            _commandTimer.Interval = 200;
            _commandTimer.Tick += PollCommands;
            _commandTimer.Start();
            WriteStatus();
            _logging.Information("RuntimeApplicationContext ready. Clock open=" + _host.ClockIsOpen + ".");
        }

        private void PollCommands(object sender, EventArgs e)
        {
            // Keep filesystem enumeration, window-layer inspection and the
            // atomic status-file replacement off the widget drag path.
            // Commands remain queued until the pointer is released; exit is
            // still handled immediately.
            if (EngineScheduler.IsWidgetDragActive)
            {
                if (_exitEngine.WaitOne(0))
                    ExitThread();
                return;
            }
            // Process the queue on every tick as well as consuming the wake-up
            // signal. This recovers safely if a client exits immediately after
            // publishing a command but before signalling the event.
            _widgetCommand.WaitOne(0);
            ProcessWidgetCommands();
            LogForegroundTransition();
            EnforceRebornWidgetLayer();

            // Retain the clock-specific events for compatibility with Dashboard
            // builds released before the generic command transport.
            if (_openClock.WaitOne(0))
            {
                _logging.Debug("Legacy open-clock command received.");
                _host.OpenWidget(EngineClient.NativeClockWidgetId);
            }
            if (_closeClock.WaitOne(0))
            {
                _logging.Debug("Legacy close-clock command received.");
                _host.CloseWidget(EngineClient.NativeClockWidgetId);
            }
            if (_exitEngine.WaitOne(0))
            {
                _logging.Information("Explicit Engine exit requested.");
                ExitThread();
                return;
            }
            WriteStatus();
        }

        private void LogForegroundTransition()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero ||
                foreground == _lastDiagnosticForeground)
                return;
            _lastDiagnosticForeground = foreground;

            uint processId;
            GetWindowThreadProcessId(
                foreground,
                out processId);
            string windowClass =
                GetWindowClass(foreground);
            WidgetLayerMode mode =
                EngineClient.GetWidgetLayerMode();
            IntPtr enforcementCacheBefore =
                _lastLayerForeground;
            uint rebornProcessId = 0;
            if (_activeRebornWindow != IntPtr.Zero &&
                IsWindow(_activeRebornWindow))
                GetWindowThreadProcessId(
                    _activeRebornWindow,
                    out rebornProcessId);
            bool desktopWindow =
                string.Equals(
                    windowClass,
                    "Progman",
                    StringComparison.Ordinal) ||
                string.Equals(
                    windowClass,
                    "WorkerW",
                    StringComparison.Ordinal);
            string decision;
            if (mode != WidgetLayerMode.AboveXWidgetReborn)
                decision = "skipped: selected mode is " + mode;
            else if (_activeRebornWindow == IntPtr.Zero ||
                !IsWindow(_activeRebornWindow))
                decision = "skipped: notified Reborn HWND is invalid";
            else if (foreground == _lastLayerForeground)
                decision = "skipped: foreground HWND already evaluated";
            else if (rebornProcessId != processId &&
                !desktopWindow)
                decision = "skipped: ordinary non-Reborn application";
            else
                decision = "execute: Build 1326 transition path";
            _logging.Debug(
                "Widget layer diagnostic transition: trigger=foreground-change" +
                "; mode=" + mode +
                "; foreground=0x" +
                foreground.ToInt64().ToString("X") +
                "; class=" + windowClass +
                "; pid=" + processId +
                "; notifiedReborn=0x" +
                _activeRebornWindow.ToInt64().ToString("X") +
                "; enforcementCacheBefore=0x" +
                enforcementCacheBefore.ToInt64().ToString("X") +
                "; enforcementCacheAfter=unchanged-by-diagnostics" +
                "; decision=\"" + decision + "\"" +
                ".");
            // XWidget changes foreground between its layered surface and
            // transient helper windows during interaction. These transitions
            // cannot execute Reborn layer policy, so avoid synchronous shell
            // enumeration and a full multi-window disk-logged snapshot. The
            // concise transition above remains sufficient to identify the
            // foreground HWND and skip reason.
            if (decision.StartsWith(
                    "skipped: ordinary non-Reborn application",
                    StringComparison.Ordinal) &&
                IsLegacyHostProcess(processId))
            {
                _logging.Debug(
                    "Widget layer diagnostic snapshot skipped: " +
                    "foreground process is xwidget.exe; no policy " +
                    "operation can execute for this transition.");
                return;
            }
            LogShellWindows();
            _host.LogWidgetLayerSnapshot(
                _activeRebornWindow,
                "foreground-change/" + windowClass);
        }

        private static bool IsLegacyHostProcess(uint processId)
        {
            if (processId == 0)
                return false;
            try
            {
                using (Process process =
                    Process.GetProcessById((int)processId))
                    return string.Equals(
                        process.ProcessName,
                        "xwidget",
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void LogShellWindows()
        {
            IntPtr progman =
                FindWindow("Progman", null);
            IntPtr taskbar =
                FindWindow("Shell_TrayWnd", null);
            var workers = new StringBuilder();
            EnumWindows(
                delegate(IntPtr window, IntPtr parameter)
                {
                    if (!string.Equals(
                        GetWindowClass(window),
                        "WorkerW",
                        StringComparison.Ordinal))
                        return true;
                    if (workers.Length > 0)
                        workers.Append(",");
                    workers.Append("0x");
                    workers.Append(
                        window.ToInt64().ToString("X"));
                    return true;
                },
                IntPtr.Zero);
            _logging.Debug(
                "Widget layer shell snapshot: Progman=0x" +
                progman.ToInt64().ToString("X") +
                "; WorkerW=[" + workers + "]" +
                "; Shell_TrayWnd=0x" +
                taskbar.ToInt64().ToString("X") + ".");
        }


        private void EnforceRebornWidgetLayer()
        {
            if (EngineClient.GetWidgetLayerMode() != WidgetLayerMode.AboveXWidgetReborn)
                return;
            if (_activeRebornWindow == IntPtr.Zero || !IsWindow(_activeRebornWindow))
                return;

            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return;
            if (foreground == _lastLayerForeground)
                return;
            _lastLayerForeground = foreground;

            uint rebornProcessId;
            uint foregroundProcessId;
            GetWindowThreadProcessId(_activeRebornWindow, out rebornProcessId);
            GetWindowThreadProcessId(foreground, out foregroundProcessId);
            string foregroundClass = GetWindowClass(foreground);
            bool desktopForeground =
                string.Equals(
                    foregroundClass,
                    "Progman",
                    StringComparison.Ordinal) ||
                string.Equals(
                    foregroundClass,
                    "WorkerW",
                    StringComparison.Ordinal);
            // Keep widgets above any active Dashboard/Gallery-owned window,
            // and restore the same normal-band chain after Explorer activates
            // the desktop. Ordinary applications remain free to cover widgets.
            // Always anchor the chain to the last explicitly notified Reborn
            // Form HWND so shell/menu HWNDs can never become chain members.
            if ((rebornProcessId != 0 &&
                rebornProcessId == foregroundProcessId) ||
                desktopForeground)
            {
                _logging.Debug(
                    "Widget layer foreground transition: foreground=0x" +
                    foreground.ToInt64().ToString("X") +
                    "; class=" + foregroundClass +
                    "; desktop=" + desktopForeground +
                    "; notifiedReborn=0x" +
                    _activeRebornWindow.ToInt64().ToString("X") +
                    "; pid=" + foregroundProcessId + ".");
                _host.BringWidgetsAboveXWidgetReborn(
                    _activeRebornWindow);
            }
        }

        private static string GetWindowClass(IntPtr window)
        {
            var className = new StringBuilder(128);
            return GetClassName(
                window,
                className,
                className.Capacity) > 0
                ? className.ToString()
                : string.Empty;
        }

        private void ProcessWidgetCommands()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppConstants.ProductName,
                AppConstants.RuntimeCommandDirectoryName);
            if (!Directory.Exists(folder)) return;

            string[] commands;
            try { commands = Directory.GetFiles(folder, "*.command"); }
            catch (Exception ex)
            {
                _logging.Warning("Could not enumerate widget commands: " + ex.Message);
                return;
            }

            Array.Sort(commands, StringComparer.OrdinalIgnoreCase);
            foreach (string path in commands)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Length > 4096)
                    {
                        _logging.Warning("Rejected oversized widget command: " + info.Name + ".");
                        continue;
                    }

                    string[] lines = File.ReadAllLines(path);
                    if (lines.Length < 2)
                    {
                        _logging.Warning("Rejected incomplete widget command: " + info.Name + ".");
                        continue;
                    }

                    string action = lines[0].Trim();
                    string widgetId = Uri.UnescapeDataString(lines[1].Trim());
                    if (string.Equals(action, "refresh", StringComparison.OrdinalIgnoreCase))
                    {
                        _host.RefreshWidgets();
                        _logging.Information("Widget registry refresh command processed.");
                        continue;
                    }
                    if (string.Equals(action, "refresh-designer", StringComparison.OrdinalIgnoreCase))
                    {
                        _host.RefreshDesignerLayouts();
                        continue;
                    }
                    if (string.Equals(action, "refresh-weather", StringComparison.OrdinalIgnoreCase))
                    {
                        _host.RefreshWeatherWidgets();
                        _logging.Information("Weather widget refresh command processed.");
                        continue;
                    }
                    if (string.Equals(action, "raise", StringComparison.OrdinalIgnoreCase))
                    {
                        long windowValue;
                        IntPtr rebornWindow = long.TryParse(widgetId, out windowValue)
                            ? new IntPtr(windowValue) : IntPtr.Zero;
                        _activeRebornWindow = rebornWindow;
                        _lastLayerForeground = IntPtr.Zero;
                        _host.BringWidgetsAboveXWidgetReborn(rebornWindow);
                        uint processId;
                        GetWindowThreadProcessId(
                            rebornWindow,
                            out processId);
                        _logging.Debug(
                            "Widget layer refresh command processed: mode=" +
                            EngineClient.GetWidgetLayerMode() +
                            "; HWND=0x" +
                            rebornWindow.ToInt64().ToString("X") +
                            "; valid=" + IsWindow(rebornWindow) +
                            "; pid=" + processId + ".");
                        continue;
                    }
                    if (string.Equals(action, "arrange", StringComparison.OrdinalIgnoreCase))
                    {
                        long windowValue;
                        IntPtr applicationWindow = long.TryParse(widgetId, out windowValue)
                            ? new IntPtr(windowValue) : IntPtr.Zero;
                        _activeRebornWindow = applicationWindow;
                        _host.PlaceNormalWidgetsBehind(applicationWindow);
                        _logging.Debug(
                            "Normal widget arrangement command processed: HWND=0x" +
                            applicationWindow.ToInt64().ToString("X") +
                            "; valid=" + IsWindow(applicationWindow) + ".");
                        continue;
                    }
                    if (string.Equals(action, "layer", StringComparison.OrdinalIgnoreCase))
                    {
                        WidgetLayerMode mode;
                        if (Enum.TryParse(widgetId, true, out mode))
                        {
                            _host.SetWidgetLayerMode(mode);
                            _logging.Information("Widget layer command processed: " + mode + ".");
                        }
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(widgetId) || widgetId.Length > 200)
                    {
                        _logging.Warning("Rejected widget command with an invalid ID: " + info.Name + ".");
                        continue;
                    }

                    bool accepted;
                    if (string.Equals(action, "open-theme", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] selection = widgetId.Split(
                            new[] { '\t' },
                            2);
                        if (selection.Length != 2 ||
                            string.IsNullOrWhiteSpace(selection[0]) ||
                            string.IsNullOrWhiteSpace(selection[1]))
                        {
                            _logging.Warning("Rejected incomplete themed widget command: " + info.Name + ".");
                            continue;
                        }
                        accepted = _host.OpenWidget(selection[0], selection[1]);
                    }
                    else if (string.Equals(action, "open", StringComparison.OrdinalIgnoreCase))
                        accepted = _host.OpenWidget(widgetId);
                    else if (string.Equals(action, "close", StringComparison.OrdinalIgnoreCase))
                        accepted = _host.CloseWidget(widgetId);
                    else
                    {
                        _logging.Warning("Rejected widget command with unknown action: " + info.Name + ".");
                        continue;
                    }

                    _logging.Debug(
                        "Generic widget command processed. Action=" + action +
                        "; Widget=" + widgetId + "; Accepted=" + accepted + ".");
                }
                catch (Exception ex)
                {
                    _logging.Error(
                        "Could not process widget command " +
                        Path.GetFileName(path) + ".",
                        ex);
                }
                finally
                {
                    try { File.Delete(path); }
                    catch (Exception ex)
                    {
                        _logging.Warning(
                            "Could not remove widget command " +
                            Path.GetFileName(path) + ": " + ex.Message);
                    }
                }
            }
        }

        private void WriteStatus()
        {
            try
            {
                string content =
                    "version=" + AppConstants.Version + Environment.NewLine +
                    "buildId=" + AppConstants.BuildId + Environment.NewLine +
                    "buildDisplay=" + AppConstants.BuildDisplay + Environment.NewLine +
                    "executable=" + Application.ExecutablePath + Environment.NewLine +
                    "executableTimestampUtc=" + File.GetLastWriteTimeUtc(Application.ExecutablePath).ToString("o") + Environment.NewLine +
                    "pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + Environment.NewLine +
                    "clockOpen=" + _host.ClockIsOpen + Environment.NewLine +
                    "widgets=" + string.Join(",", _host.OpenWidgetIds) + Environment.NewLine +
                    "availableWidgets=" + SerializeWidgetCatalog(_host.AvailableWidgets) + Environment.NewLine +
                    "uptimeSeconds=" + (long)(DateTime.UtcNow - _startedUtc).TotalSeconds + Environment.NewLine +
                    "updatedUtc=" + DateTime.UtcNow.ToString("o") + Environment.NewLine;

                // EmilyDesk V1 publishes lifecycle state only under its own
                // product directory. Legacy runtime integrations are disabled.
                WriteStatusFile(AppConstants.ProductName, content);
            }
            catch (Exception ex)
            {
                _logging.Warning("Could not write engine status: " + ex.Message);
            }
        }

        private static void WriteStatusFile(string productDirectory, string content)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                productDirectory);
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, AppConstants.RuntimeStatusFileName);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, content);

            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
        }

        private static string SerializeWidgetCatalog(XWidgetReborn.Shared.WidgetDescriptor[] widgets)
        {
            var items = new System.Collections.Generic.List<string>();
            foreach (XWidgetReborn.Shared.WidgetDescriptor widget in widgets)
            {
                items.Add(
                    Uri.EscapeDataString(widget.Id) + "|" +
                    Uri.EscapeDataString(widget.Name) + "|" +
                    Uri.EscapeDataString(widget.Description) + "|" +
                    Uri.EscapeDataString(widget.Version) + "|" +
                    Uri.EscapeDataString(widget.Author) + "|" +
                    Uri.EscapeDataString(widget.IconPath) + "|" +
                    Uri.EscapeDataString(widget.PreviewPath) + "|" +
                    Uri.EscapeDataString(widget.Category) + "|" +
                    (widget.IsOfficial ? "1" : "0") + "|" +
                    (widget.IsBundled ? "1" : "0") + "|" +
                    Uri.EscapeDataString(widget.MinimumEngineVersion) + "|" +
                    Uri.EscapeDataString(string.Join(",", widget.Capabilities)));
            }
            return string.Join(";", items.ToArray());
        }

        protected override void ExitThreadCore()
        {
            if (_exitThreadCoreEntered)
            {
                _logging.Debug("ExitThreadCore re-entered; ignoring duplicate call.");
                return;
            }
            _exitThreadCoreEntered = true;
            _logging.Debug("ExitThreadCore entered. Engine application context shutting down.");
            _logging.Debug("Stopping command timer.");
            _commandTimer.Stop();
            _commandTimer.Dispose();
            _logging.Debug("Disposing IPC event handles.");
            _openClock.Dispose();
            _closeClock.Dispose();
            _widgetCommand.Dispose();
            _exitEngine.Dispose();
            _logging.Debug("Disposing RuntimeHost.");
            _host.Dispose();
            _logging.Debug("RuntimeHost disposed.");
            if (_engineHostWindow != null && !_engineHostWindow.IsDisposed)
            {
                // Detach ApplicationContext's MainForm closed handler before
                // closing the anchor to avoid re-entering ExitThreadCore.
                _logging.Debug("Closing EngineHostWindow for approved shutdown.");
                MainForm = null;
                _engineHostWindow.PermitShutdown();
                _engineHostWindow.Close();
                _engineHostWindow.Dispose();
            }
            _logging.Debug("Calling base ExitThreadCore.");
            base.ExitThreadCore();
            _logging.Debug("ExitThreadCore completed.");
        }
    }
}
