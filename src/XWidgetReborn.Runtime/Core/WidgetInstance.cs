using System;
using System.Runtime.InteropServices;
using System.Threading;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Shared;
using XWidgetReborn.Runtime.Compatibility;

namespace XWidgetReborn.Runtime.Core
{
    internal sealed class WidgetInstance : IDisposable
    {
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpShowWindow = 0x0040;
        private const int SwRestore = 9;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private readonly IWidget _widget;
        private readonly EngineScheduler _scheduler;
        private readonly IRuntimeSettingsService _settings;
        private readonly IRuntimeLoggingService _logging;
        private readonly SynchronizationContext _synchronizationContext;
        private readonly IRuntimeContext _runtimeContext;
        private WidgetWindow _window;
        private Timer _legacyVisibilityTimer;
        private IDisposable _subscription;
        private bool _started;
        private bool _disposed;

        public event EventHandler Closed;
        public event EventHandler UserCloseRequested;
        public event EventHandler SuspendRequested;
        public event EventHandler ResumeRequested;
        public string Id { get { return _widget.Id; } }
        public bool HasConfirmedRunningState
        {
            get
            {
                LegacyExternalWidget legacy =
                    _widget as LegacyExternalWidget;
                return legacy == null || legacy.HasVisibleSurface;
            }
        }

        public WidgetInstance(
            IWidget widget,
            EngineScheduler scheduler,
            IRuntimeSettingsService settings,
            IRuntimeLoggingService logging,
            IRuntimeContext runtimeContext)
        {
            if (widget == null) throw new ArgumentNullException("widget");
            if (scheduler == null) throw new ArgumentNullException("scheduler");
            if (settings == null) throw new ArgumentNullException("settings");
            if (logging == null) throw new ArgumentNullException("logging");
            if (runtimeContext == null) throw new ArgumentNullException("runtimeContext");
            _widget = widget;
            _scheduler = scheduler;
            _settings = settings;
            _logging = logging;
            _runtimeContext = runtimeContext;
            _synchronizationContext = SynchronizationContext.Current;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            try
            {
                IRuntimeAwareWidget awareWidget = _widget as IRuntimeAwareWidget;
                if (awareWidget != null)
                {
                    awareWidget.AttachRuntime(_runtimeContext);
                    _logging.Debug("Runtime services attached to widget: " + Id + ".");
                }
                _logging.Information("Loading native widget " + Id);
                if (_widget is LegacyExternalWidget)
                {
                    _widget.Start(delegate { });
                    StartLegacyVisibilityWatcher(
                        (LegacyExternalWidget)_widget);
                    _logging.Information(
                        "Legacy widget launched through original XWidget: " +
                        Id);
                    return;
                }
                _window = new WidgetWindow(_widget, _settings, _logging);
                _widget.Start(delegate
                {
                    if (_window != null &&
                        !_window.IsDisposed)
                        _window.RequestRender();
                });
                _subscription = _scheduler.Subscribe(_widget.UpdateRate, _widget.Tick);
                _window.PauseRequested += delegate
                {
                    EventHandler handler = SuspendRequested;
                    if (handler != null) handler(this, EventArgs.Empty);
                };
                _window.ResumeRequested += delegate
                {
                    EventHandler handler = ResumeRequested;
                    if (handler != null) handler(this, EventArgs.Empty);
                };
                _window.UserCloseRequested += delegate { if (UserCloseRequested != null) UserCloseRequested(this, EventArgs.Empty); };
                _window.FormClosed += delegate(object sender, System.Windows.Forms.FormClosedEventArgs e)
                {
                    _logging.Debug("Widget window FormClosed: " + Id + "; reason=" + e.CloseReason + ".");
                    ReleaseResources();
                    if (Closed != null) Closed(this, EventArgs.Empty);
                };
                _window.ShowWithPreparedLayeredFrame();
                _logging.Information("Widget running through public contract: " + Id);
            }
            catch (Exception ex)
            {
                _logging.Error("Widget start failed: " + Id + ".", ex);
                Dispose();
                throw;
            }
        }

        public void SetLayerMode(WidgetLayerMode mode)
        {
            if (_disposed) return;
            LegacyExternalWidget legacy = _widget as LegacyExternalWidget;
            if (legacy != null)
            {
                legacy.SetLayerMode(mode);
                return;
            }
            if (_window != null && !_window.IsDisposed)
                _window.SetLayerMode(mode);
        }

        public void BringAboveXWidgetReborn(IntPtr rebornWindow)
        {
            if (_disposed) return;
            LegacyExternalWidget legacy = _widget as LegacyExternalWidget;
            if (legacy != null)
            {
                legacy.BringAboveXWidgetReborn(rebornWindow);
                return;
            }
            if (_window != null && !_window.IsDisposed)
                _window.BringAboveXWidgetReborn(rebornWindow);
        }

