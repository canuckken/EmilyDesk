using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace XWidgetReborn
{
    internal sealed class DragDiagnosticLauncher : Form
    {
        public DragDiagnosticLauncher(
            Action openPlain,
            Action openFormOnly,
            Action openHeaderOnly,
            Action openComplete,
            Action openC1,
            Action openC2,
            Action openC3,
            Action openC4,
            Action openC5,
            Action openNormal,
            Action openD1,
            Action openD2,
            Action openD3)
        {
            Text = "EmilyDesk Drag Diagnostic A/B Test 2";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(520, 725);
            Font = new Font("Segoe UI", 9F);

            Controls.Add(new Label
            {
                Location = new Point(18, 16),
                Size = new Size(484, 42),
                Text = "Begin every drag from the actual Windows title bar " +
                    "above the diagnostic content."
            });
            Controls.Add(MakeButton(
                "TEST A — Plain WinForms Window",
                68,
                openPlain));
            Controls.Add(MakeButton(
                "TEST B1 — Form Properties Only",
                118,
                openFormOnly));
            Controls.Add(MakeButton(
                "TEST B2 — Header Only",
                168,
                openHeaderOnly));
            Controls.Add(MakeButton(
                "TEST B3 — Complete Empty Shell",
                218,
                openComplete));
            Controls.Add(MakeButton(
                "TEST C1 — Cards, No Previews",
                268,
                openC1));
            Controls.Add(MakeButton(
                "TEST C2 — Previews, No Running-State Polling",
                318,
                openC2));
            Controls.Add(MakeButton(
                "TEST C3 — Previews Loaded Before Display",
                368,
                openC3));
            Controls.Add(MakeButton(
                "TEST C4 — One Preview Application Pass",
                418,
                openC4));
            Controls.Add(MakeButton(
                "TEST C5 — Running-State Snapshot Only",
                468,
                openC5));
            Controls.Add(MakeButton(
                "TEST C — Normal Full Gallery",
                518,
                openNormal));
            Controls.Add(MakeButton(
                "TEST D1 — 25 Native Cards",
                568,
                openD1));
            Controls.Add(MakeButton(
                "TEST D2 — 100 Native Cards",
                618,
                openD2));
            Controls.Add(MakeButton(
                "TEST D3 — Single Virtual Gallery Surface",
                668,
                openD3));
        }

        private static Button MakeButton(
            string text,
            int top,
            Action action)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(18, top),
                Size = new Size(484, 38)
            };
            button.Click += delegate { action(); };
            return button;
        }
    }

    internal static class DragDiagnosticTrace
    {
        private const int TimerIntervalMilliseconds = 50;
        private const int GapThresholdMilliseconds = 100;
        private static readonly object Sync = new object();
        private static readonly object DurationSync = new object();
        private static readonly Dictionary<string, List<TraceEntry>>
            PendingDurations =
                new Dictionary<string, List<TraceEntry>>();
        private static readonly Dictionary<Form, WindowProbe> Probes =
            new Dictionary<Form, WindowProbe>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly DateTime StartUtc = DateTime.UtcNow;
        private static System.Windows.Forms.Timer _gapTimer;
        private static long _lastGapTick;
        private static bool _moveObservedSinceTick;
        private static int _windowsInSizeMove;

        public static string LogPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    "Logs",
                    "drag-diagnostic.log");
            }
        }

        public static void Attach(
            Form form,
            string diagnosticName,
            Func<bool> previewActive,
            Func<bool> statePollActive,
            Func<string> subsystemState = null)
        {
            if (form == null ||
                Probes.ContainsKey(form))
                return;

            var probe = new WindowProbe(
                form,
                diagnosticName,
                previewActive,
                statePollActive,
                subsystemState);
            Probes.Add(form, probe);
            probe.Attach();
            EnsureGapTimer();
        }

        private static void EnsureGapTimer()
        {
            if (_gapTimer != null) return;
            _lastGapTick = Clock.ElapsedMilliseconds;
            _gapTimer = new System.Windows.Forms.Timer
            {
                Interval = TimerIntervalMilliseconds
            };
            _gapTimer.Tick += GapTimerTick;
            _gapTimer.Start();
        }

        private static void GapTimerTick(
            object sender,
            EventArgs e)
        {
            long now = Clock.ElapsedMilliseconds;
            long gap = now - _lastGapTick;
            _lastGapTick = now;
            bool inSizeMove = _windowsInSizeMove > 0 ||
                _moveObservedSinceTick;
            _moveObservedSinceTick = false;
            if (gap <= GapThresholdMilliseconds)
                return;

            string openWindows = GetOpenWindowNames();
            foreach (WindowProbe probe in
                new List<WindowProbe>(Probes.Values))
            {
                probe.RefreshResourceMetricsIfDue();
                probe.RecordUiGap(
                    gap,
                    openWindows,
                    inSizeMove);
            }
        }

        public static void RecordDuration(
            string testName,
            string operation,
            long milliseconds,
            string subsystemState)
        {
            string name = string.IsNullOrWhiteSpace(testName)
                ? "Gallery Diagnostic"
                : testName;
            List<TraceEntry> flush = null;
            lock (DurationSync)
            {
                List<TraceEntry> entries;
                if (!PendingDurations.TryGetValue(
                    name,
                    out entries))
                {
                    entries = new List<TraceEntry>();
                    PendingDurations[name] = entries;
                }
                entries.Add(new TraceEntry
                {
                    ElapsedMilliseconds = Clock.ElapsedMilliseconds,
                    EventName = "OPERATION_DURATION",
                    Detail = "operation=" + operation +
                        ";durationMs=" + milliseconds +
                        ";" + (subsystemState ?? string.Empty)
                });
                if (entries.Count >= 64)
                {
                    flush = entries;
                    PendingDurations.Remove(name);
                }
            }
            if (flush != null)
                QueueWrite(name, flush, "operation-batch");
        }

        private static void FlushDurations(string name)
        {
            List<TraceEntry> entries = null;
            lock (DurationSync)
            {
                if (PendingDurations.TryGetValue(
                    name,
                    out entries))
                    PendingDurations.Remove(name);
            }
            if (entries != null)
                QueueWrite(name, entries, "operation-flush");
        }

        private static string GetOpenWindowNames()
        {
            var names = new List<string>();
            foreach (WindowProbe probe in Probes.Values)
                if (!probe.Form.IsDisposed)
                    names.Add(probe.Name);
            return string.Join(",", names.ToArray());
        }

        private static void EnterSizeMove()
        {
            _windowsInSizeMove++;
            _moveObservedSinceTick = true;
        }

        private static void ObserveSizeMove()
        {
            _moveObservedSinceTick = true;
        }

        private static void ExitSizeMove()
        {
            if (_windowsInSizeMove > 0)
                _windowsInSizeMove--;
            _moveObservedSinceTick = true;
        }

        private static void Remove(WindowProbe probe)
        {
            Probes.Remove(probe.Form);
            if (Probes.Count != 0 ||
                _gapTimer == null)
                return;
            _gapTimer.Stop();
            _gapTimer.Dispose();
            _gapTimer = null;
        }

        private static DateTime GetTimestampUtc(long elapsed)
        {
            return StartUtc.AddMilliseconds(elapsed);
        }

        private static void QueueWrite(
            string name,
            IList<TraceEntry> entries,
            string reason)
        {
            if (entries == null || entries.Count == 0)
                return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var text = new StringBuilder();
                    text.AppendLine(
                        "# " + name + " flush=" + reason);
                    text.AppendLine(
                        "timestampUtc\tevent\tnativeRect\tformBounds" +
                        "\tleftMouseDown\tinsideSizeMove\tdetail");
                    foreach (TraceEntry entry in entries)
                    {
                        text.Append(
                            GetTimestampUtc(entry.ElapsedMilliseconds)
                                .ToString("o"));
                        text.Append('\t');
                        text.Append(entry.EventName);
                        text.Append('\t');
                        text.Append(FormatRectangle(entry.NativeBounds));
                        text.Append('\t');
                        text.Append(FormatRectangle(entry.FormBounds));
                        text.Append('\t');
                        text.Append(entry.LeftMouseDown);
                        text.Append('\t');
                        text.Append(entry.InSizeMove);
                        text.Append('\t');
                        text.Append(entry.Detail ?? string.Empty);
                        text.AppendLine();
                    }

                    lock (Sync)
                    {
                        string folder = Path.GetDirectoryName(LogPath);
                        Directory.CreateDirectory(folder);
                        File.AppendAllText(LogPath, text.ToString());
                    }
                }
                catch { }
            });
        }

        private static string FormatRectangle(Rectangle rectangle)
        {
            return rectangle.Left + "," + rectangle.Top + "," +
                rectangle.Right + "," + rectangle.Bottom;
        }

        private sealed class WindowProbe
        {
            private const int MaximumEntries = 4096;
            private readonly Queue<TraceEntry> _entries =
                new Queue<TraceEntry>();
            private readonly Func<bool> _previewActive;
            private readonly Func<bool> _statePollActive;
            private readonly Func<string> _subsystemState;
            private readonly NativeMessageObserver _observer;
            private bool _insideSizeMove;
            private long _lastResourceSample;
            private long _workingSet;
            private long _privateBytes;
            private uint _gdiObjects;
            private uint _userObjects;

            public WindowProbe(
                Form form,
                string name,
                Func<bool> previewActive,
                Func<bool> statePollActive,
                Func<string> subsystemState)
            {
                Form = form;
                Name = name;
                _previewActive = previewActive;
                _statePollActive = statePollActive;
                _subsystemState = subsystemState;
                _observer = new NativeMessageObserver(this);
            }

            public Form Form { get; private set; }
            public string Name { get; private set; }

            public void Attach()
            {
                Form.HandleCreated += HandleCreated;
                Form.HandleDestroyed += HandleDestroyed;
                Form.Move += FormMove;
                Form.LocationChanged += FormLocationChanged;
                Form.FormClosed += FormClosed;
                if (Form.IsHandleCreated)
                    _observer.Attach(Form.Handle);
            }

            public void RecordNativeMessage(
                string eventName)
            {
                if (eventName == "WM_ENTERSIZEMOVE")
                {
                    _insideSizeMove = true;
                    EnterSizeMove();
                }
                else if (eventName == "WM_MOVING")
                {
                    ObserveSizeMove();
                }

                AddEntry(eventName, string.Empty);

                if (eventName != "WM_EXITSIZEMOVE")
                    return;
                _insideSizeMove = false;
                ExitSizeMove();
                Flush("WM_EXITSIZEMOVE");
            }

            public void RecordUiGap(
                long milliseconds,
                string openWindows,
                bool inSizeMove)
            {
                string detail =
                    "gapMs=" + milliseconds +
                    ";open=" + openWindows +
                    ";previewActive=" + SafeGet(_previewActive) +
                    ";statePollActive=" + SafeGet(_statePollActive);
                AddEntry(
                    "UI_THREAD_GAP",
                    detail,
                    inSizeMove);
            }

            public void RefreshResourceMetricsIfDue()
            {
                long now = Clock.ElapsedMilliseconds;
                if (now - _lastResourceSample < 1000)
                    return;
                _lastResourceSample = now;
                try
                {
                    using (Process process =
                        Process.GetCurrentProcess())
                    {
                        _workingSet = process.WorkingSet64;
                        _privateBytes = process.PrivateMemorySize64;
                        _gdiObjects = GetGuiResources(
                            process.Handle,
                            0);
                        _userObjects = GetGuiResources(
                            process.Handle,
                            1);
                    }
                }
                catch { }
            }

            private void HandleCreated(
                object sender,
                EventArgs e)
            {
                _observer.Attach(Form.Handle);
                RecordWindowInformation();
            }

            private void HandleDestroyed(
                object sender,
                EventArgs e)
            {
                _observer.Detach();
            }

            private void FormMove(
                object sender,
                EventArgs e)
            {
                AddEntry("Form.Move", string.Empty);
            }

            private void FormLocationChanged(
                object sender,
                EventArgs e)
            {
                AddEntry("Form.LocationChanged", string.Empty);
            }

            private void FormClosed(
                object sender,
                FormClosedEventArgs e)
            {
                AddEntry("FormClosed", string.Empty);
                Flush("FormClosed");
                Detach();
                Remove(this);
            }

            private void RecordWindowInformation()
            {
                RefreshResourceMetricsIfDue();
                AddEntry(
                    "WINDOW_INFO",
                    GetWindowInformation(Form),
                    false);
                Flush("WINDOW_INFO");
            }

            private void AddEntry(
                string eventName,
                string detail)
            {
                AddEntry(eventName, detail, _insideSizeMove);
            }

            private void AddEntry(
                string eventName,
                string detail,
                bool inSizeMove)
            {
                Rectangle nativeBounds = Rectangle.Empty;
                NativeRect native;
                if (Form.IsHandleCreated &&
                    GetWindowRect(Form.Handle, out native))
                {
                    nativeBounds = Rectangle.FromLTRB(
                        native.Left,
                        native.Top,
                        native.Right,
                        native.Bottom);
                }
                var entry = new TraceEntry
                {
                    ElapsedMilliseconds = Clock.ElapsedMilliseconds,
                    EventName = eventName,
                    NativeBounds = nativeBounds,
                    FormBounds = Form.Bounds,
                    LeftMouseDown =
                        (GetAsyncKeyState(1) & 0x8000) != 0,
                    InSizeMove = inSizeMove,
                    Detail = AppendDiagnosticState(detail)
                };
                lock (_entries)
                {
                    while (_entries.Count >= MaximumEntries)
                        _entries.Dequeue();
                    _entries.Enqueue(entry);
                }
            }

            private string AppendDiagnosticState(string detail)
            {
                string subsystem = SafeGetText(_subsystemState);
                return (detail ?? string.Empty) +
                    (string.IsNullOrEmpty(detail) ? string.Empty : ";") +
                    subsystem +
                    (string.IsNullOrEmpty(subsystem)
                        ? string.Empty
                        : ";") +
                    "gc0=" + GC.CollectionCount(0) +
                    ";gc1=" + GC.CollectionCount(1) +
                    ";gc2=" + GC.CollectionCount(2) +
                    ";workingSet=" + _workingSet +
                    ";privateBytes=" + _privateBytes +
                    ";gdiObjects=" + _gdiObjects +
                    ";userObjects=" + _userObjects;
            }

            private void Flush(string reason)
            {
                List<TraceEntry> snapshot;
                lock (_entries)
                {
                    snapshot = new List<TraceEntry>(_entries);
                    _entries.Clear();
                }
                QueueWrite(Name, snapshot, reason);
                FlushDurations(Name);
            }

            private void Detach()
            {
                _observer.Detach();
                Form.HandleCreated -= HandleCreated;
                Form.HandleDestroyed -= HandleDestroyed;
                Form.Move -= FormMove;
                Form.LocationChanged -= FormLocationChanged;
                Form.FormClosed -= FormClosed;
            }

            private static bool SafeGet(Func<bool> getter)
            {
                if (getter == null) return false;
                try { return getter(); }
                catch { return false; }
            }

            private static string SafeGetText(Func<string> getter)
            {
                if (getter == null) return string.Empty;
                try { return getter() ?? string.Empty; }
                catch { return string.Empty; }
            }

            private static string GetWindowInformation(Form form)
            {
                int createStyle = 0;
                int createExStyle = 0;
                try
                {
                    PropertyInfo property = typeof(Control).GetProperty(
                        "CreateParams",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                    var parameters = property == null
                        ? null
                        : property.GetValue(form, null) as CreateParams;
                    if (parameters != null)
                    {
                        createStyle = parameters.Style;
                        createExStyle = parameters.ExStyle;
                    }
                }
                catch { }

                IntPtr style = GetWindowLongPtr(
                    form.Handle,
                    GwlStyle);
                IntPtr exStyle = GetWindowLongPtr(
                    form.Handle,
                    GwlExStyle);
                NativeRect client;
                GetClientRect(form.Handle, out client);
                NativeRect window;
                GetWindowRect(form.Handle, out window);

                uint dpi = GetWindowDpi(form);
                string monitorName = GetMonitorName(form.Handle);
                bool compositionEnabled = false;
                try
                {
                    bool enabled;
                    if (DwmIsCompositionEnabled(out enabled) == 0)
                        compositionEnabled = enabled;
                }
                catch { }

                return
                    "createParamsStyle=" + Hex(createStyle) +
                    ";createParamsExStyle=" + Hex(createExStyle) +
                    ";gwlStyle=" + Hex(style) +
                    ";gwlExStyle=" + Hex(exStyle) +
                    ";dpi=" + dpi +
                    ";clientRect=" + FormatNativeRectangle(client) +
                    ";windowRect=" + FormatNativeRectangle(window) +
                    ";captionHeight=" + SystemInformation.CaptionHeight +
                    ";monitor=" + monitorName +
                    ";dwmCompositionEnabled=" + compositionEnabled;
            }

            private static uint GetWindowDpi(Form form)
            {
                try
                {
                    return GetDpiForWindow(form.Handle);
                }
                catch
                {
                    using (Graphics graphics =
                        Graphics.FromHwnd(form.Handle))
                        return (uint)Math.Round(graphics.DpiX);
                }
            }

            private static string GetMonitorName(IntPtr handle)
            {
                try
                {
                    IntPtr monitor = MonitorFromWindow(
                        handle,
                        MonitorDefaultToNearest);
                    var info = new MonitorInfoEx
                    {
                        Size = Marshal.SizeOf(
                            typeof(MonitorInfoEx))
                    };
                    return GetMonitorInfo(monitor, ref info)
                        ? info.DeviceName
                        : string.Empty;
                }
                catch { return string.Empty; }
            }

            private static string FormatNativeRectangle(
                NativeRect rectangle)
            {
                return rectangle.Left + "," + rectangle.Top + "," +
                    rectangle.Right + "," + rectangle.Bottom;
            }

            private static string Hex(int value)
            {
                return "0x" + unchecked((uint)value).ToString("X8");
            }

            private static string Hex(IntPtr value)
            {
                return IntPtr.Size == 8
                    ? "0x" + unchecked(
                        (ulong)value.ToInt64()).ToString("X16")
                    : Hex(value.ToInt32());
            }
        }

        private sealed class NativeMessageObserver : NativeWindow
        {
            private const int WmMove = 0x0003;
            private const int WmMoving = 0x0216;
            private const int WmEnterSizeMove = 0x0231;
            private const int WmExitSizeMove = 0x0232;
            private readonly WindowProbe _probe;

            public NativeMessageObserver(WindowProbe probe)
            {
                _probe = probe;
            }

            public void Attach(IntPtr handle)
            {
                if (Handle == handle) return;
                Detach();
                AssignHandle(handle);
            }

            public void Detach()
            {
                if (Handle != IntPtr.Zero)
                    ReleaseHandle();
            }

            protected override void WndProc(ref Message message)
            {
                switch (message.Msg)
                {
                    case WmEnterSizeMove:
                        _probe.RecordNativeMessage(
                            "WM_ENTERSIZEMOVE");
                        break;
                    case WmMoving:
                        _probe.RecordNativeMessage("WM_MOVING");
                        break;
                    case WmMove:
                        _probe.RecordNativeMessage("WM_MOVE");
                        break;
                    case WmExitSizeMove:
                        _probe.RecordNativeMessage(
                            "WM_EXITSIZEMOVE");
                        break;
                }
                base.WndProc(ref message);
            }
        }

        private sealed class TraceEntry
        {
            public long ElapsedMilliseconds;
            public string EventName;
            public Rectangle NativeBounds;
            public Rectangle FormBounds;
            public bool LeftMouseDown;
            public bool InSizeMove;
            public string Detail;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Auto)]
        private struct MonitorInfoEx
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;

            [MarshalAs(
                UnmanagedType.ByValTStr,
                SizeConst = 32)]
            public string DeviceName;
        }

        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const uint MonitorDefaultToNearest = 2;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(
            IntPtr window,
            out NativeRect rectangle);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(
            IntPtr window,
            out NativeRect rectangle);

        [DllImport(
            "user32.dll",
            EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(
            IntPtr window,
            int index);

        [DllImport(
            "user32.dll",
            EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(
            IntPtr window,
            int index);

        private static IntPtr GetWindowLongPtr(
            IntPtr window,
            int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(window, index)
                : GetWindowLong32(window, index);
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(
            IntPtr window,
            uint flags);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(
            IntPtr monitor,
            ref MonitorInfoEx information);

        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled(
            out bool enabled);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(
            IntPtr process,
            uint flags);
    }
}
