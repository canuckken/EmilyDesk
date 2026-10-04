using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using XWidgetWeatherBridgeV2;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal sealed class RebornApplicationContext : ApplicationContext
    {
        private static int _trayOwnerClaimed;
        private readonly EventWaitHandle _showDashboardEvent;
        private readonly EventWaitHandle _exitDashboardEvent;
        private readonly System.Windows.Forms.Timer _showDashboardTimer;
        private readonly System.Windows.Forms.Timer _startupDashboardTimer;
        private BridgeHost _weatherHost;
        private MainForm _dashboard;
        private WidgetLauncherDockForm _widgetLauncherDock;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _exiting;
        private bool _cleanupCompleted;
        private bool _trayCleanupCompleted;
        private bool _exitHandlersRegistered;
        private DateTime _lastUiExceptionWarningUtc = DateTime.MinValue;
        private readonly object _trayCleanupLock = new object();

        public RebornApplicationContext(bool showDashboardAtStartup)
        {
            bool createdNew;
            _showDashboardEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                XWidgetReborn.Shared.AppConstants.DashboardShowEvent,
                out createdNew);
            _exitDashboardEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                XWidgetReborn.Shared.AppConstants.DashboardExitEvent,
                out createdNew);

            _showDashboardTimer = new System.Windows.Forms.Timer();
            _showDashboardTimer.Interval = 250;
            _showDashboardTimer.Tick += delegate
            {
                if (_exitDashboardEvent.WaitOne(0))
                {
                    BeginFullExit();
                    return;
                }
                if (_showDashboardEvent.WaitOne(0))
                    ToggleWidgetLauncherDock();
            };
            _showDashboardTimer.Start();

            _startupDashboardTimer = new System.Windows.Forms.Timer();


            RegisterExitHandlers();
            try
            {
                WidgetPackageInstaller.CleanupDisabledPackages();
                InitializeTrayIcon();
                StartUserModeWeatherHost();
                StartIndependentRuntime();
                EmilyDeskDockController.StartIfConfigured();

                // Widgets restore at startup; the Dashboard remains closed
                // until the user explicitly requests it from the tray menu.
            }
            catch
            {
                CleanupTrayIcon();
                UnregisterExitHandlers();
                throw;
            }
        }


        private void InitializeTrayIcon()
        {
            if (Interlocked.CompareExchange(
                    ref _trayOwnerClaimed,
                    1,
                    0) != 0)
            {
                WriteDashboardLog(
                    "Duplicate tray initialization was prevented.");
                return;
            }

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open Widget Dock", null, delegate
            {
                ToggleWidgetLauncherDock();
            });
            _trayMenu.Items.Add("Open Dashboard", null, delegate
            {
                ShowDashboard();
            });
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Open Gallery", null, delegate
            {
                ShowDashboard();
                if (_dashboard != null)
                    _dashboard.ShowWidgetManager();
            });
            _trayMenu.Items.Add("Settings", null, delegate
            {
                ShowDashboard();
                if (_dashboard != null)
                    _dashboard.ShowSettings();
            });
            _trayMenu.Items.Add(new ToolStripSeparator());

            var launchWidget = new ToolStripMenuItem(
                "Launch Widget");
            launchWidget.DropDownItems.Add(
                "Clock",
                null,
                delegate
                {
                    LaunchNativeWidget("native.clock");
                });
            launchWidget.DropDownItems.Add(
                "Calendar",
                null,
                delegate
                {
                    LaunchNativeWidget("native.calendar");
                });
            launchWidget.DropDownItems.Add(
                "Weather",
                null,
                delegate
                {
                    LaunchNativeWidget("native.weather");
                });
            launchWidget.DropDownItems.Add(
                "Recycle Bin",
                null,
                delegate
                {
                    LaunchNativeWidget("native.recyclebin");
                });
            _trayMenu.Items.Add(launchWidget);

            var dockMenu = new ToolStripMenuItem("Dock");
            PopulateDockMenu(dockMenu);
            dockMenu.DropDownOpening += delegate
            {
                PopulateDockMenu(dockMenu);
            };
            _trayMenu.Items.Add(dockMenu);
            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayMenu.Items.Add("About", null, delegate
            {
                ShowDashboard();
                if (_dashboard != null)
                    _dashboard.ShowAbout();
            });
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Exit", null, delegate
            {
                BeginFullExit();
            });

            _trayIcon = new NotifyIcon();
            try
            {
                _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            _trayIcon.Text = "EmilyDesk " + AppConstants.Version;
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += delegate { ToggleWidgetLauncherDock(); };
            _trayIcon.Visible = true;
            WriteDashboardLog("Dashboard tray icon initialized.");
        }

        private void PopulateDockMenu(ToolStripMenuItem dockMenu)
        {
            dockMenu.DropDownItems.Clear();
            foreach (string themeName in EmilyDeskThemeCatalog.Names)
            {
                string selectedTheme = themeName;
                dockMenu.DropDownItems.Add(
                    "Show " + selectedTheme + " Dock", null, delegate
                    {
                        EmilyDeskDockController.ShowDockForTheme(
                            selectedTheme);
                    });
            }
            dockMenu.DropDownItems.Add(
                "Dock Settings",
                null,
                delegate
                {
                    EmilyDeskDockController.ShowSettings(_dashboard);
                });
            dockMenu.DropDownItems.Add(
                "Close Dock",
                null,
                delegate { EmilyDeskDockController.CloseDock(); });
        }

        private static void LaunchNativeWidget(string widgetId)
        {
            EnsureIndependentRuntime();
            EngineClient.OpenWidget(widgetId);
        }


        private void BeginFullExit()
        {
            if (_exiting)
                return;

            _exiting = true;
            _startupDashboardTimer.Stop();
            WriteDashboardLog("Tray command: Exit EmilyDesk. Beginning coordinated shutdown.");
            CleanupTrayIcon();
            CloseEmilyDeskDesigners();
            EmilyDeskDockController.CloseDock();
            if (_widgetLauncherDock != null &&
                !_widgetLauncherDock.IsDisposed)
            {
                _widgetLauncherDock.Dispose();
                _widgetLauncherDock = null;
            }
            StopIndependentRuntime();

            // Closing the Dashboard also closes the Gallery and all Dashboard-owned
            // diagnostic windows. Dispose is intentional here because an ordinary
            // user close hides the Dashboard for tray residency.
            if (_dashboard != null && !_dashboard.IsDisposed)
            {
                WriteDashboardLog("Disposing Dashboard and owned Gallery windows.");
                _dashboard.Dispose();
                _dashboard = null;
            }

            // ExitThreadCore removes the tray icon, stops the user-mode weather
            // bridge (when present), releases IPC handles, and ends this process.
            // The installed Windows weather service is intentionally left running.
            ExitThread();
        }

        private static void CloseEmilyDeskDesigners()
        {
            string expectedPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "EmilyDesk.Designer.exe"));
            foreach (Process process in Process.GetProcessesByName(
                "EmilyDesk.Designer"))
            {
                try
                {
                    string actualPath = Path.GetFullPath(
                        process.MainModule.FileName);
                    if (!string.Equals(expectedPath, actualPath,
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    // The Designer is a separate process. Request its normal
                    // window close so the tray Exit command closes every
                    // EmilyDesk window without touching an unrelated copy.
                    process.CloseMainWindow();
                }
                catch (Exception ex)
                {
                    WriteDashboardLog(
                        "Designer close request failed: " + ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static void StopIndependentRuntime()
        {
            int enginePid = ReadEnginePid();
            bool signalled = false;
            try { signalled = EngineClient.ExitEngine(); }
            catch (Exception ex)
            {
                WriteDashboardLog("Engine exit signal failed: " + ex);
            }
            WriteDashboardLog("Engine exit signal result=" + signalled + ".");

            if (enginePid <= 0) return;
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using (Process process = Process.GetProcessById(enginePid))
                    {
                        if (process.HasExited) return;
                    }
                }
                catch { return; }
                Thread.Sleep(100);
            }

            try
            {
                using (Process process = Process.GetProcessById(enginePid))
                {
                    string expected = Path.GetFullPath(Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        AppConstants.RuntimeExecutableName));
                    string actual = Path.GetFullPath(process.MainModule.FileName);
                    if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteDashboardLog("Engine did not exit in time; terminating verified EmilyDesk Engine PID=" + enginePid + ".");
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDashboardLog("Engine fallback termination failed: " + ex.Message);
            }
        }

        internal static void StopEngineForWidgetPackageMaintenance()
        {
            StopIndependentRuntime();
        }

        internal static bool StartEngineAfterWidgetPackageMaintenance()
        {
            WidgetPackageInstaller.CleanupDisabledPackages();
            return EnsureIndependentRuntime();
        }

        private static int ReadEnginePid()
        {
            try
            {
                string statusPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppConstants.ProductName,
                    AppConstants.RuntimeStatusFileName);
                if (!File.Exists(statusPath)) return 0;
                foreach (string line in File.ReadAllLines(statusPath))
                {
                    if (!line.StartsWith("pid=", StringComparison.OrdinalIgnoreCase)) continue;
                    int pid;
                    return int.TryParse(line.Substring(4), out pid) ? pid : 0;
                }
            }
            catch { }
            return 0;
        }

        private void RegisterExitHandlers()
        {
            if (_exitHandlersRegistered)
                return;
            _exitHandlersRegistered = true;
            Application.ApplicationExit += OnApplicationExit;
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException +=
                OnUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            SystemEvents.SessionEnding += OnSessionEnding;
        }

        private void UnregisterExitHandlers()
        {
            if (!_exitHandlersRegistered)
                return;
            _exitHandlersRegistered = false;
            Application.ApplicationExit -= OnApplicationExit;
            Application.ThreadException -= OnThreadException;
            AppDomain.CurrentDomain.UnhandledException -=
                OnUnhandledException;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            SystemEvents.SessionEnding -= OnSessionEnding;
        }

        private void OnApplicationExit(
            object sender,
            EventArgs e)
        {
            EmilyDeskDockController.CloseDock();
            CleanupTrayIcon();
        }

        private void OnProcessExit(
            object sender,
            EventArgs e)
        {
            CleanupTrayIcon();
        }

        private void OnThreadException(
            object sender,
            ThreadExceptionEventArgs e)
        {
            Exception exception = e == null ? null : e.Exception;
            WriteDashboardLog(
                "Unhandled Dashboard UI exception: " +
                (exception == null
                    ? "unknown"
                    : exception.ToString()));

            // A recoverable picker, menu, or settings-window error must not
            // terminate the tray process, widgets, Designer, and dock.
            if (_exiting || DateTime.UtcNow - _lastUiExceptionWarningUtc <
                TimeSpan.FromSeconds(5))
                return;
            _lastUiExceptionWarningUtc = DateTime.UtcNow;
            try
            {
                MessageBox.Show(
                    "EmilyDesk recovered from a user-interface error and " +
                    "will remain running.\r\n\r\n" +
                    (exception == null
                        ? "Unknown error."
                        : exception.Message),
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch
            {
            }
        }

        private void OnUnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            WriteDashboardLog(
                "Unhandled Dashboard exception; removing tray icon.");
            CleanupTrayIcon();
        }

        private void OnSessionEnding(
            object sender,
            SessionEndingEventArgs e)
        {
            WriteDashboardLog(
                "Windows session is ending; removing tray icon.");
            CleanupTrayIcon();
            if (_dashboard != null &&
                _dashboard.IsHandleCreated)
            {
                try
                {
                    _dashboard.BeginInvoke(
                        new MethodInvoker(BeginFullExit));
                }
                catch
                {
                }
            }
        }

        private void CleanupTrayIcon()
        {
            lock (_trayCleanupLock)
            {
                if (_trayCleanupCompleted)
                    return;
                _trayCleanupCompleted = true;

                NotifyIcon icon = _trayIcon;
                ContextMenuStrip menu = _trayMenu;
                _trayIcon = null;
                _trayMenu = null;

                if (icon != null)
                {
                    try
                    {
                        icon.Visible = false;
                        icon.ContextMenuStrip = null;
                    }
                    catch
                    {
                    }
                    try
                    {
                        icon.Dispose();
                    }
                    catch
                    {
                    }
                }

                if (menu != null)
                {
                    try
                    {
                        menu.Dispose();
                    }
                    catch
                    {
                    }
                }

                Interlocked.Exchange(
                    ref _trayOwnerClaimed,
                    0);
                WriteDashboardLog(
                    "Dashboard tray icon cleanup completed.");
            }
        }

        public void ShowDashboard()
        {
            // The Dashboard process may remain resident in the notification area.
            // Reopening it therefore does not construct a new application context.
            // Always verify the independent Engine here so crash recovery also works
            // when an existing Dashboard process is merely shown again.
            EnsureIndependentRuntime();

            if (_dashboard == null || _dashboard.IsDisposed)
            {
                _dashboard = new MainForm();
                _dashboard.FormClosed += delegate
                {
                    _dashboard = null;
                };
            }

            if (!_dashboard.Visible)
                _dashboard.Show();

            if (_dashboard.WindowState == FormWindowState.Minimized)
                _dashboard.WindowState = FormWindowState.Normal;

            _dashboard.ShowInTaskbar = true;
            _dashboard.BringToFront();
            _dashboard.Activate();
            _dashboard.TopMost = true;
            _dashboard.TopMost = false;
            EngineClient.PlaceNormalWidgetsBehind(_dashboard.Handle);
        }

        private void ToggleWidgetLauncherDock()
        {
            EnsureIndependentRuntime();
            if (_widgetLauncherDock == null ||
                _widgetLauncherDock.IsDisposed)
            {
                _widgetLauncherDock = new WidgetLauncherDockForm(delegate
                {
                    ShowDashboard();
                    if (_dashboard != null)
                        _dashboard.ShowWidgetManager();
                });
                _widgetLauncherDock.FormClosed += delegate
                {
                    _widgetLauncherDock = null;
                };
            }
            _widgetLauncherDock.ToggleDock();
        }

        private static void StartIndependentRuntime()
        {
            // A newly launched Dashboard can otherwise attach to a healthy
            // Engine that was started from an earlier build.  That leaves
            // updated widget-host code on disk but keeps rendering through
            // the old process.  Restart once during application startup so
            // the running Engine always matches this Dashboard deployment.
            if (EngineIsHealthy())
            {
                if (EngineBuildMatchesCurrent())
                {
                    WriteDashboardLog(
                        "Healthy current-build Engine retained during Dashboard startup.");
                    return;
                }
                WriteDashboardLog(
                    "Restarting healthy Engine because its build differs from the Dashboard build.");
                StopIndependentRuntime();
            }
            EnsureIndependentRuntime();
        }

        private static bool EnsureIndependentRuntime()
        {
            // Preserve the last known visible-widget state before replacing a dead
            // Engine. Task Manager can close top-level windows before terminating
            // the process, so the persistent enabled flag alone is not sufficient.
            bool restoreClock = LastKnownClockWasOpen();

            if (EngineIsHealthy())
            {
                WriteDashboardLog("Engine health check passed; existing instance retained.");
                return true;
            }

            string runtime = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                AppConstants.RuntimeExecutableName);

            try
            {
                if (!File.Exists(runtime))
                {
                    WriteDashboardLog("Engine executable is missing: " + runtime);
                    MessageBox.Show(
                    "The EmilyDesk Engine was not installed.\r\n\r\n" + runtime +
                        "\r\n\r\nRepair or reinstall EmilyDesk.",
                        "EmilyDesk",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }

                var info = new ProcessStartInfo
                {
                    FileName = runtime,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                };
                Process process = Process.Start(info);
                WriteDashboardLog("Engine recovery launch requested. PID=" +
                    (process == null ? "unknown" : process.Id.ToString()));

                // The Engine creates its command events and status heartbeat after
                // entering its message loop. Wait briefly so restored widgets are
                // available before the Dashboard finishes opening.
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < deadline)
                {
                    if (EngineIsHealthy())
                    {
                        if (restoreClock)
                        {
                            EngineClient.OpenWidget(EngineClient.NativeClockWidgetId);
                            WriteDashboardLog("Requested restoration of the clock from the last healthy Engine state.");
                        }
                        WriteDashboardLog("Engine recovery completed successfully.");
                        return true;
                    }
                    Thread.Sleep(100);
                }

                if (process != null && !process.HasExited)
                {
                    // Slower systems and first-run antivirus inspection can
                    // delay widget assembly loading. The command queue remains
                    // durable, so allow the living Engine to finish without a
                    // false failure dialog.
                    WriteDashboardLog(
                        "Engine is still starting after 15 seconds; startup continues in the background.");
                    return true;
                }

                WriteDashboardLog("Engine process exited before a valid heartbeat appeared.");
                MessageBox.Show(
                    "The EmilyDesk Engine closed before it finished starting.\r\n\r\n" +
                    "Check the Engine log or repair the EmilyDesk installation.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex)
            {
                WriteDashboardLog("Engine recovery launch failed: " + ex);
                MessageBox.Show(
                    "The EmilyDesk Engine could not be started.\r\n\r\n" + ex.Message,
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }


        private static bool LastKnownClockWasOpen()
        {
            try
            {
                string statusPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    AppConstants.RuntimeStatusFileName);
                if (!File.Exists(statusPath)) return false;
                foreach (string line in File.ReadAllLines(statusPath))
                {
                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;
                    if (!string.Equals(line.Substring(0, equals), "clockOpen", StringComparison.OrdinalIgnoreCase)) continue;
                    bool result;
                    return bool.TryParse(line.Substring(equals + 1), out result) && result;
                }
            }
            catch { }
            return false;
        }

        private static void SignalEngineEvent(string eventName)
        {
            try
            {
                using (EventWaitHandle signal = EventWaitHandle.OpenExisting(eventName))
                    signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                WriteDashboardLog("Engine command event was not ready: " + eventName);
            }
            catch (Exception ex)
            {
                WriteDashboardLog("Could not signal Engine event " + eventName + ": " + ex.Message);
            }
        }

        private static bool EngineIsHealthy()
        {
            try
            {
                string statusPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    AppConstants.RuntimeStatusFileName);
                if (!File.Exists(statusPath)) return false;

                string[] lines = File.ReadAllLines(statusPath);
                int pid = 0;
                DateTime updatedUtc = DateTime.MinValue;
                foreach (string line in lines)
                {
                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;
                    string name = line.Substring(0, equals);
                    string value = line.Substring(equals + 1);
                    if (string.Equals(name, "pid", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(value, out pid);
                    else if (string.Equals(name, "updatedUtc", StringComparison.OrdinalIgnoreCase))
                        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out updatedUtc);
                }

                if (pid <= 0) return false;
                Process process;
                try { process = Process.GetProcessById(pid); }
                catch { return false; }
                if (process.HasExited) return false;

                // The runtime updates this file every 200 ms. A generous timeout
                // distinguishes a live Engine from a stale status file.
                return updatedUtc != DateTime.MinValue &&
                    DateTime.UtcNow.Subtract(updatedUtc.ToUniversalTime()).TotalSeconds < 10;
            }
            catch
            {
                return false;
            }
        }

        private static bool EngineBuildMatchesCurrent()
        {
            try
            {
                string statusPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    AppConstants.RuntimeStatusFileName);
                if (!File.Exists(statusPath)) return false;
                foreach (string line in File.ReadAllLines(statusPath))
                {
                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;
                    if (!string.Equals(line.Substring(0, equals), "buildId",
                        StringComparison.OrdinalIgnoreCase)) continue;
                    return string.Equals(line.Substring(equals + 1).Trim(),
                        AppConstants.BuildId, StringComparison.Ordinal);
                }
            }
            catch { }
            return false;
        }

        private static void WriteDashboardLog(string message)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    "Logs");
                Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, "dashboard.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " +
                    message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static void LogLegacyHostState(string stage)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("xwidget");
                if (processes == null || processes.Length == 0)
                {
                    WriteDashboardLog(
                        "Legacy host audit (" + stage + "): xwidget.exe is not running.");
                    return;
                }

                foreach (Process process in processes)
                {
                    string path = "(unavailable)";
                    try
                    {
                        path = process.MainModule.FileName;
                    }
                    catch
                    {
                    }

                    WriteDashboardLog(
                        "Legacy host audit (" + stage + "): pre-existing xwidget.exe " +
                        "PID=" + process.Id + " path=" + path + ". " +
                        "This process is independent of EmilyDesk and was not launched by this build.");
                }
            }
            catch (Exception ex)
            {
                WriteDashboardLog(
                    "Legacy host audit (" + stage + ") failed: " + ex.Message);
            }
        }

        private void StartUserModeWeatherHost()
        {
            // A previous administrative installation may own the compatibility
            // URL through the LocalSystem service. Never race that owner with a
            // user-mode HttpListener: retain or start the registered service
            // first, and use the in-process host only when no service is present.
            if (ExistingWeatherEngineIsHealthy())
            {
                WriteDashboardLog(
                    "Weather startup retained the healthy existing host.");
                return;
            }

            bool serviceInstalled = WeatherServiceIsInstalled();
            if (serviceInstalled)
            {
                string serviceError;
                if (TryStartRegisteredWeatherService(out serviceError) &&
                    WaitForWeatherEngine(TimeSpan.FromSeconds(10)))
                {
                    WriteDashboardLog(
                        "Registered Weather Engine started successfully.");
                    return;
                }

                WriteDashboardLog(
                    "Registered Weather Engine startup failed: " +
                    serviceError);
                MessageBox.Show(
                    "The installed Weather Engine could not be started." +
                    "\r\n\r\n" + serviceError +
                    "\r\n\r\nRepair or reinstall EmilyDesk if this " +
                    "problem continues.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _weatherHost = new BridgeHost();
                _weatherHost.Start();
                WriteDashboardLog(
                    "User-mode Weather Engine started successfully.");
            }
            catch (Exception ex)
            {
                _weatherHost = null;

                if (ExistingWeatherEngineIsHealthy())
                    return;

                WriteDashboardLog(
                    "User-mode Weather Engine startup failed: " + ex);
                MessageBox.Show(
                    "EmilyDesk could not start the local weather API." +
                    "\r\n\r\n" + ex.Message +
                    "\r\n\r\nThe compatibility address is unavailable and " +
                    "no working EmilyDesk Weather Engine was detected." +
                    "\r\n\r\nOn a first installation, run the one-time " +
                    "Compatibility Bootstrap as administrator.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static bool WeatherServiceIsInstalled()
        {
            try
            {
                foreach (ServiceController service in
                    ServiceController.GetServices())
                {
                    using (service)
                        if (string.Equals(
                            service.ServiceName,
                            AppConstants.ServiceName,
                            StringComparison.OrdinalIgnoreCase))
                            return true;
                }
            }
            catch (Exception ex)
            {
                WriteDashboardLog(
                    "Could not inspect registered Weather Engine services: " +
                    ex);
            }
            return false;
        }

        private static bool TryStartRegisteredWeatherService(
            out string error)
        {
            error = null;
            try
            {
                using (var service = new ServiceController(
                    AppConstants.ServiceName))
                {
                    service.Refresh();
                    if (service.Status == ServiceControllerStatus.Running)
                        return true;
                    service.Start();
                    service.WaitForStatus(
                        ServiceControllerStatus.Running,
                        TimeSpan.FromSeconds(10));
                    return true;
                }
            }
            catch (Exception directError)
            {
                WriteDashboardLog(
                    "Direct Weather Engine service start failed; requesting " +
                    "the existing setup helper: " + directError);
            }

            string applicationDirectory =
                AppDomain.CurrentDomain.BaseDirectory;
            string helper = Path.Combine(
                applicationDirectory,
                "EmilyDesk.SetupHelper.exe");
            if (!File.Exists(helper))
            {
                error = "The Weather Engine setup helper is missing: " +
                    helper;
                return false;
            }

            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = helper,
                    Arguments = "start-and-verify",
                    WorkingDirectory = applicationDirectory,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        error = "The Weather Engine setup helper did not start.";
                        return false;
                    }
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        error = "The Weather Engine setup helper returned " +
                            "exit code " + process.ExitCode + ".";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool WaitForWeatherEngine(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (ExistingWeatherEngineIsHealthy()) return true;
                Thread.Sleep(100);
            }
            return false;
        }

        private static bool ExistingWeatherEngineIsHealthy()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.DownloadString(
                        AppConstants.BridgeBaseUrl +
                        "/xwidgetbridge/status.json");
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        protected override void ExitThreadCore()
        {
            EmilyDeskDockController.CloseDock();
            CleanupTrayIcon();
            if (!_cleanupCompleted)
            {
                _cleanupCompleted = true;
                _exiting = true;
                WriteDashboardLog("Dashboard application context cleanup started.");
                _showDashboardTimer.Stop();
                _showDashboardTimer.Dispose();
                _startupDashboardTimer.Stop();
                _startupDashboardTimer.Dispose();
                _showDashboardEvent.Dispose();
                _exitDashboardEvent.Dispose();

                if (_weatherHost != null)
                {
                    try
                    {
                        _weatherHost.Stop();
                    }
                    catch
                    {
                    }

                    _weatherHost.Dispose();
                    _weatherHost = null;
                }

                if (_dashboard != null && !_dashboard.IsDisposed)
                    _dashboard.Dispose();
                if (_widgetLauncherDock != null &&
                    !_widgetLauncherDock.IsDisposed)
                    _widgetLauncherDock.Dispose();
                _widgetLauncherDock = null;

                WriteDashboardLog("Dashboard application context cleanup completed.");
            }

            UnregisterExitHandlers();
            base.ExitThreadCore();
        }
    }
}