        public void Activate()
        {
            if (_disposed) return;
            var legacy = _widget as LegacyExternalWidget;
            if (legacy != null)
            {
                legacy.Activate();
                _logging.Information(
                    "Legacy widget show command sent: " + Id + ".");
                return;
            }
            if (_window != null)
                _window.PrepareForActivation();
            BringWindowForward();
        }

        private void BringWindowForward()
        {
            if (_window == null || _window.IsDisposed) return;

            // A widget is hosted by the Engine, while the Dashboard is a different
            // foreground process. Windows may reject Activate/BringToFront across
            // process boundaries. Briefly promoting the window to topmost and then
            // immediately restoring normal Z-order reliably places it above the
            // Dashboard without leaving the widget permanently topmost.
            IntPtr handle = _window.Handle;
            uint flags = SwpNoMove | SwpNoSize | SwpShowWindow;
            ShowWindow(handle, SwRestore);
            SetWindowPos(handle, HwndTopMost, 0, 0, 0, 0, flags);
            SetWindowPos(handle, HwndNoTopMost, 0, 0, 0, 0, flags);
            bool foreground = SetForegroundWindow(handle);
            _logging.Debug("Widget foreground request: " + Id + "; SetForegroundWindow=" + foreground + ".");
        }

        private void StartLegacyVisibilityWatcher(
            LegacyExternalWidget legacy)
        {
            _legacyVisibilityTimer = new Timer(
                delegate
                {
                    if (_disposed || legacy.HasVisibleSurface)
                        return;

                    if (_synchronizationContext != null)
                        _synchronizationContext.Post(
                            delegate { HandleLegacySurfaceClosed(); },
                            null);
                    else
                        HandleLegacySurfaceClosed();
                },
                null,
                500,
                500);
        }

        private void HandleLegacySurfaceClosed()
        {
            if (_disposed)
                return;

            if (_legacyVisibilityTimer != null)
            {
                _legacyVisibilityTimer.Change(
                    Timeout.Infinite,
                    Timeout.Infinite);
            }
            _logging.Information(
                "Legacy widget surface was closed by the user: " +
                Id + ".");
            EventHandler closeRequested = UserCloseRequested;
            if (closeRequested != null)
                closeRequested(this, EventArgs.Empty);
            ReleaseResources();
            EventHandler closed = Closed;
            if (closed != null)
                closed(this, EventArgs.Empty);
        }

        public void Suspend()
        {
            if (_disposed) return;
            _widget.Pause();
            _logging.Information("Widget paused: " + Id);
        }

        public void Resume()
        {
            if (_disposed) return;
            _widget.Resume();
            _logging.Information("Widget resumed: " + Id);
        }


        internal bool IsLegacyExternal
        {
            get { return _widget is LegacyExternalWidget; }
        }

        internal IntPtr LayerSurfaceHandle
        {
            get
            {
                LegacyExternalWidget legacy =
                    _widget as LegacyExternalWidget;
                if (legacy != null)
                    return legacy.SurfaceHandle;
                return _window != null && !_window.IsDisposed &&
                    _window.IsHandleCreated && _window.Visible
                    ? _window.Handle
                    : IntPtr.Zero;
            }
        }


        internal WidgetLayerMode LayerMode
        {
            get
            {
                LegacyExternalWidget legacy = _widget as LegacyExternalWidget;
                return legacy != null
                    ? EngineClient.GetWidgetLayerMode()
                    : (_window == null || _window.IsDisposed
                        ? WidgetLayerMode.NormalDesktop
                        : _window.LayerMode);
            }
        }

        internal void DetachLegacyForHostShutdown()
        {
            if (_disposed) return;
            LegacyExternalWidget legacy = _widget as LegacyExternalWidget;
            if (legacy == null)
            {
                Dispose();
                return;
            }

            _logging.Information(
                "Detaching legacy widget from Reborn shutdown: " + Id + ".");
            legacy.Detach();
            ReleaseResources(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _logging.Debug("WidgetInstance.Dispose entered: " + Id + "; windowExists=" + (_window != null) + "; windowDisposed=" + (_window != null && _window.IsDisposed) + ".");
            if (_window != null && !_window.IsDisposed)
            {
                _logging.Debug("WidgetInstance closing window: " + Id + ".");
                _window.Close();
            }
            ReleaseResources();
            _logging.Debug("WidgetInstance.Dispose completed: " + Id + ".");
        }

        private void ReleaseResources(bool disposeWidget = true)
        {
            if (_disposed) return;
            _disposed = true;
            if (_subscription != null) _subscription.Dispose();
            _subscription = null;
            if (_legacyVisibilityTimer != null)
            {
                _legacyVisibilityTimer.Change(
                    Timeout.Infinite,
                    Timeout.Infinite);
                _legacyVisibilityTimer.Dispose();
                _legacyVisibilityTimer = null;
            }
            if (disposeWidget)
                _widget.Dispose();
        }
    }
}
