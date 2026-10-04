using System;
using System.Drawing;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Shared;

namespace XWidgetReborn.Runtime.Core
{
    internal sealed class WidgetWindow : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid settingGuid, uint flags);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern int GetWindowLong32(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern int SetWindowLong32(IntPtr window, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(
            IntPtr window,
            IntPtr destinationDc,
            ref NativePoint destination,
            ref NativeSize size,
            IntPtr sourceDc,
            ref NativePoint source,
            int colorKey,
            ref BlendFunction blend,
            int flags);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);

        private const int GwlExStyle = -20;
        private const long WsExLayered = 0x00080000L;
        private const long WsExTransparent = 0x00000020L;
        private const byte AcSrcOver = 0;
        private const byte AcSrcAlpha = 1;
        private const int UlwAlpha = 2;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonUp = 0x0202;
        private const int WmMove = 0x0003;
        private const int WmWindowPosChanging = 0x0046;
        private const int WmWindowPosChanged = 0x0047;
        private const int WmGetMinMaxInfo = 0x0024;
        private const int WmMoving = 0x0216;
        private const int WmEnterSizeMove = 0x0231;
        private const int WmExitSizeMove = 0x0232;
        private const int WmDisplayChange = 0x007E;
        private const int WmPowerBroadcast = 0x0218;
        private const int PbtApmResumeSuspend = 0x0007;
        private const int PbtApmResumeAutomatic = 0x0012;
        private const int PbtPowerSettingChange = 0x8013;
        private static readonly Guid MonitorPowerOnSetting =
            new Guid("02731015-4510-4526-99e6-e5a17ebd1aea");
        private static readonly Guid SessionDisplayStatusSetting =
            new Guid("2b84c20e-ad23-4ddf-93db-05ffbd7efca5");

        private readonly IWidget _widget;
        private readonly IRuntimeSettingsService _settings;
        private readonly IRuntimeLoggingService _logging;
        private bool _paused;
        private Size _preferredSize;
        private float _scale = 1F;
        private WidgetWindowShape _windowShape =
            WidgetWindowShape.Rectangle;
        private bool _perPixelAlpha;
        private bool _layeredPresentationPending;
        private bool _initialLayeredFramePrimed;
        private double _widgetOpacity = 1D;
        private bool _fullOpacityOnHover;
        private bool _pointerOverWidget;
        private float _dpiScale = 1F;
        private Bitmap _layeredBitmap;
        private readonly IWidgetPointerInput
            _pointerInput;
        private readonly IWidgetDoubleClickInput
            _doubleClickInput;
        private readonly IWidgetFileDropTarget
            _fileDropTarget;
        private readonly IWidgetInputRegionProvider
            _inputRegionProvider;
        private readonly WidgetHostContext _hostContext;
        private bool _pointerCapturedByWidget;
        private Control _pointerCaptureControl;
        private Rectangle _activeHotspotBounds = Rectangle.Empty;
        private bool _rightAnchoredComposite;
        private int _rightAnchoredMainWidth;
        private bool _fixedCompositionSurface;
        private Rectangle _compositionParentBounds;
        private bool _suppressLayeredPresentation;
        private int _layeredPresentationCount;
        private int _anchoredCompositeResizeCount;
        private bool _positionLocked;
        private bool _snapToEdges;
        private bool _keepOnScreen;
        private bool _clickThrough;
        private WidgetLayerMode _layerMode;
        private Control _interactionSurface;
        private DateTime _lastDesktopClick = DateTime.MinValue;
        private Point _lastDesktopClickPoint;
        private Stopwatch _dragDiagnosticTimer;
        private int _dragMovingMessages;
        private int _dragMoveMessages;
        private int _dragWindowPosChangingMessages;
        private int _dragWindowPosChangedMessages;
        private bool _dragInProgress;
        private bool _pointerWindowDragActive;
        private Control _pointerWindowDragCapture;
        private Stopwatch _pointerWindowDragTimer;
        private int _pointerWindowDragMoves;
        private Point _dragStartPointer;
        private Point _dragStartHostLocation;
        private bool _natureWeatherDragActive;
        private bool _dragStartedSnappedLeft;
        private bool _dragStartedSnappedRight;
        private bool _dragStartedSnappedTop;
        private bool _dragStartedSnappedBottom;
        private bool _dragDetachedFromLeft;
        private bool _dragDetachedFromRight;
        private bool _dragDetachedFromTop;
        private bool _dragDetachedFromBottom;
        // A Weather skin can use a composition surface wider than the monitor
        // so its slide-out panel has room to animate.  Windows constrains that
        // transparent host before its visible card reaches the edge.  Record
        // the user's actual drag intent instead of inferring it from the host.
        private bool _dragReachedRightScreenEdge;
        // Windows constrains the transparent Weather composition host to the
        // monitor work-area top during a user drag.  Record that exact native
        // clamp so, after the drag ends, the visible card can be restored to
        // the top edge without turning the manual-placement path into a
        // snapping rule.
        private bool _dragEncounteredNativeTopClamp;
        private bool _reportedFixedCompositionTrackSize;
        private IntPtr _monitorPowerNotification;
        private IntPtr _sessionDisplayNotification;
        private System.Windows.Forms.Timer _placementRestoreTimer;
        private int _placementRestorePass;
        private string _placementRestoreReason = string.Empty;
        private bool _displayEventsSubscribed;
        // Weather configures its wider composition before this layered window
        // is shown for the first time.  Reapply it once after the first show:
        // that is the same fully-initialised native sizing path used by a
        // later theme change.
        private bool _initialFixedCompositionFinalized;
        private readonly List<HotspotWindow> _hotspotWindows = new List<HotspotWindow>();
        private const bool HotspotDiagnosticsEnabled = false;
        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;

        public event EventHandler PauseRequested;
        public event EventHandler ResumeRequested;
        public event EventHandler UserCloseRequested;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // The host must be a layered HWND from the instant Windows
                // creates it. Converting an already-created ordinary Form in
                // OnHandleCreated leaves a temporary opaque redirection
                // surface, which is the dark startup rectangle.
                if (_perPixelAlpha ||
                    _windowShape != WidgetWindowShape.Rectangle)
                    parameters.ExStyle |= unchecked((int)WsExLayered);
                return parameters;
            }
        }

        // Render and commit the first ARGB frame while the HWND is hidden,
        // then expose the already-composited layered window.
        internal void ShowWithPreparedLayeredFrame()
        {
            if (_layerMode == WidgetLayerMode.Hidden)
                return;

            if (!IsHandleCreated)
            {
                // Accessing Handle forces Form handle creation without showing
                // the window. CreateControl alone is not sufficient for a
                // top-level Form on every WinForms/Windows combination.
                IntPtr unused = Handle;
            }
            if (!_perPixelAlpha ||
                ClientSize.Width < 1 ||
                ClientSize.Height < 1)
            {
                Show();
                return;
            }

            EnsureLayeredBitmap();
            using (Graphics graphics = Graphics.FromImage(_layeredBitmap))
            {
                graphics.Clear(Color.Transparent);
                _widget.Render(
                    graphics,
                    new Rectangle(
                        0,
                        0,
                        ClientSize.Width - 1,
                        ClientSize.Height - 1));
            }
            _initialLayeredFramePrimed = true;
            PresentLayeredSurface(true, true);
            Show();
        }

        public WidgetWindow(
            IWidget widget,
            IRuntimeSettingsService settings,
            IRuntimeLoggingService logging)
        {
            if (widget == null) throw new ArgumentNullException("widget");
            if (settings == null) throw new ArgumentNullException("settings");
            if (logging == null) throw new ArgumentNullException("logging");
            _widget = widget;
            _pointerInput =
                widget as IWidgetPointerInput;
            _doubleClickInput =
                widget as IWidgetDoubleClickInput;
            _fileDropTarget =
                widget as IWidgetFileDropTarget;
            _inputRegionProvider =
                widget as IWidgetInputRegionProvider;
            _settings = settings;
            _logging = logging;
            Text = widget.Name;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = _settings.GetWidgetPosition(widget.Id, widget.DefaultLocation);
            _preferredSize = widget.DefaultSize;
            ApplyScaledSize();
            BackColor = Color.FromArgb(24, 31, 41);
            bool legacyTopMost = _settings.IsWidgetTopMost(widget.Id, false);
            _layerMode = ReadLayerMode(legacyTopMost);
            // Do not call ApplyLayerMode here. It calls Show() for every
            // non-hidden widget, exposing this unrendered Form before the
            // widget is started and its first ARGB frame is committed. The
            // host deliberately shows the window only after first render.
            TopMost = _layerMode == WidgetLayerMode.AlwaysOnTop;
            _positionLocked = ReadBooleanSetting(
                "host.lockPosition",
                false);
            _snapToEdges = ReadBooleanSetting("host.snapToEdges", true);
            _keepOnScreen = ReadBooleanSetting("host.keepOnScreen", true);
            _clickThrough = ReadBooleanSetting(
                "host.clickThrough",
                false);
            _fullOpacityOnHover = ReadBooleanSetting(
                "host.fullOpacityOnHover", false);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            MouseDown += HandleMouseDown;
            MouseMove += HandleMouseMove;
            MouseUp += HandleMouseUp;
            MouseEnter += HandleOpacityMouseEnter;
            if (_fileDropTarget != null)
            {
                AllowDrop = true;
                DragEnter += HandleFileDragEnter;
                DragDrop += HandleFileDrop;
            }
            MouseLeave += delegate
            {
                if (_pointerInput != null)
                    _pointerInput.PointerLeave();
                HandleOpacityMouseLeave();
            };
            FormClosed += delegate
            {
                SaveWidgetPosition();
                DisposeHotspotWindows();
            };
            LocationChanged += delegate { UpdateHotspotWindows(); };
            SizeChanged += delegate { UpdateHotspotWindows(); };
            VisibleChanged += delegate { UpdateHotspotWindows(); };
            MouseCaptureChanged += HandleWindowDragCaptureChanged;

            try
            {
                SystemEvents.DisplaySettingsChanged +=
                    DisplaySettingsChanged;
                _displayEventsSubscribed = true;
            }
            catch (Exception ex)
            {
                // WM_DISPLAYCHANGE and the registered monitor-power message
                // remain available if SystemEvents cannot create its helper
                // window in a restricted Windows session.
                _logging.Warning(
                    "Display settings notification is unavailable for " +
                    _widget.Id + ": " + ex.Message);
            }

            IOfficialWidget official =
                _widget as IOfficialWidget;
            _hostContext = null;
            try
            {
                if (official != null)
                {
                    _hostContext = new WidgetHostContext(this);
                    official.AttachHost(_hostContext);
                }
                ContextMenuStrip = BuildContextMenu(official);
            }
            catch
            {
                // A Form can create its native handle while an official widget
                // attaches its host. If attachment or initial rendering fails,
                // dispose that partially constructed HWND here because the
                // caller has not yet received a WidgetWindow reference.
                DisposeHotspotWindows();
                Dispose();
                throw;
            }
            _logging.Debug("WidgetWindow constructed: " + _widget.Id + ".");
        }


        private void CreateInteractionSurface()
        {
            if (_pointerInput == null &&
                _doubleClickInput == null &&
                _fileDropTarget == null)
                return;

            // Per-pixel layered forms do not reliably receive normal WinForms
            // mouse events on every Windows configuration. A transparent child
            // HWND gives interactive widgets a stable input surface while the
            // parent continues to own all rendering through UpdateLayeredWindow.
            var surface = new PointerInputSurface(
                delegate(Point point)
                {
                    return _inputRegionProvider == null ||
                        _inputRegionProvider.AcceptsInput(point, ClientSize);
                })
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                TabStop = false,
                ContextMenuStrip = ContextMenuStrip
            };
            surface.MouseDown += HandleMouseDown;
            surface.MouseMove += HandleMouseMove;
            surface.MouseCaptureChanged += HandleWindowDragCaptureChanged;
            surface.MouseUp += HandleMouseUp;
            surface.MouseEnter += HandleOpacityMouseEnter;
            if (_fileDropTarget != null)
            {
                surface.AllowDrop = true;
                surface.DragEnter += HandleFileDragEnter;
                surface.DragDrop += HandleFileDrop;
            }
            surface.MouseLeave += delegate
            {
                if (_pointerInput != null)
                    _pointerInput.PointerLeave();
                HandleOpacityMouseLeave();
            };
            Controls.Add(surface);
            surface.BringToFront();
            _interactionSurface = surface;
            _logging.Debug(
                "Interaction surface attached: " +
                _widget.Id + ".");
        }

        private void CreateHotspotWindows()
        {
            // Interactive regions are now hit-tested inside the main widget
            // window before drag handling. Separate transparent/owned windows
            // proved unreliable across Windows layered-window configurations
            // and could become visible or escape the widget Z-order.
            DisposeHotspotWindows();
        }

        private void DisposeHotspotWindows()
        {
            foreach (HotspotWindow window in _hotspotWindows)
            {
                try
                {
                    window.MouseDown -= HandleHotspotMouseDown;
                    window.MouseMove -= HandleHotspotMouseMove;
                    window.MouseUp -= HandleHotspotMouseUp;
                    window.Close();
                    window.Dispose();
                }
                catch { }
            }
            _hotspotWindows.Clear();
        }

        private void UpdateHotspotWindows()
        {
            // Kept as a no-op for compatibility with existing render/update
            // calls. Hotspots are handled directly by the main widget HWND.
        }

        private bool TryGetHotspotAt(Point location, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            var provider = _widget as IWidgetHotspotProvider;
            if (provider == null || _clickThrough)
                return false;

            try
            {
                IEnumerable<WidgetHotspot> hotspots = provider.GetHotspots(ClientSize);
                if (hotspots == null)
                    return false;
                foreach (WidgetHotspot hotspot in hotspots)
                {
                    if (hotspot != null && hotspot.Bounds.Contains(location))
                    {
                        bounds = hotspot.Bounds;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logging.Debug("Main-window hotspot hit test failed: " + _widget.Id + "; " + ex.Message);
            }
            return false;
        }

        private static Point HotspotCenter(Rectangle bounds)
        {
            return new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        }

        private Point ParentPointFromHotspot(object sender, Point local)
        {
            var window = sender as HotspotWindow;
            if (window == null)
                return local;
            return new Point(window.Hotspot.Bounds.X + local.X, window.Hotspot.Bounds.Y + local.Y);
        }

        private void HandleHotspotMouseDown(object sender, MouseEventArgs e)
        {
            var diagnosticWindow = sender as HotspotWindow;
            _logging.Warning(
                "HOTSPOT DIAGNOSTIC MouseDown: widget=" + _widget.Id +
                "; id=" + (diagnosticWindow == null || diagnosticWindow.Hotspot == null ? "?" : diagnosticWindow.Hotspot.Id) +
                "; local=" + e.X + "," + e.Y +
                "; button=" + e.Button + ".");
            if (_pointerInput == null)
                return;
            Point point = ParentPointFromHotspot(sender, e.Location);
            if (_pointerInput.PointerDown(point, PointerButton(e.Button)))
            {
                _pointerCapturedByWidget = true;
                var window = sender as HotspotWindow;
                if (window != null)
                    window.Capture = true;
            }
        }

        private void HandleHotspotMouseMove(object sender, MouseEventArgs e)
        {
            if (_pointerInput != null)
                _pointerInput.PointerMove(ParentPointFromHotspot(sender, e.Location));
        }

        private void HandleHotspotMouseUp(object sender, MouseEventArgs e)
        {
            var diagnosticWindow = sender as HotspotWindow;
            _logging.Warning(
                "HOTSPOT DIAGNOSTIC MouseUp: widget=" + _widget.Id +
                "; id=" + (diagnosticWindow == null || diagnosticWindow.Hotspot == null ? "?" : diagnosticWindow.Hotspot.Id) +
                "; local=" + e.X + "," + e.Y +
                "; button=" + e.Button + ".");
            if (_pointerInput == null)
                return;
            Point point = ParentPointFromHotspot(sender, e.Location);
            if (_pointerCapturedByWidget)
            {
                _pointerCapturedByWidget = false;
                var window = sender as HotspotWindow;
                if (window != null)
                    window.Capture = false;
                _pointerInput.PointerUp(point, PointerButton(e.Button));
            }
        }

        private ContextMenuStrip BuildContextMenu(
            IOfficialWidget official)
        {
            var menu = new ContextMenuStrip();
            bool weatherMenu = official != null && string.Equals(
                _widget.Id, "native.weather",
                StringComparison.OrdinalIgnoreCase);
            ToolStripMenuItem widgetSettings = weatherMenu
                ? new ToolStripMenuItem("Widget Settings") : null;
            ToolStripMenuItem weatherSettings = weatherMenu
                ? new ToolStripMenuItem("Weather Settings") : null;
            ToolStripItemCollection widgetItems = weatherMenu
                ? widgetSettings.DropDownItems : menu.Items;
            if (weatherMenu)
            {
                menu.Items.Add(widgetSettings);
                menu.Items.Add(weatherSettings);
            }

            var pause = widgetItems.Add("Pause widget");
            pause.Click += delegate
            {
                _paused = !_paused;
                pause.Text = _paused
                    ? "Resume widget"
                    : "Pause widget";
                if (_paused)
                {
                    _widget.Pause();
                    if (PauseRequested != null)
                        PauseRequested(
                            this,
                            EventArgs.Empty);
                }
                else
                {
                    _widget.Resume();
                    if (ResumeRequested != null)
                        ResumeRequested(
                            this,
                            EventArgs.Empty);
                }
                RequestRender();
            };

            if (official != null)
            {
                widgetItems.Add(BuildScaleMenu());
                widgetItems.Add(BuildOpacityMenu());
            }


            widgetItems.Add(BuildPositionMenu());

            var clickThrough =
                new ToolStripMenuItem(
                    "Click Through")
                {
                    Checked = _clickThrough,
                    CheckOnClick = false,
                    ToolTipText =
                        "Select this widget in the Gallery to restore mouse interaction."
                };
            clickThrough.Click += delegate
            {
                _clickThrough = !_clickThrough;
                clickThrough.Checked = _clickThrough;
                WriteBooleanSetting(
                    "host.clickThrough",
                    _clickThrough);
                BeginInvoke((Action)ApplyClickThroughStyle);
            };
            widgetItems.Add(clickThrough);

            IWidgetThemeProvider themeProvider =
                _widget as IWidgetThemeProvider;
            IWidgetStyleProvider styleProvider =
                _widget as IWidgetStyleProvider;
            if (official != null &&
                (themeProvider != null ||
                    styleProvider != null))
                widgetItems.Add(
                    BuildAppearanceMenu(
                        themeProvider,
                        styleProvider));

            if (official != null)
            {
                var officialMenuItems =
                    new List<KeyValuePair<
                        ToolStripMenuItem,
                        WidgetMenuCommand>>();
                ToolStripMenuItem weatherWebPages = null;
                ToolStripMenuItem weatherProviders = null;
                if (weatherMenu)
                {
                    weatherSettings.DropDownItems.Add(
                        "Change Location...",
                        null,
                        delegate
                        {
                            official.ShowSettings();
                            RequestRender();
                        });
                    weatherSettings.DropDownItems.Add(
                        new ToolStripSeparator());
                }
                else
                    menu.Items.Add(new ToolStripSeparator());
                foreach (WidgetMenuCommand command in
                    official.GetMenuCommands())
                {
                    WidgetMenuCommand captured = command;
                    bool weatherWebLink = weatherMenu &&
                        IsWeatherWebPageCommand(captured.Text);
                    bool weatherProviderLink = weatherMenu &&
                        IsWeatherProviderCommand(captured.Text);
                    var item = new ToolStripMenuItem(
                        weatherWebLink
                            ? WeatherWebPageLabel(captured.Text)
                            : weatherProviderLink
                                ? WeatherProviderLabel(captured.Text)
                            : captured.Text)
                    {
                        Checked = captured.IsChecked
                    };
                    item.Click += delegate
                        {
                            captured.Execute();
                        RefreshOfficialMenuItems(
                            officialMenuItems);
                        RequestRender();
                        };
                    // Presentation and animation affect the widget itself;
                    // forecast, units, and location control weather data.
                    if (weatherWebLink)
                    {
                        if (weatherWebPages == null)
                        {
                            weatherWebPages = new ToolStripMenuItem(
                                "Open Weather Web Page");
                            weatherSettings.DropDownItems.Add(
                                weatherWebPages);
                        }
                        weatherWebPages.DropDownItems.Add(item);
                    }
                    else if (weatherProviderLink)
                    {
                        if (weatherProviders == null)
                        {
                            weatherProviders = new ToolStripMenuItem(
                                "Weather Providers");
                            weatherSettings.DropDownItems.Add(
                                weatherProviders);
                        }
                        weatherProviders.DropDownItems.Add(item);
                    }
                    else if (weatherMenu && IsWeatherWidgetSetting(
                        captured.Text))
                        widgetItems.Add(item);
                    else if (weatherMenu)
                        weatherSettings.DropDownItems.Add(item);
                    else
                        menu.Items.Add(item);
                    officialMenuItems.Add(
                        new KeyValuePair<
                            ToolStripMenuItem,
                            WidgetMenuCommand>(
                                item,
                                captured));
                }
                menu.Opening += delegate
                {
                    RefreshOfficialMenuItems(
                        officialMenuItems);
                    RefreshThemeMenu(
                        menu,
                        themeProvider);
                    RefreshStyleMenu(
                        menu,
                        styleProvider);
                };
                if (weatherMenu)
                {
                    weatherSettings.DropDownItems.Add(
                        new ToolStripSeparator());
                    weatherSettings.DropDownItems.Add(
                        "Reset Weather Settings",
                        null,
                        delegate
                        {
                            official.ResetSettings();
                            RequestRender();
                        });
                    widgetItems.Add(new ToolStripSeparator());
                    widgetItems.Add(
                        "Reload Widget",
                        null,
                        delegate
                        {
                            _widget.Pause();
                            _widget.Resume();
                            RequestRender();
                        });
                }
                else
                {
                    menu.Items.Add(
                        new ToolStripSeparator());
                    menu.Items.Add(
                        "Widget Settings...",
                        null,
                        delegate
                        {
                            official.ShowSettings();
                            RequestRender();
                        });
                    menu.Items.Add(
                        "Reload Widget",
                        null,
                        delegate
                        {
                            _widget.Pause();
                            _widget.Resume();
                            RequestRender();
                        });
                    menu.Items.Add(
                        "Reset Settings",
                        null,
                        delegate
                        {
                            official.ResetSettings();
                            RequestRender();
                        });
                }
            }

            if (IsNativeWidgetFolder(_widget.Id))
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(
                    "Open Folder Location",
                    null,
                    delegate { OpenWidgetAssetFolder(); });
                if (string.Equals(_widget.Id, "native.weather",
                    StringComparison.OrdinalIgnoreCase))
                    menu.Items.Add(
                        "Open Weather Icons Folder",
                        null,
                        delegate { OpenWeatherIconsFolder(); });
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(
                "Close Widget",
                null,
                delegate
                {
                    _logging.Debug(
                        "Close-widget menu selected: " +
                        _widget.Id + ".");
                    if (UserCloseRequested != null)
                        UserCloseRequested(
                            this,
                            EventArgs.Empty);
                    Close();
                });
            return menu;
        }

        private static bool IsNativeWidgetFolder(string widgetId)
        {
            return string.Equals(widgetId, "native.weather",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(widgetId, "native.clock",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(widgetId, "native.calendar",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(widgetId, "native.recyclebin",
                    StringComparison.OrdinalIgnoreCase);
        }

        private void OpenWidgetAssetFolder()
        {
            string folder = ResolveActiveThemeAssetFolder();
            SeedWidgetAssetFolder(folder);
            OpenFolder(folder);
        }

        private void SeedWidgetAssetFolder(string destination)
        {
            try
            {
                string source = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets", "Themes", ActiveThemeFolderName());
                if (Directory.Exists(source))
                {
                    string widgetName = string.Equals(_widget.Id,
                        "native.clock", StringComparison.OrdinalIgnoreCase)
                        ? "clock" : string.Equals(_widget.Id,
                        "native.calendar", StringComparison.OrdinalIgnoreCase)
                        ? "calendar" : "weather";
                    if (string.Equals(_widget.Id, "native.recyclebin",
                        StringComparison.OrdinalIgnoreCase))
                        widgetName = "recycle-bin";
                    foreach (string file in Directory.GetFiles(source, "*.png"))
                    {
                        string name = Path.GetFileName(file);
                        string lower = name.ToLowerInvariant();
                        bool belongs = lower.Contains(widgetName) ||
                            (widgetName == "clock" &&
                             (lower.Contains("hour-hand") ||
                              lower.Contains("minute-hand") ||
                              lower.Contains("second-hand")));
                        if (!belongs) continue;
                        string copy = Path.Combine(destination, name);
                        if (!File.Exists(copy)) File.Copy(file, copy);
                    }
                }

                string readme = Path.Combine(destination, "README.txt");
                if (!File.Exists(readme))
                    File.WriteAllText(readme,
                        "EmilyDesk custom widget images\r\n\r\n" +
                        "Place your PNG files in this folder. In EmilyDesk " +
                        "Designer, select the background or image layer, " +
                        "choose the replacement PNG, save, and reload the " +
                        "widget.\r\n\r\n" +
                        "Clock hour, minute, and second hands may use custom " +
                        "transparent PNG files. The image should point upward " +
                        "with its rotation point at the bottom centre.\r\n");
            }
            catch { }
        }

        private void OpenWeatherIconsFolder()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "WeatherIconPacks", ActiveThemeFolderName());
            if (!Directory.Exists(folder))
                folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Assets", "WeatherIconPacks");
            OpenFolder(folder);
        }

        private string ResolveActiveThemeAssetFolder()
        {
            string widgetFolder = string.Equals(_widget.Id, "native.clock",
                StringComparison.OrdinalIgnoreCase) ? "Clock" :
                string.Equals(_widget.Id, "native.calendar",
                StringComparison.OrdinalIgnoreCase) ? "Calendar" :
                string.Equals(_widget.Id, "native.recyclebin",
                StringComparison.OrdinalIgnoreCase) ? "RecycleBin" : "Weather";
            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerAssets", widgetFolder,
                ActiveThemeFolderName());
            Directory.CreateDirectory(folder);
            return folder;
        }

        private string ActiveThemeFolderName()
        {
            IWidgetThemeProvider provider = _widget as IWidgetThemeProvider;
            string theme = provider == null ? string.Empty : provider.Theme;
            if (string.Equals(theme, "Art Deco",
                StringComparison.OrdinalIgnoreCase)) return "ArtDeco";
            if (string.Equals(theme, "Woodland Nature",
                StringComparison.OrdinalIgnoreCase)) return "WoodlandNature";
            if (string.Equals(theme, "Botanical Nature",
                StringComparison.OrdinalIgnoreCase)) return "BotanicalNature";
            if (string.Equals(theme, "Industrial",
                StringComparison.OrdinalIgnoreCase)) return "Industrial";
            if (string.Equals(theme, "Steampunk",
                StringComparison.OrdinalIgnoreCase)) return "Steampunk";
            if (string.Equals(theme, "Vintage",
                StringComparison.OrdinalIgnoreCase)) return "Vintage";
            return "Modern";
        }

        private static void OpenFolder(string folder)
        {
            if (!Directory.Exists(folder)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static bool IsWeatherWidgetSetting(string commandText)
        {
            return string.Equals(commandText, "Edit in Designer",
                StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(commandText) &&
                commandText.StartsWith("Animation - ",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsWeatherWebPageCommand(string commandText)
        {
            return !string.IsNullOrEmpty(commandText) &&
                commandText.StartsWith("Open Weather: ",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string WeatherWebPageLabel(string commandText)
        {
            return commandText.Substring("Open Weather: ".Length);
        }

        private static bool IsWeatherProviderCommand(string commandText)
        {
            return !string.IsNullOrEmpty(commandText) &&
                commandText.StartsWith("Weather Provider: ",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string WeatherProviderLabel(string commandText)
        {
            return commandText.Substring("Weather Provider: ".Length);
        }

        private ToolStripMenuItem BuildPositionMenu()
        {
            var menu = new ToolStripMenuItem("Position");
            AddPositionToggle(menu, "Lock Position", delegate
            { return _positionLocked; }, delegate(bool value)
            {
                _positionLocked = value;
                WriteBooleanSetting("host.lockPosition", value);
            });
            AddPositionToggle(menu, "Snap to Screen Edges", delegate
            { return _snapToEdges; }, delegate(bool value)
            {
                _snapToEdges = value;
                WriteBooleanSetting("host.snapToEdges", value);
                if (value)
                {
                    SnapVisibleCompositionAfterMove(true);
                    SaveWidgetPosition();
                }
            });
            AddPositionToggle(menu, "Keep on Screen", delegate
            { return _keepOnScreen; }, delegate(bool value)
            {
                _keepOnScreen = value;
                WriteBooleanSetting("host.keepOnScreen", value);
            });
            AddPositionToggle(menu, "Always on Top", delegate
            { return _layerMode == WidgetLayerMode.AlwaysOnTop; },
                delegate(bool value)
                {
                    SetLayerMode(value ? WidgetLayerMode.AlwaysOnTop
                        : WidgetLayerMode.NormalDesktop);
                });
            return menu;
        }

        private static void AddPositionToggle(ToolStripMenuItem menu,
            string text, Func<bool> read, Action<bool> write)
        {
            var item = new ToolStripMenuItem(text)
            {
                Checked = read(),
                CheckOnClick = false
            };
            item.Click += delegate
            {
                write(!read());
                item.Checked = read();
            };
            menu.DropDownOpening += delegate { item.Checked = read(); };
            menu.DropDownItems.Add(item);
        }


        private ToolStripMenuItem BuildLayerMenu()
        {
            var menu = new ToolStripMenuItem("Layer");
            AddLayerMenuItem(
                menu,
                "Desktop Layer",
                WidgetLayerMode.NormalDesktop);
            AddLayerMenuItem(
                menu,
                "Always on Top",
                WidgetLayerMode.AlwaysOnTop);
            menu.DropDownOpening += delegate
            {
                RefreshLayerMenu(menu);
            };
            RefreshLayerMenu(menu);
            return menu;
        }

        private void AddLayerMenuItem(
            ToolStripMenuItem menu,
            string text,
            WidgetLayerMode mode)
        {
            var item = new ToolStripMenuItem(text)
            {
                Tag = mode,
                CheckOnClick = false
            };
            item.Click += delegate
            {
                SetLayerMode(mode);
                RefreshLayerMenu(menu);
            };
            menu.DropDownItems.Add(item);
        }

        private void RefreshLayerMenu(
            ToolStripMenuItem menu)
        {
            foreach (ToolStripItem child in menu.DropDownItems)
            {
                var item = child as ToolStripMenuItem;
                if (item != null && item.Tag is WidgetLayerMode)
                    item.Checked = (WidgetLayerMode)item.Tag == _layerMode;
            }
        }

        private WidgetLayerMode ReadLayerMode(
            bool legacyTopMost)
        {
            string fallback = legacyTopMost
                ? WidgetLayerMode.AlwaysOnTop.ToString()
                : WidgetLayerMode.NormalDesktop.ToString();
            string value = _settings.GetWidgetSetting(
                _widget.Id,
                "host.layerMode",
                fallback);
            WidgetLayerMode mode;
            if (!Enum.TryParse(value, true, out mode))
                return WidgetLayerMode.NormalDesktop;
            return mode == WidgetLayerMode.AboveXWidgetReborn
                ? WidgetLayerMode.NormalDesktop : mode;
        }

        internal void SetLayerMode(WidgetLayerMode mode)
        {
            _layerMode = mode;
            _settings.SetWidgetSetting(
                _widget.Id,
                "host.layerMode",
                mode.ToString());
            _settings.SetWidgetTopMost(
                _widget.Id,
                mode == WidgetLayerMode.AlwaysOnTop);
            ApplyLayerMode();
            if (mode == WidgetLayerMode.AboveXWidgetReborn)
                BringAboveXWidgetReborn();
        }

        private void ApplyLayerMode()
        {
            TopMost = _layerMode == WidgetLayerMode.AlwaysOnTop;
            if (_layerMode == WidgetLayerMode.Hidden)
                Hide();
            else if (!Visible)
                Show();
        }

        internal WidgetLayerMode LayerMode
        {
            get { return _layerMode; }
        }

        internal void BringAboveXWidgetReborn()
        {
            BringAboveXWidgetReborn(IntPtr.Zero);
        }

        internal void BringAboveXWidgetReborn(IntPtr rebornWindow)
        {
            if (_layerMode != WidgetLayerMode.AboveXWidgetReborn ||
                IsDisposed || !IsHandleCreated)
                return;
            // Raise the widget to the top of the normal (non-topmost) Z-order
            // band. Using the Reborn HWND as hWndInsertAfter placed the widget
            // behind that window because SetWindowPos inserts after/below it.
            // HWND_TOP keeps it above Reborn while normal apps can later cover it.
            uint flags = SwpNoActivate | SwpShowWindow | 0x0001 | 0x0002;
            SetWindowPos(Handle, HwndNoTopMost, 0, 0, 0, 0, flags);
            SetWindowPos(Handle, HwndTop, 0, 0, 0, 0, flags);
        }

        private ToolStripMenuItem BuildScaleMenu()
        {
            var menu = new ToolStripMenuItem("Scale");
            foreach (int percentage in
                new[] { 50, 75, 100, 125, 150, 175, 200 })
            {
                int captured = percentage;
                var item = new ToolStripMenuItem(
                    captured + "%")
                {
                    Tag = captured
                };
                item.Click += delegate
                {
                    _hostContext.Scale =
                        captured / 100F;
                    RefreshPercentageMenu(
                        menu,
                        (int)Math.Round(
                            _hostContext.Scale * 100));
                    RequestRender();
                };
                menu.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(
                new ToolStripSeparator());
            menu.DropDownItems.Add(
                "Custom...",
                null,
                delegate
                {
                    ShowCustomScaleDialog();
                    RefreshPercentageMenu(
                        menu,
                        (int)Math.Round(
                            _hostContext.Scale * 100));
                });
            menu.DropDownOpening += delegate
            {
                RefreshPercentageMenu(
                    menu,
                    (int)Math.Round(
                        _hostContext.Scale * 100));
            };
            return menu;
        }

        private ToolStripMenuItem BuildOpacityMenu()
        {
            var menu = new ToolStripMenuItem(
                "Opacity");
            foreach (int percentage in
                new[] { 25, 50, 75, 90, 100 })
            {
                int captured = percentage;
                var item = new ToolStripMenuItem(
                    captured + "%")
                {
                    Tag = captured
                };
                item.Click += delegate
                {
                    _hostContext.Opacity =
                        captured / 100D;
                    RefreshPercentageMenu(
                        menu,
                        (int)Math.Round(
                            _hostContext.Opacity * 100));
                    RequestRender();
                };
                menu.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(new ToolStripSeparator());
            var hover = new ToolStripMenuItem(
                "100% while mouse is over widget")
            {
                Checked = _fullOpacityOnHover,
                CheckOnClick = false
            };
            hover.Click += delegate
            {
                _fullOpacityOnHover = !_fullOpacityOnHover;
                hover.Checked = _fullOpacityOnHover;
                WriteBooleanSetting("host.fullOpacityOnHover",
                    _fullOpacityOnHover);
                ApplyEffectiveOpacity();
            };
            menu.DropDownItems.Add(hover);
            menu.DropDownOpening += delegate
            {
                hover.Checked = _fullOpacityOnHover;
                RefreshPercentageMenu(
                    menu,
                    (int)Math.Round(
                        _hostContext.Opacity * 100));
            };
            return menu;
        }

        private double EffectiveWidgetOpacity
        {
            get
            {
                return _fullOpacityOnHover && _pointerOverWidget
                    ? 1D : _widgetOpacity;
            }
        }

        private void HandleOpacityMouseEnter(object sender, EventArgs e)
        {
            if (_pointerOverWidget) return;
            _pointerOverWidget = true;
            ApplyEffectiveOpacity();
        }

        private void HandleOpacityMouseLeave()
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)delegate
            {
                if (IsDisposed || Bounds.Contains(Cursor.Position)) return;
                _pointerOverWidget = false;
                ApplyEffectiveOpacity();
            });
        }

        private void ApplyEffectiveOpacity()
        {
            if (_perPixelAlpha)
                PresentLayeredSurface();
            else
                base.Opacity = EffectiveWidgetOpacity;
        }

        private static ToolStripMenuItem BuildAppearanceMenu(
            IWidgetThemeProvider themeProvider,
            IWidgetStyleProvider styleProvider)
        {
            var menu = new ToolStripMenuItem(
                "Appearance");
            if (themeProvider != null)
                menu.DropDownItems.Add(
                    BuildThemeMenu(themeProvider));
            if (styleProvider != null)
                menu.DropDownItems.Add(
                    BuildStyleMenu(styleProvider));
            return menu;
        }

        private static ToolStripMenuItem BuildThemeMenu(
            IWidgetThemeProvider provider)
        {
            var menu = new ToolStripMenuItem("Theme");
            PopulateThemeMenu(menu, provider);
            return menu;
        }

        private static void PopulateThemeMenu(
            ToolStripMenuItem menu,
            IWidgetThemeProvider provider)
        {
            foreach (string theme in provider.Themes)
            {
                string captured = theme;
                var item = new ToolStripMenuItem(
                    captured)
                {
                    Tag = captured,
                    Checked = string.Equals(
                        provider.Theme,
                        captured,
                        StringComparison.OrdinalIgnoreCase)
                };
                item.Click += delegate
                {
                    provider.Theme = captured;
                };
                menu.DropDownItems.Add(item);
            }
        }

        private static ToolStripMenuItem BuildStyleMenu(
            IWidgetStyleProvider provider)
        {
            var menu = new ToolStripMenuItem("Style");
            PopulateStyleMenu(menu, provider);
            return menu;
        }

        private static void PopulateStyleMenu(
            ToolStripMenuItem menu,
            IWidgetStyleProvider provider)
        {
            var styles =
                new List<WidgetStyleMetadata>();
            if (provider.Styles != null)
                styles.AddRange(provider.Styles);
            foreach (WidgetStyleMetadata style in styles)
            {
                WidgetStyleMetadata captured = style;
                var item = new ToolStripMenuItem(
                    captured.DisplayName)
                {
                    Tag = captured.Id,
                    ToolTipText =
                        captured.Description,
                    Checked = string.Equals(
                        provider.Style,
                        captured.Id,
                        StringComparison.OrdinalIgnoreCase)
                };
                item.Click += delegate
                {
                    provider.Style = captured.Id;
                };
                menu.DropDownItems.Add(item);
            }
            if (styles.Count > 1)
            {
                menu.DropDownItems.Add(
                    new ToolStripSeparator());
                menu.DropDownItems.Add(
                    "Manage Styles...",
                    null,
                    delegate
                    {
                        provider.ManageStyles();
                    });
            }
        }

        private static void RefreshThemeMenu(
            ContextMenuStrip menu,
            IWidgetThemeProvider provider)
        {
            if (provider == null)
                return;
            ToolStripMenuItem themeMenu =
                FindAppearanceSubmenu(
                    menu,
                    "Theme");
            if (themeMenu == null)
                return;
            themeMenu.DropDownItems.Clear();
            PopulateThemeMenu(
                themeMenu,
                provider);
        }

        private static void RefreshStyleMenu(
            ContextMenuStrip menu,
            IWidgetStyleProvider provider)
        {
            if (provider == null)
                return;
            ToolStripMenuItem styleMenu =
                FindAppearanceSubmenu(
                    menu,
                    "Style");
            if (styleMenu == null)
                return;
            styleMenu.DropDownItems.Clear();
            PopulateStyleMenu(
                styleMenu,
                provider);
        }

        private static ToolStripMenuItem
            FindAppearanceSubmenu(
                ContextMenuStrip menu,
                string text)
        {
            return FindAppearanceSubmenu(menu.Items, text);
        }

        private static ToolStripMenuItem
            FindAppearanceSubmenu(
                ToolStripItemCollection items,
                string text)
        {
            foreach (ToolStripItem root in items)
            {
                var appearance =
                    root as ToolStripMenuItem;
                if (appearance == null)
                    continue;
                if (appearance.Text == "Appearance")
                {
                    foreach (ToolStripItem child in
                        appearance.DropDownItems)
                    {
                        var submenu =
                            child as ToolStripMenuItem;
                        if (submenu != null &&
                            submenu.Text == text)
                            return submenu;
                    }
                }
                ToolStripMenuItem nested = FindAppearanceSubmenu(
                    appearance.DropDownItems, text);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static void RefreshPercentageMenu(
            ToolStripMenuItem menu,
            int percentage)
        {
            foreach (ToolStripItem child in
                menu.DropDownItems)
            {
                var item =
                    child as ToolStripMenuItem;
                if (item != null &&
                    item.Tag is int)
                    item.Checked =
                        (int)item.Tag == percentage;
            }
        }

        private void ShowCustomScaleDialog()
        {
            using (var form = new Form())
            using (var number = new NumericUpDown())
            {
                form.Text = "Custom Widget Scale";
                form.FormBorderStyle =
                    FormBorderStyle.FixedDialog;
                form.StartPosition =
                    FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(286, 112);
                form.Font = new Font("Segoe UI", 9F);
                form.Controls.Add(new Label
                {
                    Text = "Scale (%)",
                    Location = new Point(16, 18),
                    Size = new Size(92, 25)
                });
                number.Minimum = 50;
                number.Maximum = 200;
                number.Value = (decimal)Math.Round(
                    _hostContext.Scale * 100);
                number.Location = new Point(112, 15);
                number.Size = new Size(150, 27);
                form.Controls.Add(number);
                var apply = new Button
                {
                    Text = "Apply",
                    DialogResult = DialogResult.OK,
                    Location = new Point(104, 66),
                    Size = new Size(76, 30)
                };
                var cancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(188, 66),
                    Size = new Size(76, 30)
                };
                form.Controls.Add(apply);
                form.Controls.Add(cancel);
                form.AcceptButton = apply;
                form.CancelButton = cancel;
                if (form.ShowDialog(this) !=
                    DialogResult.OK)
                    return;
                _hostContext.Scale =
                    (float)number.Value / 100F;
                RequestRender();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _logging.Debug("WidgetWindow OnFormClosing: " + _widget.Id + "; reason=" + e.CloseReason + ".");
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _logging.Debug("WidgetWindow OnFormClosed: " + _widget.Id + "; reason=" + e.CloseReason + ".");
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_perPixelAlpha)
            {
                PresentLayeredSurface();
                return;
            }
            base.OnPaint(e);
            _widget.Render(e.Graphics, new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterForMonitorPowerChanges();
            using (Graphics graphics = CreateGraphics())
                _dpiScale = Math.Max(1F, graphics.DpiX / 96F);
            ApplyScaledSize();
            EnsureVisibleOnMonitor();
            ApplyClickThroughStyle();
            if (_windowShape ==
                WidgetWindowShape.Rectangle)
                return;
            _perPixelAlpha = false;
            SetPerPixelAlpha(true);
            EnsureFixedCompositionSurfaceSize();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterFromMonitorPowerChanges();
            base.OnHandleDestroyed(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_perPixelAlpha)
            {
                CreateInteractionSurface();
                CreateHotspotWindows();
                if (_snapToEdges)
                {
                    SnapVisibleCompositionAfterMove();
                    SaveWidgetPosition();
                }
                return;
            }

            _logging.Debug(
                "Finalizing initial layered widget presentation: " +
                _widget.Id + "; ClientSize=" +
                ClientSize.Width + "x" +
                ClientSize.Height + "; HandleCreated=" +
                IsHandleCreated + "; Visible=" + Visible +
                "; Pending=" +
                _layeredPresentationPending + ".");
            PresentLayeredSurface(true);
            // The input surface is a child HWND.  Creating it before the
            // parent has received its first UpdateLayeredWindow bitmap lets
            // Windows briefly paint the child as a dark rectangle.  It is
            // only needed after the widget is visible.
            CreateInteractionSurface();
            CreateHotspotWindows();
            FinalizeInitialFixedCompositionSurface();
            if (!_fixedCompositionSurface && _snapToEdges)
            {
                SnapVisibleCompositionAfterMove();
                SaveWidgetPosition();
            }
        }

        private void FinalizeInitialFixedCompositionSurface()
        {
            if (_initialFixedCompositionFinalized ||
                !_fixedCompositionSurface ||
                !IsHandleCreated)
                return;

            _initialFixedCompositionFinalized = true;
            BeginInvoke((MethodInvoker)delegate
            {
                if (IsDisposed ||
                    Disposing ||
                    !_fixedCompositionSurface ||
                    !IsHandleCreated)
                    return;

                Rectangle parentBounds = _compositionParentBounds;
                Point visibleParentLocation = new Point(
                    Left + (int)Math.Round(
                        parentBounds.X * _scale * _dpiScale),
                    Top + (int)Math.Round(
                        parentBounds.Y * _scale * _dpiScale));

                // This deliberately mirrors WidgetHostContext's
                // SetFixedCompositionSurface path.  The point here is not to
                // invent a new snapping rule; it makes initial launch follow
                // the proven post-theme-change native sizing sequence.
                ApplyScaledSizeWithoutLayeredPresentation();
                Location = new Point(
                    visibleParentLocation.X - (int)Math.Round(
                        parentBounds.X * _scale * _dpiScale),
                    visibleParentLocation.Y - (int)Math.Round(
                        parentBounds.Y * _scale * _dpiScale));
                EnsureFixedCompositionSurfaceSize();
                _logging.Debug(
                    "Initial fixed composition finalized: widget=" +
                    _widget.Id + "; required=" +
                    RequiredFixedCompositionSurfaceSize().Width + "x" +
                    RequiredFixedCompositionSurfaceSize().Height +
                    "; actual=" + ClientSize.Width + "x" +
                    ClientSize.Height + ".");
                PresentLayeredSurface();
                if (_snapToEdges)
                {
                    SnapVisibleCompositionAfterMove();
                    SaveWidgetPosition();
                }
            });
        }

        protected override void WndProc(ref Message message)
        {
            const int WmDpiChanged = 0x02E0;
            bool restoreAfterMessage = message.Msg == WmDisplayChange;
            if (message.Msg == WmPowerBroadcast)
            {
                int powerEvent = message.WParam.ToInt32();
                restoreAfterMessage =
                    powerEvent == PbtApmResumeSuspend ||
                    powerEvent == PbtApmResumeAutomatic ||
                    MonitorPoweredOn(powerEvent, message.LParam);
            }
            if (message.Msg == WmGetMinMaxInfo)
            {
                // A borderless WinForms window inherits Windows' normal
                // maximum tracking size, which is the monitor width.  The
                // Weather slide-out uses a deliberately wider *transparent*
                // composition surface at 200%; its visible card still fits
                // on-screen, but the host needs the extra width to render
                // and snap that card to the right edge.  Lift only this
                // internal tracking limit for fixed composition surfaces.
                base.WndProc(ref message);
                ApplyFixedCompositionTrackSize(message.LParam);
                return;
            }
            if (message.Msg == WmDpiChanged)
            {
                int anchoredMainLeft = _rightAnchoredComposite &&
                    _rightAnchoredMainWidth > 0
                    ? Right - _rightAnchoredMainWidth
                    : Left;
                float previousDpiScale = _dpiScale;
                int dpi = message.WParam.ToInt32() & 0xFFFF;
                _dpiScale = Math.Max(1F, dpi / 96F);
                if (_rightAnchoredComposite &&
                    _rightAnchoredMainWidth > 0 &&
                    previousDpiScale > 0F)
                    _rightAnchoredMainWidth = (int)Math.Round(
                        _rightAnchoredMainWidth *
                        (_dpiScale / previousDpiScale));
                ApplyScaledSizeWithoutLayeredPresentation();
                if (_rightAnchoredComposite &&
                    _rightAnchoredMainWidth > 0)
                    Left = anchoredMainLeft -
                        (Width - _rightAnchoredMainWidth);
                RequestRender();
            }
            // At large scales a fixed transparent composition can be wider
            // than the monitor.  Windows then constrains the *whole* host
            // rectangle during a normal titleless drag, even though the
            // visible card inside it would fit at the right edge.  Correct
            // the proposed WM_MOVING rectangle before Windows applies it so
            // Weather-style slide panels can still snap their visible card
            // flush to the right edge at 200%.
            if (message.Msg == WmMoving)
            {
                TrackPointerEdgeIntent();
                if (!_natureWeatherDragActive)
                {
                    SnapOversizedCompositionToRightWhileDragging(message.LParam);
                    TrackFixedCompositionTopClamp(message.LParam);
                }
            }

            TrackDragDiagnostic(message.Msg);

            // Layered windows can occasionally bypass the normal WinForms
            // MouseDown/MouseUp event route. Dispatch left-button messages
            // directly so widget hotspots remain reliable.
            if (message.Msg == WmLeftButtonUp && _pointerWindowDragActive)
            {
                FinishPointerWindowDrag();
                message.Result = IntPtr.Zero;
                return;
            }
            if (_pointerInput != null && !_clickThrough)
            {
                if (message.Msg == WmLeftButtonDown)
                {
                    Point location = PointFromMessage(message.LParam);
                    Rectangle hotspot;
                    if (TryGetHotspotAt(location, out hotspot))
                    {
                        _activeHotspotBounds = hotspot;
                        _pointerInput.PointerDown(HotspotCenter(hotspot), WidgetPointerButton.Left);
                        _pointerCapturedByWidget = true;
                        Capture = true;
                        return;
                    }
                    if (_pointerInput.PointerDown(location, WidgetPointerButton.Left))
                    {
                        _activeHotspotBounds = Rectangle.Empty;
                        _pointerCapturedByWidget = true;
                        Capture = true;
                        return;
                    }
                }
                else if (message.Msg == WmLeftButtonUp)
                {
                    Point location = PointFromMessage(message.LParam);
                    if (_pointerCapturedByWidget)
                    {
                        Rectangle active = _activeHotspotBounds;
                        _pointerCapturedByWidget = false;
                        _activeHotspotBounds = Rectangle.Empty;
                        Capture = false;
                        _pointerInput.PointerUp(active.IsEmpty ? location : HotspotCenter(active), WidgetPointerButton.Left);
                        return;
                    }

                    // Some layered-window configurations deliver the button-up
                    // message without the matching WinForms MouseDown event.
                    // Give an interactive region one final hit-test at release
                    // so a normal click cannot silently turn into a dead hotspot.
                    if (_pointerInput.PointerDown(location, WidgetPointerButton.Left))
                    {
                        _pointerInput.PointerUp(location, WidgetPointerButton.Left);
                        return;
                    }
                }
            }

            base.WndProc(ref message);

            if (restoreAfterMessage)
                ScheduleSavedPositionRestore(
                    message.Msg == WmDisplayChange
                    ? "display change" : "display wake");

            if (message.Msg == WmMoving &&
                CorrectNatureWeatherDragRectangle(message.LParam))
                message.Result = new IntPtr(1);

            if (message.Msg == WmExitSizeMove)
            {
                try
                {
                    CompleteDragDiagnostic();
                    // Nature placement is completed after SendMessage returns,
                    // outside the native loop. A click must not run snap logic.
                    if (!_natureWeatherDragActive)
                    {
                        RestoreVisibleTopAfterNativeHostClamp();
                        SnapVisibleCompositionAfterMove();
                        SaveWidgetPosition();
                    }
                }
                finally
                {
                    if (_dragInProgress)
                        EngineScheduler.EndWidgetDrag();
                    _dragInProgress = false;
                    _dragStartedSnappedLeft = false;
                    _dragStartedSnappedRight = false;
                    _dragStartedSnappedTop = false;
                    _dragStartedSnappedBottom = false;
                    _dragDetachedFromLeft = false;
                    _dragDetachedFromRight = false;
                    _dragDetachedFromTop = false;
                    _dragDetachedFromBottom = false;
                    _dragReachedRightScreenEdge = false;
                    FlushDeferredDragRender();
                }
            }
        }

        private void RegisterForMonitorPowerChanges()
        {
            if (!IsHandleCreated ||
                _monitorPowerNotification != IntPtr.Zero)
                return;
            try
            {
                Guid setting = MonitorPowerOnSetting;
                _monitorPowerNotification =
                    RegisterPowerSettingNotification(
                        Handle, ref setting, 0);
                Guid sessionSetting = SessionDisplayStatusSetting;
                _sessionDisplayNotification =
                    RegisterPowerSettingNotification(
                        Handle, ref sessionSetting, 0);
                if (_monitorPowerNotification == IntPtr.Zero &&
                    _sessionDisplayNotification == IntPtr.Zero)
                    _logging.Warning(
                        "Monitor power notification registration failed: " +
                        _widget.Id + "; Error=" +
                        Marshal.GetLastWin32Error() + ".");
            }
            catch (Exception ex)
            {
                _logging.Warning(
                    "Monitor power notification is unavailable for " +
                    _widget.Id + ": " + ex.Message);
            }
        }

        private void UnregisterFromMonitorPowerChanges()
        {
            try
            {
                if (_monitorPowerNotification != IntPtr.Zero)
                    UnregisterPowerSettingNotification(
                        _monitorPowerNotification);
                if (_sessionDisplayNotification != IntPtr.Zero)
                    UnregisterPowerSettingNotification(
                        _sessionDisplayNotification);
            }
            catch
            {
                // The window is already closing; there is nothing to recover.
            }
            _monitorPowerNotification = IntPtr.Zero;
            _sessionDisplayNotification = IntPtr.Zero;
        }

        private static bool MonitorPoweredOn(
            int powerEvent, IntPtr settingPointer)
        {
            if (powerEvent != PbtPowerSettingChange ||
                settingPointer == IntPtr.Zero)
                return false;
            try
            {
                PowerBroadcastSetting setting =
                    (PowerBroadcastSetting)Marshal.PtrToStructure(
                        settingPointer,
                        typeof(PowerBroadcastSetting));
                if ((setting.PowerSetting != MonitorPowerOnSetting &&
                    setting.PowerSetting != SessionDisplayStatusSetting) ||
                    setting.DataLength < 4)
                    return false;
                return Marshal.ReadInt32(
                    settingPointer,
                    Marshal.SizeOf(typeof(PowerBroadcastSetting))) != 0;
            }
            catch
            {
                return false;
            }
        }

        private void DisplaySettingsChanged(object sender, EventArgs e)
        {
            ScheduleSavedPositionRestore("display settings change");
        }

        private void ScheduleSavedPositionRestore(string reason)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        ScheduleSavedPositionRestore(reason);
                    });
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            _placementRestoreReason = reason ?? string.Empty;
            _placementRestorePass = 0;
            if (_placementRestoreTimer == null)
            {
                _placementRestoreTimer =
                    new System.Windows.Forms.Timer();
                _placementRestoreTimer.Tick +=
                    PlacementRestoreTimerTick;
            }
            _placementRestoreTimer.Stop();
            _placementRestoreTimer.Interval = 350;
            _placementRestoreTimer.Start();
        }

        private void PlacementRestoreTimerTick(
            object sender, EventArgs e)
        {
            _placementRestoreTimer.Stop();
            if (IsDisposed || Disposing)
                return;
            if (_dragInProgress)
            {
                _placementRestoreTimer.Interval = 500;
                _placementRestoreTimer.Start();
                return;
            }

            RestoreSavedWidgetPosition(_placementRestoreReason);
            _placementRestorePass++;
            // Windows can finish restoring the display after the first power
            // notification. A second pass prevents its late adjustment from
            // leaving matched widgets a few pixels apart.
            if (_placementRestorePass < 2)
            {
                _placementRestoreTimer.Interval = 900;
                _placementRestoreTimer.Start();
            }
        }

        private void RestoreSavedWidgetPosition(string reason)
        {
            Point saved = _settings.GetWidgetPosition(
                _widget.Id, _widget.DefaultLocation);
            Point requested = saved;
            if (_fixedCompositionSurface)
            {
                Rectangle anchor = ScaledCompositionBounds();
                requested = new Point(
                    saved.X - anchor.X,
                    saved.Y - anchor.Y);
            }
            else if (_rightAnchoredComposite &&
                _rightAnchoredMainWidth > 0)
            {
                requested = new Point(
                    saved.X - (Width - _rightAnchoredMainWidth),
                    saved.Y);
            }

            bool displaced = Location != requested;
            if (displaced)
            {
                if (IsHandleCreated)
                    SetWindowPos(Handle, IntPtr.Zero,
                        requested.X, requested.Y, 0, 0,
                        0x0001 | SwpNoZOrder | SwpNoActivate);
                else
                    Location = requested;
            }
            UpdateHotspotWindows();
            RequestRender();
            _logging.Debug(
                "Saved widget placement restored after " + reason +
                ": widget=" + _widget.Id +
                "; savedVisible=" + saved.X + "," + saved.Y +
                "; requestedHost=" + requested.X + "," + requested.Y +
                "; actualHost=" + Left + "," + Top +
                "; displaced=" + displaced + ".");
        }

        private void TrackDragDiagnostic(int message)
        {
            if (message == WmEnterSizeMove)
            {
                if (!_dragInProgress)
                    StartEdgeDragTracking();
                _dragMovingMessages = 0;
                _dragMoveMessages = 0;
                _dragWindowPosChangingMessages = 0;
                _dragWindowPosChangedMessages = 0;
                _dragReachedRightScreenEdge = false;
                _dragEncounteredNativeTopClamp = false;
                _dragDiagnosticTimer = Stopwatch.StartNew();
                return;
            }

            if (_dragDiagnosticTimer == null)
                return;

            switch (message)
            {
                case WmMoving:
                    _dragMovingMessages++;
                    break;
                case WmMove:
                    _dragMoveMessages++;
                    break;
                case WmWindowPosChanging:
                    _dragWindowPosChangingMessages++;
                    break;
                case WmWindowPosChanged:
                    _dragWindowPosChangedMessages++;
                    break;
            }
        }

        private void SnapOversizedCompositionToRightWhileDragging(IntPtr movingRectangle)
        {
            if (!_snapToEdges ||
                !_perPixelAlpha ||
                !_fixedCompositionSurface ||
                (_dragStartedSnappedRight && _dragDetachedFromRight) ||
                movingRectangle == IntPtr.Zero)
                return;

            Point pointer = Cursor.Position;
            Rectangle area = Screen.FromPoint(pointer).WorkingArea;
            if (Width <= area.Width)
                return;

            int threshold = Math.Max(
                18,
                (int)Math.Ceiling(18F * Math.Max(1F, _scale) * Math.Max(1F, _dpiScale)));
            if (pointer.X < area.Right - threshold)
                return;

            Rectangle anchor = ScaledCompositionBounds();
            int hostLeft = area.Right - anchor.Right;

            // WM_MOVING lParam is a native RECT: left, top, right, bottom.
            // Preserve the proposed vertical position and host size; change
            // only the horizontal origin needed by the visible composition.
            Marshal.WriteInt32(movingRectangle, 0, hostLeft);
            Marshal.WriteInt32(movingRectangle, 8, hostLeft + Width);
            _dragReachedRightScreenEdge = true;
        }

        private void TrackFixedCompositionTopClamp(
            IntPtr movingRectangle)
        {
            if (_snapToEdges ||
                !_perPixelAlpha ||
                !_fixedCompositionSurface ||
                movingRectangle == IntPtr.Zero)
                return;

            Rectangle anchor = ScaledCompositionBounds();
            if (anchor.Top <= 0)
                return;

            // Do not rewrite WM_MOVING here. Windows can disregard a negative
            // host position while its titleless move loop is active.  Instead
            // remember the exact clamp and correct it after that loop ends.
            int proposedTop = Marshal.ReadInt32(movingRectangle, 4);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (proposedTop > area.Top || _dragEncounteredNativeTopClamp)
                return;

            _dragEncounteredNativeTopClamp = true;
            _logging.Debug("Native top clamp observed: widget=" +
                _widget.Id + "; proposedHostTop=" + proposedTop +
                "; workAreaTop=" + area.Top + "; visibleInset=" +
                anchor.Top + ".");
        }

        private void RestoreVisibleTopAfterNativeHostClamp()
        {
            if (!_dragEncounteredNativeTopClamp ||
                _snapToEdges ||
                !_perPixelAlpha ||
                !_fixedCompositionSurface ||
                !IsHandleCreated)
                return;

            Rectangle anchor = ScaledCompositionBounds();
            if (anchor.Top <= 0)
                return;

            Rectangle visible = new Rectangle(
                Left + anchor.X, Top + anchor.Y,
                anchor.Width, anchor.Height);
            Rectangle area = Screen.FromRectangle(visible).WorkingArea;

            // This is deliberately an exact host-clamp check, rather than a
            // proximity tolerance.  It only restores the placement Windows
            // prevented during this drag; it never attracts a manually placed
            // widget toward the top edge.
            if (Math.Abs(Top - area.Top) > 1)
                return;

            int requestedHostTop = area.Top - anchor.Top;
            bool moved = SetWindowPos(
                Handle,
                IntPtr.Zero,
                Left,
                requestedHostTop,
                0,
                0,
                0x0001 | SwpNoZOrder | SwpNoActivate);
            _logging.Debug("Native top clamp restored: widget=" +
                _widget.Id + "; requestedHostTop=" + requestedHostTop +
                "; actualHostTop=" + Top + "; visibleTop=" +
                (Top + anchor.Top) + "; success=" + moved + ".");
        }

        private Rectangle ScaledCompositionBounds()
        {
            return new Rectangle(
                (int)Math.Round(_compositionParentBounds.X * _scale * _dpiScale),
                (int)Math.Round(_compositionParentBounds.Y * _scale * _dpiScale),
                (int)Math.Round(_compositionParentBounds.Width * _scale * _dpiScale),
                (int)Math.Round(_compositionParentBounds.Height * _scale * _dpiScale));
        }

        private void SnapVisibleCompositionAfterMove(bool explicitSnap = false)
        {
            Rectangle anchor = _fixedCompositionSurface
                ? ScaledCompositionBounds()
                : SnapAnchorBounds();
            Rectangle visible = new Rectangle(
                Left + anchor.X, Top + anchor.Y,
                anchor.Width, anchor.Height);
            Rectangle area = Screen.FromRectangle(visible).WorkingArea;
            if (UsesNatureWeatherPlacement())
            {
                ApplyNatureWeatherLocation(NatureWeatherPlacement.Stationary(
                    Location, anchor, area, explicitSnap && _snapToEdges, _keepOnScreen));
                return;
            }
            // This is a physical mouse-release tolerance, not an artwork
            // measurement.  Scaling it with the widget made an enlarged
            // Weather surface retain the top edge for an unexpectedly large
            // part of the screen.
            const int threshold = 18;
            int x = Left;
            int y = Top;
            // The themed Weather host keeps a fixed transparent composition
            // surface for its slide panel. At 200% that surface can be wider
            // than the monitor even though the visible main card is not.
            // Windows prevents an ordinary drag from reaching the required
            // negative X position, so finish the right-edge snap explicitly.
            bool oversizedFixedCompositionAtRight = _snapToEdges &&
                _perPixelAlpha && _fixedCompositionSurface &&
                Width > area.Width && visible.Right > area.Right &&
                Left <= area.Left + threshold;
            bool dragReachedRightEdge = _snapToEdges &&
                _dragReachedRightScreenEdge;
            bool detachedFromSnappedLeft = _dragInProgress &&
                _dragStartedSnappedLeft && _dragDetachedFromLeft;
            bool detachedFromSnappedRight = _dragInProgress &&
                _dragStartedSnappedRight && _dragDetachedFromRight;
            bool detachedFromSnappedTop = _dragInProgress &&
                _dragStartedSnappedTop && _dragDetachedFromTop;
            bool detachedFromSnappedBottom = _dragInProgress &&
                _dragStartedSnappedBottom && _dragDetachedFromBottom;
            _logging.Debug("Snap release: widget=" + _widget.Id +
                "; fixed=" + _fixedCompositionSurface +
                "; host=" + Left + "," + Top + "," + Width + "," + Height +
                "; anchor=" + anchor.X + "," + anchor.Y + "," + anchor.Width + "," + anchor.Height +
                "; visible=" + visible.Left + "," + visible.Top + "," + visible.Width + "," + visible.Height +
                "; area=" + area.Left + "," + area.Top + "," + area.Width + "," + area.Height +
                "; detached=" + detachedFromSnappedLeft + "," +
                    detachedFromSnappedTop + "," + detachedFromSnappedRight +
                    "," + detachedFromSnappedBottom);
            if ((_keepOnScreen && visible.Left < area.Left) ||
                (_snapToEdges && !detachedFromSnappedLeft &&
                    Math.Abs(visible.Left - area.Left) <= threshold))
                x += area.Left - visible.Left;
            else if ((_keepOnScreen && visible.Right > area.Right) ||
                (_snapToEdges && !detachedFromSnappedRight &&
                    (Math.Abs(visible.Right - area.Right) <= threshold ||
                    (_perPixelAlpha && Right >= area.Right - threshold &&
                        anchor.Right < Width) || oversizedFixedCompositionAtRight ||
                    dragReachedRightEdge)))
                x += area.Right - visible.Right;
            // Windows may stop a transparent layered host at the monitor top
            // before inset artwork reaches it. This applies to fixed Weather
            // compositions and to image-backed clocks. Finish the requested
            // snap after the native move loop so the visible frame, rather
            // than its transparent host canvas, reaches the screen edge.
            bool alphaSurfaceAtTop = _snapToEdges &&
                _perPixelAlpha &&
                !detachedFromSnappedTop &&
                Top <= area.Top + threshold &&
                anchor.Top > 0;
            bool alphaSurfaceAtBottom = _snapToEdges &&
                _perPixelAlpha &&
                !detachedFromSnappedBottom &&
                Bottom >= area.Bottom - threshold &&
                anchor.Bottom < Height;
            if ((_keepOnScreen && visible.Top < area.Top) ||
                (_snapToEdges && !detachedFromSnappedTop &&
                    (Math.Abs(visible.Top - area.Top) <= threshold ||
                    alphaSurfaceAtTop)))
                y += area.Top - visible.Top;
            else if ((_keepOnScreen && visible.Bottom > area.Bottom) ||
                (_snapToEdges && !detachedFromSnappedBottom &&
                    (Math.Abs(visible.Bottom - area.Bottom) <= threshold ||
                    alphaSurfaceAtBottom)))
                y += area.Bottom - visible.Bottom;
            bool snapApplied = x != Left || y != Top;
            if (snapApplied)
            {
                // Remember that a snap was requested before assigning
                // Location.  Location updates Left/Top synchronously, so
                // testing those values afterwards prevents the Win32 call
                // below from ever running.  Enlarged fixed-composition
                // Weather widgets need SetWindowPos to accept the negative
                // host offset that places their visible card at the right
                // screen edge.
                Location = new Point(x, y);

                // Alpha widgets can have transparent pixels above their
                // visible artwork. Re-apply the final snap through Win32
                // after release so the host can use the required negative
                // X or Y offset at enlarged scales.
                if (_perPixelAlpha && IsHandleCreated)
                {
                    const uint swpNoSize = 0x0001;
                    SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0,
                        swpNoSize | SwpNoZOrder | SwpNoActivate);
                    _logging.Debug("Snap release applied: widget=" + _widget.Id +
                        "; requested=" + x + "," + y + "; actual=" + Left + "," + Top);
                }
            }
        }

        private void TrackPointerEdgeIntent()
        {
            Point pointer = Cursor.Position;
            if (_dragInProgress)
            {
                int horizontalDetach = Math.Max(
                    2, SystemInformation.DragSize.Width);
                int verticalDetach = Math.Max(
                    2, SystemInformation.DragSize.Height);
                if (_dragStartedSnappedLeft)
                    _dragDetachedFromLeft = pointer.X >=
                        _dragStartPointer.X + horizontalDetach;
                if (_dragStartedSnappedRight)
                {
                    _dragDetachedFromRight = pointer.X <=
                        _dragStartPointer.X - horizontalDetach;
                    if (_dragDetachedFromRight)
                        _dragReachedRightScreenEdge = false;
                }
                if (_dragStartedSnappedTop)
                    _dragDetachedFromTop = pointer.Y >=
                        _dragStartPointer.Y + verticalDetach;
                if (_dragStartedSnappedBottom)
                    _dragDetachedFromBottom = pointer.Y <=
                        _dragStartPointer.Y - verticalDetach;
            }

            if (!_snapToEdges)
                return;

            Rectangle area = Screen.FromPoint(pointer).WorkingArea;
            const int threshold = 18;
            if (pointer.X >= area.Right - threshold)
                _dragReachedRightScreenEdge = true;
        }

        private bool UsesNatureWeatherPlacement()
        {
            IWidgetThemeProvider provider = _widget as IWidgetThemeProvider;
            return _fixedCompositionSurface && _widget.Id == "native.weather" &&
                provider != null &&
                (provider.Theme == "Botanical Nature" ||
                    provider.Theme == "Woodland Nature" ||
                    EmilyDeskThemeCatalog.Get(provider.Theme).IsImported);
        }

        private bool CorrectNatureWeatherDragRectangle(IntPtr movingRectangle)
        {
            if (!_natureWeatherDragActive || movingRectangle == IntPtr.Zero)
                return false;
            int left = Marshal.ReadInt32(movingRectangle, 0);
            int top = Marshal.ReadInt32(movingRectangle, 4);
            Point requested = NatureWeatherPlacement.PointerLocation(
                _dragStartHostLocation, _dragStartPointer, Cursor.Position);

            // Update the RECT itself: the native move loop applies it after
            // WM_MOVING returns. Merely moving the HWND inside WndProc can be
            // overwritten by that pending native placement.
            int width = Marshal.ReadInt32(movingRectangle, 8) - left;
            int height = Marshal.ReadInt32(movingRectangle, 12) - top;
            Marshal.WriteInt32(movingRectangle, 0, requested.X);
            Marshal.WriteInt32(movingRectangle, 4, requested.Y);
            Marshal.WriteInt32(movingRectangle, 8, requested.X + width);
            Marshal.WriteInt32(movingRectangle, 12, requested.Y + height);
            return true;
        }

        private void MoveNatureWeatherWithPointer()
        {
            StartEdgeDragTracking();
            _natureWeatherDragActive = true;
            try
            {
                ReleaseCapture();
                int packedPointer = unchecked(((_dragStartPointer.Y & 0xffff) << 16) |
                    (_dragStartPointer.X & 0xffff));
                SendMessage(Handle, 0xA1, new IntPtr(2), new IntPtr(packedPointer));
                if (IsDisposed || !IsHandleCreated)
                    return;

                // This is after the entire native move loop, not just inside
                // WM_EXITSIZEMOVE. Its final host clamp cannot overwrite us.
                Point pointer = Cursor.Position;
                Rectangle anchor = ScaledCompositionBounds();
                Point raw = NatureWeatherPlacement.PointerLocation(
                    _dragStartHostLocation, _dragStartPointer, pointer);
                Rectangle visible = new Rectangle(raw.X + anchor.X, raw.Y + anchor.Y,
                    anchor.Width, anchor.Height);
                bool cancelled = (Control.MouseButtons & MouseButtons.Left) != 0;
                Point requested = NatureWeatherPlacement.EndDrag(
                    _dragStartHostLocation, _dragStartPointer, pointer,
                    SystemInformation.DragSize, anchor,
                    Screen.FromRectangle(visible).WorkingArea,
                    _snapToEdges, _keepOnScreen, cancelled);
                ApplyNatureWeatherLocation(requested);
                SaveWidgetPosition();
            }
            finally
            {
                _natureWeatherDragActive = false;
                if (_dragInProgress)
                    EngineScheduler.EndWidgetDrag();
                _dragInProgress = false;
                FlushDeferredDragRender();
            }
        }

        private void ApplyNatureWeatherLocation(Point requested)
        {
            if (Location == requested)
                return;
            if (!IsHandleCreated)
            {
                Location = requested;
                return;
            }
            bool moved = SetWindowPos(Handle, IntPtr.Zero, requested.X, requested.Y,
                0, 0, 0x0001 | SwpNoZOrder | SwpNoActivate);
            _logging.Debug("Nature Weather visible placement: requested=" +
                requested.X + "," + requested.Y + "; actual=" + Left + "," + Top +
                "; success=" + moved + ".");
        }

        private void CaptureEdgeSnapDragState()
        {
            _dragStartPointer = Cursor.Position;
            _dragStartHostLocation = Location;
            _dragDetachedFromLeft = false;
            _dragDetachedFromRight = false;
            _dragDetachedFromTop = false;
            _dragDetachedFromBottom = false;
            Rectangle anchor = _fixedCompositionSurface
                ? ScaledCompositionBounds()
                : SnapAnchorBounds();
            Rectangle visible = new Rectangle(
                Left + anchor.X, Top + anchor.Y,
                anchor.Width, anchor.Height);
            Rectangle area = Screen.FromRectangle(visible).WorkingArea;
            // Treat the whole activation band as already attached. This
            // includes a fixed-composition host that an older build left at
            // the Windows clamp with its visible artwork still inset.
            _dragStartedSnappedLeft = _snapToEdges &&
                Math.Abs(visible.Left - area.Left) <= 18;
            _dragStartedSnappedRight = _snapToEdges &&
                Math.Abs(visible.Right - area.Right) <= 18;
            _dragStartedSnappedTop = _snapToEdges &&
                (Math.Abs(visible.Top - area.Top) <= 18 ||
                    (_fixedCompositionSurface && anchor.Top > 0 &&
                        Top <= area.Top + 1));
            _dragStartedSnappedBottom = _snapToEdges &&
                Math.Abs(visible.Bottom - area.Bottom) <= 18;
        }

        private Rectangle VisibleAlphaBounds()
        {
            if (!_perPixelAlpha || _layeredBitmap == null ||
                _layeredBitmap.Width < 1 || _layeredBitmap.Height < 1)
                return new Rectangle(0, 0, Width, Height);
            Rectangle source = new Rectangle(0, 0,
                _layeredBitmap.Width, _layeredBitmap.Height);
            System.Drawing.Imaging.BitmapData data = null;
            try
            {
                data = _layeredBitmap.LockBits(source,
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                int stride = Math.Abs(data.Stride);
                byte[] pixels = new byte[stride * data.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                int left = data.Width;
                int top = data.Height;
                int right = -1;
                int bottom = -1;
                for (int y = 0; y < data.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < data.Width; x++)
                    {
                        if (pixels[row + x * 4 + 3] <= 8) continue;
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
                }
                return right < left || bottom < top
                    ? new Rectangle(0, 0, Width, Height)
                    : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
            }
            catch
            {
                return new Rectangle(0, 0, Width, Height);
            }
            finally
            {
                if (data != null)
                    _layeredBitmap.UnlockBits(data);
            }
        }

        private Rectangle SnapAnchorBounds()
        {
            var provider = _widget as IWidgetSnapBoundsProvider;
            if (provider != null && _layeredBitmap != null)
            {
                Rectangle supplied = provider.GetSnapBounds(
                    _layeredBitmap.Size);
                Rectangle surface = new Rectangle(Point.Empty,
                    _layeredBitmap.Size);
                supplied.Intersect(surface);
                if (supplied.Width > 0 && supplied.Height > 0)
                    return supplied;
            }
            return VisibleAlphaBounds();
        }

        private void CompleteDragDiagnostic()
        {
            Stopwatch timer = _dragDiagnosticTimer;
            if (timer == null)
                return;

            _dragDiagnosticTimer = null;
            timer.Stop();
            _logging.Debug(
                "Native widget drag summary: id=" + _widget.Id +
                "; durationMs=" + timer.ElapsedMilliseconds +
                "; WM_MOVING=" + _dragMovingMessages +
                "; WM_MOVE=" + _dragMoveMessages +
                "; WM_WINDOWPOSCHANGING=" +
                _dragWindowPosChangingMessages +
                "; WM_WINDOWPOSCHANGED=" +
                _dragWindowPosChangedMessages +
                "; persistenceWriteDuringDrag=false" +
                "; summaryWrites=1.");
        }

        private static Point PointFromMessage(IntPtr value)
        {
            long packed = value.ToInt64();
            int x = unchecked((short)(packed & 0xFFFF));
            int y = unchecked((short)((packed >> 16) & 0xFFFF));
            return new Point(x, y);
        }

        private void HandleMouseDown(
            object sender,
            MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left &&
                TryHandleDesktopDoubleClick(e.Location))
                return;
            if (_pointerInput != null)
            {
                Rectangle hotspot;
                if (e.Button == MouseButtons.Left && TryGetHotspotAt(e.Location, out hotspot))
                {
                    _activeHotspotBounds = hotspot;
                    _pointerInput.PointerDown(HotspotCenter(hotspot), WidgetPointerButton.Left);
                    _pointerCapturedByWidget = true;
                    _pointerCaptureControl =
                        sender as Control ?? this;
                    _pointerCaptureControl.Capture = true;
                    return;
                }
                if (_pointerInput.PointerDown(e.Location, PointerButton(e.Button)))
                {
                    _activeHotspotBounds = Rectangle.Empty;
                    _pointerCapturedByWidget = true;
                    _pointerCaptureControl =
                        sender as Control ?? this;
                    _pointerCaptureControl.Capture = true;
                    return;
                }
            }
            if (e.Button != MouseButtons.Left)
                return;
            if (_positionLocked)
                return;
            if (UsesNatureWeatherPlacement())
            {
                BeginPointerWindowDrag(sender);
                return;
            }
            BeginPointerWindowDrag(sender);
        }

        private void BeginPointerWindowDrag(object sender)
        {
            StartEdgeDragTracking();
            Control capture = sender as Control ?? this;
            _pointerWindowDragCapture = capture;
            capture.Capture = true;
            _pointerWindowDragMoves = 0;
            _pointerWindowDragTimer = Stopwatch.StartNew();
            _pointerWindowDragActive = true;
        }

        private void FinishPointerWindowDrag()
        {
            if (!_pointerWindowDragActive)
                return;
            _pointerWindowDragActive = false;
            Control capture = _pointerWindowDragCapture;
            _pointerWindowDragCapture = null;
            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    TrackPointerEdgeIntent();
                    if (UsesNatureWeatherPlacement())
                    {
                        Point pointer = Cursor.Position;
                        Rectangle anchor = ScaledCompositionBounds();
                        Point raw = NatureWeatherPlacement.PointerLocation(
                            _dragStartHostLocation, _dragStartPointer, pointer);
                        Rectangle visible = new Rectangle(
                            raw.X + anchor.X, raw.Y + anchor.Y,
                            anchor.Width, anchor.Height);
                        Point requested = NatureWeatherPlacement.EndDrag(
                            _dragStartHostLocation, _dragStartPointer, pointer,
                            SystemInformation.DragSize, anchor,
                            Screen.FromRectangle(visible).WorkingArea,
                            _snapToEdges, _keepOnScreen,
                            (Control.MouseButtons & MouseButtons.Left) != 0);
                        ApplyNatureWeatherLocation(requested);
                    }
                    else
                        SnapVisibleCompositionAfterMove();
                    SaveWidgetPosition();
                }
            }
            finally
            {
                Stopwatch timer = _pointerWindowDragTimer;
                _pointerWindowDragTimer = null;
                if (timer != null)
                {
                    timer.Stop();
                    _logging.Debug("Pointer widget drag summary: id=" +
                        _widget.Id + "; durationMs=" +
                        timer.ElapsedMilliseconds + "; moves=" +
                        _pointerWindowDragMoves + ".");
                }
                if (_dragInProgress)
                    EngineScheduler.EndWidgetDrag();
                _dragInProgress = false;
                _dragStartedSnappedLeft = false;
                _dragStartedSnappedRight = false;
                _dragStartedSnappedTop = false;
                _dragStartedSnappedBottom = false;
                _dragDetachedFromLeft = false;
                _dragDetachedFromRight = false;
                _dragDetachedFromTop = false;
                _dragDetachedFromBottom = false;
                _dragReachedRightScreenEdge = false;
                if (capture != null && !capture.IsDisposed)
                    capture.Capture = false;
                FlushDeferredDragRender();
            }
        }

        private void HandleWindowDragCaptureChanged(object sender, EventArgs e)
        {
            if (_pointerWindowDragActive &&
                sender == _pointerWindowDragCapture &&
                !_pointerWindowDragCapture.Capture)
                FinishPointerWindowDrag();
        }

        private void StartEdgeDragTracking()
        {
            if (!_dragInProgress)
                EngineScheduler.BeginWidgetDrag();
            _dragInProgress = true;
            CaptureEdgeSnapDragState();
        }

        private bool TryHandleDesktopDoubleClick(Point location)
        {
            if (_doubleClickInput == null || _clickThrough)
                return false;
            DateTime now = DateTime.UtcNow;
            Size tolerance = SystemInformation.DoubleClickSize;
            bool repeated = (now - _lastDesktopClick).TotalMilliseconds <=
                    SystemInformation.DoubleClickTime &&
                Math.Abs(location.X - _lastDesktopClickPoint.X) <=
                    tolerance.Width / 2 &&
                Math.Abs(location.Y - _lastDesktopClickPoint.Y) <=
                    tolerance.Height / 2;
            _lastDesktopClick = repeated ? DateTime.MinValue : now;
            _lastDesktopClickPoint = location;
            return repeated && _doubleClickInput.PointerDoubleClick(
                location, WidgetPointerButton.Left);
        }

        private void HandleFileDragEnter(
            object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (_fileDropTarget == null || _clickThrough ||
                !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            string[] paths = e.Data.GetData(
                DataFormats.FileDrop) as string[];
            if (_fileDropTarget.CanAcceptFiles(paths))
                e.Effect = DragDropEffects.Move;
        }

        private void HandleFileDrop(
            object sender, DragEventArgs e)
        {
            if (_fileDropTarget == null || _clickThrough ||
                !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            string[] paths = e.Data.GetData(
                DataFormats.FileDrop) as string[];
            if (_fileDropTarget.CanAcceptFiles(paths))
                _fileDropTarget.DropFiles(paths);
        }

        private void HandleMouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (_pointerWindowDragActive)
            {
                if ((Control.MouseButtons & MouseButtons.Left) == 0)
                {
                    FinishPointerWindowDrag();
                    return;
                }
                TrackPointerEdgeIntent();
                Point pointer = Cursor.Position;
                Point requested = new Point(
                    _dragStartHostLocation.X + pointer.X - _dragStartPointer.X,
                    _dragStartHostLocation.Y + pointer.Y - _dragStartPointer.Y);
                if (requested != Location && IsHandleCreated)
                {
                    if (SetWindowPos(Handle, IntPtr.Zero,
                        requested.X, requested.Y, 0, 0,
                        0x0001 | SwpNoZOrder | SwpNoActivate))
                        _pointerWindowDragMoves++;
                }
                return;
            }
            if (_pointerInput != null)
                _pointerInput.PointerMove(e.Location);
        }

        private void HandleMouseUp(
            object sender,
            MouseEventArgs e)
        {
            if (_pointerWindowDragActive && e.Button == MouseButtons.Left)
            {
                FinishPointerWindowDrag();
                return;
            }
            if (_pointerCapturedByWidget &&
                _pointerInput != null)
            {
                Rectangle active = _activeHotspotBounds;
                Control captureControl =
                    _pointerCaptureControl;
                _pointerCapturedByWidget = false;
                _pointerCaptureControl = null;
                _activeHotspotBounds = Rectangle.Empty;
                _pointerInput.PointerUp(
                    active.IsEmpty ? e.Location : HotspotCenter(active),
                    PointerButton(e.Button));
                if (captureControl != null)
                    captureControl.Capture = false;
                return;
            }
            if (e.Button == MouseButtons.Left)
                SaveWidgetPosition();
        }

        private void SaveWidgetPosition()
        {
            Point position = Location;
            if (_fixedCompositionSurface)
                position = new Point(
                    Left + (int)Math.Round(_compositionParentBounds.X * _scale * _dpiScale),
                    Top + (int)Math.Round(_compositionParentBounds.Y * _scale * _dpiScale));
            else if (_rightAnchoredComposite && _rightAnchoredMainWidth > 0)
                position = new Point(Right - _rightAnchoredMainWidth, Top);
            _settings.SetWidgetPosition(_widget.Id, position);
        }

        private static WidgetPointerButton PointerButton(
            MouseButtons button)
        {
            if (button == MouseButtons.Left)
                return WidgetPointerButton.Left;
            if (button == MouseButtons.Right)
                return WidgetPointerButton.Right;
            if (button == MouseButtons.Middle)
                return WidgetPointerButton.Middle;
            return WidgetPointerButton.None;
        }

        internal void RequestRender()
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)RequestRender);
                return;
            }
            // A layered widget already has a complete image in the desktop
            // compositor. Rebuilding that image inside the native move loop
            // interrupts pointer movement; the latest frame can be drawn on
            // release without losing any widget state updates.
            if (_dragInProgress && _perPixelAlpha)
            {
                _layeredPresentationPending = true;
                return;
            }
            UpdateHotspotWindows();
            if (_perPixelAlpha)
                PresentLayeredSurface();
            else
                Invalidate();
        }

        private void FlushDeferredDragRender()
        {
            if (!_layeredPresentationPending || IsDisposed ||
                Disposing || !IsHandleCreated)
                return;

            // Post outside WM_EXITSIZEMOVE. A new drag may already have begun
            // when the callback runs, in which case its release will flush it.
            BeginInvoke((Action)delegate
            {
                if (!_dragInProgress && !IsDisposed &&
                    _layeredPresentationPending)
                    RequestRender();
            });
        }

        internal int LayeredPresentationCount
        {
            get { return _layeredPresentationCount; }
        }

        internal int AnchoredCompositeResizeCount
        {
            get { return _anchoredCompositeResizeCount; }
        }

        internal void PrepareForActivation()
        {
            if (!_clickThrough)
                return;
            _clickThrough = false;
            WriteBooleanSetting(
                "host.clickThrough",
                false);
            ApplyClickThroughStyle();
            if (_clickThrough)
                UpdateHotspotWindows();
            else if (Visible)
                CreateHotspotWindows();
        }

        private bool ReadBooleanSetting(
            string key,
            bool fallback)
        {
            bool value;
            return bool.TryParse(
                _settings.GetWidgetSetting(
                    _widget.Id,
                    key,
                    fallback.ToString(
                        CultureInfo.InvariantCulture)),
                out value)
                ? value
                : fallback;
        }

        private void WriteBooleanSetting(
            string key,
            bool value)
        {
            _settings.SetWidgetSetting(
                _widget.Id,
                key,
                value.ToString(
                    CultureInfo.InvariantCulture));
        }

        private void ApplyClickThroughStyle()
        {
            if (!IsHandleCreated)
                return;
            long style = GetExtendedStyle();
            if (_clickThrough)
                SetExtendedStyle(
                    style | WsExTransparent);
            else
                SetExtendedStyle(
                    style & ~WsExTransparent);
        }

        private void ApplyScaledSize()
        {
            Size = new Size(
                Math.Max(
                    120,
                    (int)Math.Round(
                        _preferredSize.Width * _scale * _dpiScale)),
                Math.Max(
                    100,
                    (int)Math.Round(
                        _preferredSize.Height * _scale * _dpiScale)));
            // WinForms can limit a borderless layered Form to the work area.
            // Weather's slide panel deliberately uses a wider transparent
            // composition, which otherwise clips its visible card at 200%.
            EnsureFixedCompositionSurfaceSize();
            ApplyWindowShape();
            UpdateHotspotWindows();
        }

        private void EnsureFixedCompositionSurfaceSize()
        {
            if (!IsHandleCreated ||
                !_perPixelAlpha ||
                !_fixedCompositionSurface)
                return;

            Size required = RequiredFixedCompositionSurfaceSize();
            int requiredWidth = required.Width;
            int requiredHeight = required.Height;
            if (ClientSize.Width >= requiredWidth &&
                ClientSize.Height >= requiredHeight)
                return;

            bool resized = SetWindowPos(
                Handle,
                IntPtr.Zero,
                Left,
                Top,
                requiredWidth,
                requiredHeight,
                SwpNoZOrder | SwpNoActivate);
            _logging.Debug(
                "Fixed composition native resize: widget=" +
                _widget.Id + "; requested=" + requiredWidth + "x" +
                requiredHeight + "; actual=" + ClientSize.Width + "x" +
                ClientSize.Height + "; success=" + resized + ".");
            if (!resized)
                _logging.Warning(
                    "Fixed composition native resize failed: " +
                    _widget.Id + "; Error=" +
                    Marshal.GetLastWin32Error() + ".");
        }

        private Size RequiredFixedCompositionSurfaceSize()
        {
            return new Size(
                Math.Max(
                    120,
                    (int)Math.Round(
                        _preferredSize.Width * _scale * _dpiScale)),
                Math.Max(
                    100,
                    (int)Math.Round(
                        _preferredSize.Height * _scale * _dpiScale)));
        }

        private void ApplyFixedCompositionTrackSize(IntPtr minMaxInfoPointer)
        {
            if (!_fixedCompositionSurface ||
                minMaxInfoPointer == IntPtr.Zero)
                return;

            Size required = RequiredFixedCompositionSurfaceSize();
            NativeMinMaxInfo minMaxInfo =
                (NativeMinMaxInfo)Marshal.PtrToStructure(
                    minMaxInfoPointer,
                    typeof(NativeMinMaxInfo));
            int beforeWidth = minMaxInfo.MaxTrackSize.X;
            int beforeHeight = minMaxInfo.MaxTrackSize.Y;
            // WinForms ultimately routes SetWindowPos through the same
            // non-client sizing rules as a maximise operation on some
            // systems.  Raise both limits: MaxTrackSize governs drag/program
            // sizing, while MaxSize prevents the monitor-sized maximise cap
            // from re-clamping the transparent host afterwards.
            minMaxInfo.MaxSize.X = Math.Max(
                minMaxInfo.MaxSize.X,
                required.Width);
            minMaxInfo.MaxSize.Y = Math.Max(
                minMaxInfo.MaxSize.Y,
                required.Height);
            minMaxInfo.MaxTrackSize.X = Math.Max(
                minMaxInfo.MaxTrackSize.X,
                required.Width);
            minMaxInfo.MaxTrackSize.Y = Math.Max(
                minMaxInfo.MaxTrackSize.Y,
                required.Height);
            Marshal.StructureToPtr(
                minMaxInfo,
                minMaxInfoPointer,
                false);

            if (!_reportedFixedCompositionTrackSize &&
                (beforeWidth < required.Width ||
                 beforeHeight < required.Height))
            {
                _reportedFixedCompositionTrackSize = true;
                _logging.Debug(
                    "Fixed composition tracking limit raised: widget=" +
                    _widget.Id + "; required=" +
                    required.Width + "x" + required.Height +
                    "; previous=" + beforeWidth + "x" + beforeHeight + ".");
            }
        }

        private void ApplyScaledSizeWithoutLayeredPresentation()
        {
            bool previous = _suppressLayeredPresentation;
            _suppressLayeredPresentation = true;
            try
            {
                ApplyScaledSize();
            }
            finally
            {
                _suppressLayeredPresentation = previous;
            }
        }

        private void EnsureVisibleOnMonitor()
        {
            foreach (Screen screen in Screen.AllScreens)
                if (screen.WorkingArea.IntersectsWith(Bounds))
                    return;
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            Location = new Point(
                Math.Max(area.Left,
                    Math.Min(area.Right - Math.Min(Width, area.Width), Left)),
                Math.Max(area.Top,
                    Math.Min(area.Bottom - Math.Min(Height, area.Height), Top)));
        }

        private static void RefreshOfficialMenuItems(
            IEnumerable<KeyValuePair<
                ToolStripMenuItem,
                WidgetMenuCommand>> items)
        {
            foreach (KeyValuePair<
                ToolStripMenuItem,
                WidgetMenuCommand> item in items)
                item.Key.Checked = item.Value.IsChecked;
        }

        private void ApplyWindowShape()
        {
            Region oldRegion = Region;
            if (_windowShape !=
                    WidgetWindowShape.Rectangle &&
                _windowShape !=
                    WidgetWindowShape.AlphaRectangle &&
                ClientSize.Width > 0 &&
                ClientSize.Height > 0)
            {
                using (var path = new GraphicsPath())
                {
                    Rectangle bounds = new Rectangle(
                        0,
                        0,
                        ClientSize.Width,
                        ClientSize.Height);
                    if (_windowShape ==
                        WidgetWindowShape.Ellipse)
                        path.AddEllipse(bounds);
                    else
                        AddRoundedRectangle(
                            path,
                            bounds,
                            Math.Max(
                                8,
                                Math.Min(
                                    ClientSize.Width,
                                    ClientSize.Height) /
                                    18));
                    Region = new Region(path);
                }
            }
            else
                Region = null;
            if (oldRegion != null)
                oldRegion.Dispose();
            SetPerPixelAlpha(
                _windowShape !=
                    WidgetWindowShape.Rectangle);
        }

        private static void AddRoundedRectangle(
            GraphicsPath path,
            Rectangle bounds,
            int radius)
        {
            int diameter = radius * 2;
            path.AddArc(
                bounds.Left,
                bounds.Top,
                diameter,
                diameter,
                180,
                90);
            path.AddArc(
                bounds.Right - diameter,
                bounds.Top,
                diameter,
                diameter,
                270,
                90);
            path.AddArc(
                bounds.Right - diameter,
                bounds.Bottom - diameter,
                diameter,
                diameter,
                0,
                90);
            path.AddArc(
                bounds.Left,
                bounds.Bottom - diameter,
                diameter,
                diameter,
                90,
                90);
            path.CloseFigure();
        }

        private void SetPerPixelAlpha(bool enabled)
        {
            if (!IsHandleCreated)
            {
                _perPixelAlpha = enabled;
                return;
            }
            if (_perPixelAlpha == enabled)
            {
                if (enabled && !_suppressLayeredPresentation)
                {
                    EnsureLayeredStyle();
                    PresentLayeredSurface();
                }
                return;
            }

            _perPixelAlpha = enabled;
            long style = GetExtendedStyle();
            if (enabled)
            {
                base.Opacity = 1D;
                EnsureLayeredStyle();
                PresentLayeredSurface();
            }
            else
            {
                if (_layeredBitmap != null)
                {
                    _layeredBitmap.Dispose();
                    _layeredBitmap = null;
                }
                SetExtendedStyle(style & ~WsExLayered);
                base.Opacity = EffectiveWidgetOpacity;
                Invalidate();
            }
        }

        private long GetExtendedStyle()
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(
                    Handle,
                    GwlExStyle).ToInt64()
                : GetWindowLong32(Handle, GwlExStyle);
        }

        private void SetExtendedStyle(long value)
        {
            if (IntPtr.Size == 8)
                SetWindowLongPtr64(
                    Handle,
                    GwlExStyle,
                    new IntPtr(value));
            else
                SetWindowLong32(
                    Handle,
                    GwlExStyle,
                    unchecked((int)value));
        }

        private void EnsureLayeredStyle()
        {
            if (!IsHandleCreated)
                return;
            long style = GetExtendedStyle();
            if ((style & WsExLayered) == 0)
                SetExtendedStyle(style | WsExLayered);
        }

        private void PresentLayeredSurface(
            bool usePrimedFrame = false,
            bool allowHidden = false)
        {
            if (!_perPixelAlpha ||
                !IsHandleCreated ||
                ClientSize.Width < 1 ||
                ClientSize.Height < 1)
                return;
            if (_dragInProgress && !allowHidden)
            {
                _layeredPresentationPending = true;
                return;
            }
            if (!Visible && !allowHidden)
            {
                _layeredPresentationPending = true;
                return;
            }
            _layeredPresentationCount++;

            EnsureLayeredBitmap();
            if (!(usePrimedFrame && _initialLayeredFramePrimed))
            {
                using (Graphics graphics =
                    Graphics.FromImage(_layeredBitmap))
                {
                    graphics.Clear(Color.Transparent);
                    _widget.Render(
                        graphics,
                        new Rectangle(
                            0,
                            0,
                            ClientSize.Width - 1,
                            ClientSize.Height - 1));
                }
            }
            _initialLayeredFramePrimed = false;

            _layeredPresentationPending = false;
            EnsureLayeredStyle();

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc =
                CreateCompatibleDC(screenDc);
            IntPtr bitmapHandle =
                _layeredBitmap.GetHbitmap(
                    Color.FromArgb(0));
            IntPtr oldBitmap =
                SelectObject(memoryDc, bitmapHandle);
            try
            {
                var destination =
                    new NativePoint(Left, Top);
                var source = new NativePoint(0, 0);
                var size = new NativeSize(
                    ClientSize.Width,
                    ClientSize.Height);
                var blend = new BlendFunction
                {
                    BlendOp = AcSrcOver,
                    SourceConstantAlpha = (byte)Math.Round(
                        EffectiveWidgetOpacity * 255D),
                    AlphaFormat = AcSrcAlpha
                };
                if (!UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    UlwAlpha))
                    _logging.Warning(
                        "UpdateLayeredWindow failed: " +
                        _widget.Id + "; Error=" +
                        Marshal.GetLastWin32Error() +
                        "; ExStyle=0x" +
                        GetExtendedStyle().ToString(
                            "X") + ".");
            }
            finally
            {
                SelectObject(memoryDc, oldBitmap);
                DeleteObject(bitmapHandle);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void EnsureLayeredBitmap()
        {
            if (_layeredBitmap != null &&
                _layeredBitmap.Width == ClientSize.Width &&
                _layeredBitmap.Height == ClientSize.Height)
                return;

            if (_layeredBitmap != null)
                _layeredBitmap.Dispose();
            _layeredBitmap = new Bitmap(
                ClientSize.Width,
                ClientSize.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_dragInProgress)
                {
                    EngineScheduler.EndWidgetDrag();
                    _dragInProgress = false;
                }
                if (_displayEventsSubscribed)
                {
                    SystemEvents.DisplaySettingsChanged -=
                        DisplaySettingsChanged;
                    _displayEventsSubscribed = false;
                }
                UnregisterFromMonitorPowerChanges();
                if (_placementRestoreTimer != null)
                {
                    _placementRestoreTimer.Stop();
                    _placementRestoreTimer.Dispose();
                    _placementRestoreTimer = null;
                }
                if (_layeredBitmap != null)
                {
                    _layeredBitmap.Dispose();
                    _layeredBitmap = null;
                }
            }
            base.Dispose(disposing);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public NativeSize(int width, int height)
            {
                Width = width;
                Height = height;
            }
            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PowerBroadcastSetting
        {
            public Guid PowerSetting;
            public int DataLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMinMaxInfo
        {
            public NativePoint Reserved;
            public NativePoint MaxSize;
            public NativePoint MaxPosition;
            public NativePoint MinTrackSize;
            public NativePoint MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private sealed class PointerInputSurface : Control
        {
            private readonly Func<Point, bool> _acceptsInput;

            public PointerInputSurface(Func<Point, bool> acceptsInput)
            {
                _acceptsInput = acceptsInput;
                SetStyle(
                    ControlStyles.SupportsTransparentBackColor |
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);
            }

            protected override void WndProc(ref Message message)
            {
                const int WmNcHitTest = 0x0084;
                const int HtTransparent = -1;
                if (message.Msg == WmNcHitTest &&
                    _acceptsInput != null &&
                    !_acceptsInput(PointToClient(Cursor.Position)))
                {
                    message.Result = new IntPtr(HtTransparent);
                    return;
                }
                base.WndProc(ref message);
            }

            protected override void OnPaintBackground(
                PaintEventArgs e)
            {
                // Rendering belongs to the layered parent bitmap. The surface
                // exists only to provide a dependable child window for input.
            }

            protected override void OnPaint(PaintEventArgs e)
            {
            }
        }

        private sealed class WidgetHostContext :
            IWidgetHostContext
        {
            private readonly WidgetWindow _window;

            public WidgetHostContext(WidgetWindow window)
            {
                _window = window;
                float scale;
                if (float.TryParse(
                    GetSetting("scale", "1"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out scale))
                    Scale = scale;
                double opacity;
                if (double.TryParse(
                    GetSetting("opacity", "1"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out opacity))
                    Opacity = opacity;
            }

            public string GetSetting(
                string key,
                string fallback)
            {
                return _window._settings.GetWidgetSetting(
                    _window._widget.Id,
                    key,
                    fallback);
            }

            public void SetSetting(string key, string value)
            {
                _window._settings.SetWidgetSetting(
                    _window._widget.Id,
                    key,
                    value);
            }

            public double Opacity
            {
                get { return _window._widgetOpacity; }
                set
                {
                    double clamped = Math.Max(
                        0.2,
                        Math.Min(1, value));
                    _window._widgetOpacity = clamped;
                    _window.ApplyEffectiveOpacity();
                    SetSetting(
                        "opacity",
                        clamped.ToString(
                            CultureInfo.InvariantCulture));
                }
            }

            public float Scale
            {
                get { return _window._scale; }
                set
                {
                    Rectangle fixedParentBounds =
                        _window._compositionParentBounds;
                    Point fixedParentLocation = _window._fixedCompositionSurface
                        ? new Point(
                            _window.Left + (int)Math.Round(
                                fixedParentBounds.X * _window._scale * _window._dpiScale),
                            _window.Top + (int)Math.Round(
                                fixedParentBounds.Y * _window._scale * _window._dpiScale))
                        : Point.Empty;
                    int anchoredMainLeft = _window._rightAnchoredComposite &&
                        _window._rightAnchoredMainWidth > 0
                        ? _window.Right - _window._rightAnchoredMainWidth
                        : _window.Left;
                    float previousScale = _window._scale;
                    _window._scale = Math.Max(
                        0.5F,
                        Math.Min(2F, value));
                    if (_window._rightAnchoredComposite &&
                        _window._rightAnchoredMainWidth > 0 &&
                        previousScale > 0F)
                        _window._rightAnchoredMainWidth = (int)Math.Round(
                            _window._rightAnchoredMainWidth *
                            (_window._scale / previousScale));
                    if (_window._rightAnchoredComposite)
                        _window.ApplyScaledSizeWithoutLayeredPresentation();
                    else
                        _window.ApplyScaledSize();
                    if (_window._fixedCompositionSurface)
                        _window.Location = new Point(
                            fixedParentLocation.X - (int)Math.Round(
                                fixedParentBounds.X * _window._scale * _window._dpiScale),
                            fixedParentLocation.Y - (int)Math.Round(
                                fixedParentBounds.Y * _window._scale * _window._dpiScale));
                    if (_window._rightAnchoredComposite &&
                        _window._rightAnchoredMainWidth > 0)
                        _window.Left = anchoredMainLeft -
                            (_window.Width - _window._rightAnchoredMainWidth);
                    float persistedScale = _window._scale;
                    SetSetting(
                        "scale",
                        persistedScale.ToString(
                            CultureInfo.InvariantCulture));
                    _window.SnapVisibleCompositionAfterMove();
                    _window.SaveWidgetPosition();
                }
            }

            public void SetPreferredSize(Size size)
            {
                Rectangle fixedParentBounds =
                    _window._compositionParentBounds;
                Point parentLocation = _window._fixedCompositionSurface
                    ? new Point(
                        _window.Left + (int)Math.Round(
                            fixedParentBounds.X * _window._scale * _window._dpiScale),
                        _window.Top + (int)Math.Round(
                            fixedParentBounds.Y * _window._scale * _window._dpiScale))
                    : _window.Location;
                _window._preferredSize = size;
                _window._fixedCompositionSurface = false;
                _window._compositionParentBounds = Rectangle.Empty;
                _window._rightAnchoredComposite = false;
                _window._rightAnchoredMainWidth = 0;
                _window.ApplyScaledSize();
                _window.Location = parentLocation;
            }

            public void SetPreferredSizeAnchoredRight(Size size)
            {
                int right = _window.Right;
                _window._anchoredCompositeResizeCount++;
                if (!_window._rightAnchoredComposite)
                {
                    _window._rightAnchoredMainWidth = _window.Width;
                    _window._rightAnchoredComposite = true;
                }
                _window._preferredSize = size;
                _window.ApplyScaledSizeWithoutLayeredPresentation();
                _window.Left = right - _window.Width;
            }

            public void SetFixedCompositionSurface(
                Size size, Rectangle parentBounds)
            {
                Rectangle fixedParentBounds =
                    _window._compositionParentBounds;
                Point parentLocation = _window._fixedCompositionSurface
                    ? new Point(
                        _window.Left + (int)Math.Round(
                            fixedParentBounds.X * _window._scale * _window._dpiScale),
                        _window.Top + (int)Math.Round(
                            fixedParentBounds.Y * _window._scale * _window._dpiScale))
                    : _window.Location;
                _window._preferredSize = size;
                _window._fixedCompositionSurface = true;
                _window._compositionParentBounds = parentBounds;
                _window._rightAnchoredComposite = false;
                _window._rightAnchoredMainWidth = 0;
                _window.ApplyScaledSizeWithoutLayeredPresentation();
                _window.Location = new Point(
                    parentLocation.X - (int)Math.Round(
                        parentBounds.X * _window._scale * _window._dpiScale),
                    parentLocation.Y - (int)Math.Round(
                        parentBounds.Y * _window._scale * _window._dpiScale));
            }

            public void SetWindowShape(WidgetWindowShape shape)
            {
                _window._windowShape = shape;
                _window.ApplyWindowShape();
            }

            public void Invalidate()
            {
                _window.RequestRender();
            }

            public void ReportDiagnostic(
                string message)
            {
                _window._logging.Warning(
                    "Widget diagnostic [" +
                    _window._widget.Id +
                    "]: " +
                    (message ?? string.Empty));
            }
        }
        private sealed class HotspotWindow : Form
        {
            private const int WsExToolWindow = 0x00000080;
            private const int WsExNoActivate = 0x08000000;
            private readonly WidgetWindow _ownerWindow;
            private readonly ToolTip _toolTip = new ToolTip();
            private WidgetHotspot _hotspot;
            private readonly bool _diagnosticsEnabled;

            public HotspotWindow(WidgetWindow ownerWindow, WidgetHotspot hotspot, bool diagnosticsEnabled)
            {
                _ownerWindow = ownerWindow;
                _hotspot = hotspot;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                BackColor = diagnosticsEnabled
                    ? Color.FromArgb(220, 35, 35)
                    : Color.Magenta;
                if (!diagnosticsEnabled)
                    TransparencyKey = Color.Magenta;
                Opacity = diagnosticsEnabled ? 0.48D : 1D;
                // Ownership controls Z-order. A separate TopMost hotspot would
                // remain above unrelated applications after the widget is covered.
                TopMost = false;
                Cursor = Cursors.Hand;
                TabStop = false;
                _toolTip.SetToolTip(this, hotspot.ToolTip);
                _diagnosticsEnabled = diagnosticsEnabled;
                UpdateInputRegion();
            }

            public WidgetHotspot Hotspot
            {
                get { return _hotspot; }
                set
                {
                    _hotspot = value;
                    _toolTip.SetToolTip(this, value == null ? string.Empty : value.ToolTip);
                    UpdateInputRegion();
                }
            }

            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                UpdateInputRegion();
            }

            private void UpdateInputRegion()
            {
                if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                    return;
                Region old = Region;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddEllipse(0, 0, ClientSize.Width, ClientSize.Height);
                    Region = new Region(path);
                }
                if (old != null) old.Dispose();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (!_diagnosticsEnabled)
                    return;
                using (var pen = new Pen(Color.White, 2F))
                using (var font = new Font("Segoe UI", 7F, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawRectangle(pen, 1, 1, Math.Max(1, ClientSize.Width - 3), Math.Max(1, ClientSize.Height - 3));
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString("HOTSPOT", font, brush, ClientRectangle, format);
                    format.Dispose();
                }
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams value = base.CreateParams;
                    value.ExStyle |= WsExToolWindow | WsExNoActivate;
                    return value;
                }
            }

            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                _toolTip.Dispose();
                base.OnFormClosed(e);
            }
        }

    }
}
