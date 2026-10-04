using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using XWidgetReborn.Shared;

namespace XWidgetReborn.Runtime.Core
{
    internal interface ILifecycleManager : IDisposable
    {
        event EventHandler Changed;
        LifecycleState GetState(string id);
        bool IsRunning(string id);
        string[] GetRunningWidgetIds();
        bool Start(string id);
        bool Stop(string id);
        bool Suspend(string id);
        bool Resume(string id);
        void Restore();
        void BringWidgetsAboveXWidgetReborn(IntPtr rebornWindow);
        void PlaceNormalWidgetsBehind(IntPtr applicationWindow);
        void LogWidgetLayerSnapshot(
            IntPtr rebornWindow,
            string trigger);
        void SetWidgetLayerMode(WidgetLayerMode mode);
    }

    internal sealed class LifecycleManager : ILifecycleManager
    {
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private const int GwlExStyle = -20;
        private const int GwlStyle = -16;
        private const uint WsExTopMost = 0x00000008;
        private const uint GwHwndNext = 2;
        private const uint GwHwndPrev = 3;
        private const uint GwOwner = 4;
        private const int DwmwaCloaked = 14;
        private const uint GuiInMoveSize = 0x00000002;
        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);
        private delegate bool EnumWindowsCallback(
            IntPtr window,
            IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(
            EnumWindowsCallback callback,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(
            IntPtr window,
            uint command);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(
            IntPtr window,
            int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(
            IntPtr window,
            int index);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr window,
            out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(
            IntPtr window,
            out NativeRect rectangle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGUIThreadInfo(
            uint threadId,
            ref GuiThreadInfo information);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(
            IntPtr window,
            int attribute,
            out int value,
            int valueSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(
            IntPtr window,
            StringBuilder text,
            int maximumCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(
            IntPtr window,
            StringBuilder className,
            int maximumCount);

        private sealed class LayerSurface
        {
            public string Id;
            public IntPtr Handle;
            public bool Legacy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GuiThreadInfo
        {
            public int Size;
            public uint Flags;
            public IntPtr Active;
            public IntPtr Focus;
            public IntPtr Capture;
            public IntPtr MenuOwner;
            public IntPtr MoveSize;
            public IntPtr Caret;
            public NativeRect CaretRectangle;
        }

        private sealed class Entry
        {
            public WidgetInstance Instance;
            public LifecycleState State;
        }

        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly IWidgetRegistry _registry;
        private readonly EngineScheduler _scheduler;
        private readonly IRuntimeSettingsService _settings;
        private readonly IRuntimeLoggingService _logging;
        private readonly IEventDispatcher _events;
        private readonly ServiceContainer _services;
        private bool _disposing;

        public event EventHandler Changed;

        public LifecycleManager(
            IWidgetRegistry registry,
            EngineScheduler scheduler,
            IRuntimeSettingsService settings,
            IRuntimeLoggingService logging,
            IEventDispatcher events,
            ServiceContainer services)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (scheduler == null) throw new ArgumentNullException("scheduler");
            if (settings == null) throw new ArgumentNullException("settings");
            if (logging == null) throw new ArgumentNullException("logging");
            if (events == null) throw new ArgumentNullException("events");
            if (services == null) throw new ArgumentNullException("services");
            _registry = registry;
            _scheduler = scheduler;
            _settings = settings;
            _logging = logging;
            _events = events;
            _services = services;
            _registry.Changed += RegistryChanged;
            _logging.Debug("LifecycleManager constructed.");
        }

        public LifecycleState GetState(string id)
        {
            Entry entry;
            if (_entries.TryGetValue(id, out entry)) return entry.State;
            return _registry.Contains(id)
                ? LifecycleState.Discovered
                : LifecycleState.Stopped;
        }

        public bool IsRunning(string id)
        {
            LifecycleState state = GetState(id);
            return state == LifecycleState.Loaded ||
                state == LifecycleState.Running ||
                state == LifecycleState.Suspended;
        }

        public string[] GetRunningWidgetIds()
        {
            var ids = new List<string>();
            foreach (KeyValuePair<string, Entry> item in _entries)
                if ((item.Value.State == LifecycleState.Loaded ||
                    item.Value.State == LifecycleState.Running ||
                    item.Value.State == LifecycleState.Suspended) &&
                    item.Value.Instance != null &&
                    item.Value.Instance.HasConfirmedRunningState)
                    ids.Add(item.Key);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids.ToArray();
        }

        public bool Start(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !_registry.Contains(id))
            {
                _logging.Warning("LifecycleManager rejected unknown widget ID: " + id + ".");
                return false;
            }

            Entry existing;
            if (_entries.TryGetValue(id, out existing) && IsRunning(id))
            {
                try
                {
                    existing.Instance.Activate();
                    return true;
                }
                catch (Exception ex)
                {
                    _logging.Error(
                        "Widget activation failed: " + id + ".",
                        ex);
                    return false;
                }
            }

            bool restarting = existing != null;
            var entry = existing ?? new Entry();
            _entries[id] = entry;
            try
            {
                entry.Instance = new WidgetInstance(
                    _registry.Create(id),
                    _scheduler,
                    _settings,
                    _logging,
                    new WidgetRuntimeContext(id, _services));
                entry.State = LifecycleState.Loaded;
                WireInstance(id, entry);
                entry.Instance.Start();
                _settings.SetWidgetEnabled(id, true);
                entry.State = LifecycleState.Running;
                _logging.Information("Widget lifecycle entered Running: " + id + ".");
                _events.Publish(
                    restarting
                        ? RuntimeEventType.WidgetRestarted
                        : RuntimeEventType.WidgetStarted,
                    id);
                OnChanged();
                return true;
            }
            catch (Exception ex)
            {
                Fail(id, entry, ex);
                return false;
            }
        }

        public bool Stop(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !_registry.Contains(id))
                return false;

            Entry entry;
            if (!_entries.TryGetValue(id, out entry) || !IsRunning(id))
            {
                _settings.SetWidgetEnabled(id, false);
                return true;
            }

            try
            {
                entry.Instance.Dispose();
                _settings.SetWidgetEnabled(id, false);
                CompleteStop(id, entry);
                return true;
            }
            catch (Exception ex)
            {
                Fail(id, entry, ex);
                return false;
            }
        }

        public bool Suspend(string id)
        {
            Entry entry;
            if (!_entries.TryGetValue(id, out entry) ||
                entry.State != LifecycleState.Running) return false;
            try
            {
                entry.Instance.Suspend();
                entry.State = LifecycleState.Suspended;
                _logging.Information("Widget lifecycle entered Suspended: " + id + ".");
                OnChanged();
                return true;
            }
            catch (Exception ex)
            {
                Fail(id, entry, ex);
                return false;
            }
        }

        public bool Resume(string id)
        {
            Entry entry;
            if (!_entries.TryGetValue(id, out entry) ||
                entry.State != LifecycleState.Suspended) return false;
            try
            {
                entry.Instance.Resume();
                entry.State = LifecycleState.Running;
                _logging.Information("Widget lifecycle resumed Running: " + id + ".");
                OnChanged();
                return true;
            }
            catch (Exception ex)
            {
                Fail(id, entry, ex);
                return false;
            }
        }

        public void BringWidgetsAboveXWidgetReborn(IntPtr rebornWindow)
        {
            uint widgetFlags = SwpNoSize | SwpNoMove |
                SwpNoActivate | SwpShowWindow;
            uint rebornFlags = SwpNoSize | SwpNoMove |
                SwpNoActivate;
            bool rebornValid = rebornWindow != IntPtr.Zero &&
                IsWindow(rebornWindow);
            uint rebornProcessId = 0;
            if (rebornValid)
                GetWindowThreadProcessId(
                    rebornWindow,
                    out rebornProcessId);

            var surfacesByHandle =
                new Dictionary<IntPtr, LayerSurface>();
            foreach (KeyValuePair<string, Entry> item in _entries)
            {
                Entry entry = item.Value;
                if (entry == null || entry.Instance == null)
                    continue;
                IntPtr handle = entry.Instance.LayerSurfaceHandle;
                if (handle == IntPtr.Zero || !IsWindow(handle))
                    continue;
                surfacesByHandle[handle] = new LayerSurface
                {
                    Id = item.Key,
                    Handle = handle,
                    Legacy = entry.Instance.IsLegacyExternal
                };
            }

            _logging.Debug(
                "Widget layer request: mode=" +
                EngineClient.GetWidgetLayerMode() +
                "; reborn=" + FormatHandle(rebornWindow) +
                "; valid=" + rebornValid +
                "; pid=" + rebornProcessId +
                "; widgets=" + surfacesByHandle.Count + ".");
            if (!rebornValid || surfacesByHandle.Count == 0)
                return;
            LogWindow("reborn-before", "Reborn", rebornWindow);

            var ordered = new List<LayerSurface>();
            EnumWindows(
                delegate(IntPtr window, IntPtr parameter)
                {
                    LayerSurface surface;
                    if (surfacesByHandle.TryGetValue(
                        window,
                        out surface))
                        ordered.Add(surface);
                    return true;
                },
                IntPtr.Zero);

            foreach (LayerSurface surface in ordered)
            {
                LogSurface("before", surface);
                // XWidget surfaces are layered windows. Moving an already
                // non-topmost layered surface through HWND_NOTOPMOST and then
                // immediately through the real chain insertion produces a
                // visible drop-and-restore frame. SetLayerMode already removes
                // WS_EX_TOPMOST when Above Reborn is selected, so retain the
                // proven native normalization but avoid the redundant legacy
                // transition.
                if (surface.Legacy &&
                    !IsTopMost(surface.Handle))
                {
                    _logging.Debug(
                        "Widget layer SetWindowPos skipped: caller=" +
                        "LifecycleManager.BringWidgetsAboveXWidgetReborn" +
                        "; id=" + surface.Id +
                        "; hwnd=" +
                        FormatHandle(surface.Handle) +
                        "; insertAfter=" +
                        FormatHandle(HwndNoTopMost) +
                        "; reason=legacy surface already non-topmost.");
                    continue;
                }
                ApplyWindowPosition(
                    surface.Id,
                    surface.Handle,
                    HwndNoTopMost,
                    widgetFlags);
            }

            // Build a deliberate top-to-bottom normal-band chain. SetWindowPos
            // places a window behind hWndInsertAfter, so the active Reborn
            // window is moved behind the last widget after the widgets have
            // been linked in their existing top-to-bottom order.
            IntPtr insertAfter = HwndTop;
            foreach (LayerSurface surface in ordered)
            {
                ApplyWindowPosition(
                    surface.Id,
                    surface.Handle,
                    insertAfter,
                    widgetFlags);
                insertAfter = surface.Handle;
            }
            ApplyWindowPosition(
                "Reborn",
                rebornWindow,
                insertAfter,
                rebornFlags);
            LogWindow("reborn-after", "Reborn", rebornWindow);

            foreach (LayerSurface surface in ordered)
                LogSurface("after", surface);
            _logging.Debug(
                "Widget layer chain completed: top=" +
                FormatHandle(ordered[0].Handle) +
                "; bottom=" +
                FormatHandle(ordered[ordered.Count - 1].Handle) +
                "; reborn=" + FormatHandle(rebornWindow) + ".");
        }

        public void PlaceNormalWidgetsBehind(IntPtr applicationWindow)
        {
            if (applicationWindow == IntPtr.Zero || !IsWindow(applicationWindow))
                return;

            uint flags = SwpNoSize | SwpNoMove | SwpNoActivate;
            foreach (KeyValuePair<string, Entry> item in _entries)
            {
                Entry entry = item.Value;
                if (entry == null || entry.Instance == null ||
                    entry.Instance.LayerMode == WidgetLayerMode.AlwaysOnTop)
                    continue;
                IntPtr handle = entry.Instance.LayerSurfaceHandle;
                if (handle == IntPtr.Zero || !IsWindow(handle))
                    continue;
                // Clear a stale native TOPMOST flag before anchoring an
                // ordinary widget behind an EmilyDesk application window.
                // This prevents one widget (most visibly Calendar) from
                // floating over the Dashboard while its peers remain behind.
                ApplyWindowPosition(
                    item.Key,
                    handle,
                    HwndNoTopMost,
                    flags);
                ApplyWindowPosition(
                    item.Key,
                    handle,
                    applicationWindow,
                    flags);
            }
            _logging.Debug(
                "Normal widget layer anchored behind application window " +
                FormatHandle(applicationWindow) + ".");
        }

        private static bool IsTopMost(IntPtr window)
        {
            long exStyle =
                GetWindowLongPtr(window, GwlExStyle)
                    .ToInt64();
            return (exStyle & WsExTopMost) != 0;
        }

        public void LogWidgetLayerSnapshot(
            IntPtr rebornWindow,
            string trigger)
        {
            _logging.Debug(
                "Widget layer diagnostic snapshot: trigger=" +
                trigger + "; mode=" +
                EngineClient.GetWidgetLayerMode() +
                "; reborn=" + FormatHandle(rebornWindow) +
                "; valid=" +
                (rebornWindow != IntPtr.Zero &&
                IsWindow(rebornWindow)) + ".");
            if (rebornWindow != IntPtr.Zero &&
                IsWindow(rebornWindow))
                LogWindow(
                    "diagnostic",
                    "Dashboard/Gallery",
                    rebornWindow);

            foreach (KeyValuePair<string, Entry> item in _entries)
            {
                Entry entry = item.Value;
                if (entry == null || entry.Instance == null)
                    continue;
                IntPtr handle =
                    entry.Instance.LayerSurfaceHandle;
                if (handle == IntPtr.Zero ||
                    !IsWindow(handle))
                    continue;
                LogWindow(
                    "diagnostic",
                    item.Key + "/" +
                    (entry.Instance.IsLegacyExternal
                        ? "legacy"
                        : "native"),
                    handle);
            }
        }

        private void ApplyWindowPosition(
            string id,
            IntPtr window,
            IntPtr insertAfter,
            uint flags)
        {
            NativeRect currentRectangle;
            GetWindowRect(
                window,
                out currentRectangle);
            uint processId;
            uint windowThreadId =
                GetWindowThreadProcessId(
                    window,
                    out processId);
            bool dragging =
                IsThreadInMoveSize(windowThreadId);
            long timestamp =
                Stopwatch.GetTimestamp();
            bool result = SetWindowPos(
                window,
                insertAfter,
                0,
                0,
                0,
                0,
                flags);
            int error = result ? 0 : Marshal.GetLastWin32Error();
            _logging.Debug(
                "Widget layer SetWindowPos: caller=LifecycleManager.ApplyWindowPosition" +
                "; id=" + id +
                "; hwnd=" + FormatHandle(window) +
                "; perfTicks=" + timestamp +
                "; processId=" + processId +
                "; windowThreadId=" +
                windowThreadId +
                "; callerThreadId=" +
                GetCurrentThreadId() +
                "; foreground=" +
                FormatHandle(GetForegroundWindow()) +
                "; insertAfter=" + FormatHandle(insertAfter) +
                "; flags=0x" + flags.ToString("X") +
                "; requestedRect=unchanged" +
                "; currentRect=" +
                FormatRectangle(currentRectangle) +
                "; mode=" +
                EngineClient.GetWidgetLayerMode() +
                "; dragging=" + dragging +
                "; changesPosition=" +
                ((flags & SwpNoMove) == 0 ||
                (flags & SwpNoSize) == 0) +
                "; changesZOrder=true" +
                "; result=" + result +
                (result ? "." : "; error=" + error + "."));
        }

        private void LogSurface(
            string stage,
            LayerSurface surface)
        {
            LogWindow(
                stage,
                surface.Id + "/" +
                (surface.Legacy ? "legacy" : "native"),
                surface.Handle);
        }

        private void LogWindow(
            string stage,
            string id,
            IntPtr handle)
        {
            IntPtr exStyle = GetWindowLongPtr(handle, GwlExStyle);
            IntPtr style = GetWindowLongPtr(handle, GwlStyle);
            var title = new StringBuilder(256);
            var className = new StringBuilder(128);
            GetWindowText(handle, title, title.Capacity);
            GetClassName(
                handle,
                className,
                className.Capacity);
            uint processId;
            uint threadId =
                GetWindowThreadProcessId(
                    handle,
                    out processId);
            NativeRect rectangle;
            GetWindowRect(handle, out rectangle);
            int cloakedValue;
            bool cloakedKnown =
                DwmGetWindowAttribute(
                    handle,
                    DwmwaCloaked,
                    out cloakedValue,
                    sizeof(int)) == 0;
            _logging.Debug(
                "Widget layer " + stage +
                ": id=" + id +
                "; hwnd=" + FormatHandle(handle) +
                "; pid=" + processId +
                "; threadId=" + threadId +
                "; class=" + className +
                "; title=\"" + title + "\"" +
                "; visible=" + IsWindowVisible(handle) +
                "; rect=" +
                FormatRectangle(rectangle) +
                "; cloaked=" +
                (cloakedKnown
                    ? (cloakedValue != 0).ToString()
                    : "unknown") +
                "; owner=" +
                FormatHandle(GetWindow(handle, GwOwner)) +
                "; parent=" +
                FormatHandle(GetParent(handle)) +
                "; exStyle=0x" +
                exStyle.ToInt64().ToString("X") +
                "; style=0x" +
                style.ToInt64().ToString("X") +
                "; topmost=" +
                ((exStyle.ToInt64() & WsExTopMost) != 0) +
                "; previous=" +
                FormatHandle(GetWindow(handle, GwHwndPrev)) +
                "; next=" +
                FormatHandle(GetWindow(handle, GwHwndNext)) +
                ".");
        }

        private static bool IsThreadInMoveSize(
            uint threadId)
        {
            if (threadId == 0)
                return false;
            var information = new GuiThreadInfo
            {
                Size =
                    Marshal.SizeOf(
                        typeof(GuiThreadInfo))
            };
            return GetGUIThreadInfo(
                    threadId,
                    ref information) &&
                (information.Flags & GuiInMoveSize) != 0;
        }

        private static string FormatRectangle(
            NativeRect rectangle)
        {
            return rectangle.Left + "," +
                rectangle.Top + "," +
                rectangle.Right + "," +
                rectangle.Bottom;
        }

        private static IntPtr GetWindowLongPtr(
            IntPtr window,
            int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(window, index)
                : GetWindowLong32(window, index);
        }

        private static string FormatHandle(IntPtr handle)
        {
            return "0x" + handle.ToInt64().ToString("X");
        }

        public void SetWidgetLayerMode(WidgetLayerMode mode)
        {
            foreach (KeyValuePair<string, Entry> item in _entries)
            {
                Entry entry = item.Value;
                if (entry != null && entry.Instance != null)
                    entry.Instance.SetLayerMode(mode);
            }
            _logging.Information("Global widget layer mode changed to " + mode + ".");
        }

        public void Restore()
        {
            _settings.ApplyMigrations();
            foreach (WidgetDescriptor descriptor in _registry.GetInstalledWidgets())
            {
                bool enabled = _settings.IsWidgetEnabled(
                    descriptor.Id,
                    _registry.IsEnabledByDefault(descriptor.Id));
                if (!enabled) continue;

                if (_registry.IsLegacy(descriptor.Id))
                {
                    // Legacy widgets are external sessions owned by xwidget.exe.
                    // A persisted enabled flag may survive a crash or forced stop.
                    // Clear it without launching, closing, or duplicating the widget.
                    _settings.SetWidgetEnabled(descriptor.Id, false);
                    _logging.Information(
                        "Startup reconciliation detached stale legacy state: " +
                        descriptor.Id + ".");
                    continue;
                }

                Start(descriptor.Id);
            }
        }

        private void WireInstance(string id, Entry entry)
        {
            entry.Instance.SuspendRequested += delegate { Suspend(id); };
            entry.Instance.ResumeRequested += delegate { Resume(id); };
            entry.Instance.UserCloseRequested += delegate
            {
                if (!_disposing) _settings.SetWidgetEnabled(id, false);
            };
            entry.Instance.Closed += delegate { CompleteStop(id, entry); };
        }

        private void CompleteStop(string id, Entry entry)
        {
            if (entry.State == LifecycleState.Stopped ||
                entry.State == LifecycleState.Failed) return;
            entry.Instance = null;
            entry.State = LifecycleState.Stopped;
            _logging.Information("Widget lifecycle entered Stopped: " + id + ".");
            _events.Publish(RuntimeEventType.WidgetStopped, id);
            OnChanged();
        }

        private void Fail(string id, Entry entry, Exception error)
        {
            entry.State = LifecycleState.Failed;
            _settings.SetWidgetEnabled(id, false);
            WidgetInstance instance = entry.Instance;
            entry.Instance = null;
            if (instance != null)
            {
                try { instance.Dispose(); }
                catch (Exception cleanupError)
                {
                    _logging.Error(
                        "Widget failure cleanup also failed: " + id + ".",
                        cleanupError);
                }
            }
            _logging.Error(
                "Widget lifecycle entered Failed: " + id + ".",
                error);
            _events.Publish(RuntimeEventType.WidgetCrashed, id, error);
            OnChanged();
        }

        private void RegistryChanged(object sender, EventArgs e)
        {
            foreach (KeyValuePair<string, Entry> item in
                new List<KeyValuePair<string, Entry>>(_entries))
            {
                if (_registry.Contains(item.Key)) continue;
                item.Value.State = LifecycleState.Stopped;
                if (item.Value.Instance != null)
                {
                    try { item.Value.Instance.Dispose(); }
                    catch (Exception ex)
                    {
                        _logging.Error(
                            "Removed widget cleanup failed: " + item.Key + ".",
                            ex);
                    }
                }
                item.Value.Instance = null;
            }
            OnChanged();
        }

        private void OnChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposing) return;
            _disposing = true;
            _registry.Changed -= RegistryChanged;
            foreach (KeyValuePair<string, Entry> item in _entries)
            {
                Entry entry = item.Value;
                if (entry.Instance == null) continue;

                if (entry.Instance.IsLegacyExternal)
                {
                    // A legacy widget belongs to xwidget.exe after launch.
                    // Do not close it and do not restore it on Reborn restart.
                    _settings.SetWidgetEnabled(item.Key, false);
                    entry.Instance.DetachLegacyForHostShutdown();
                }
                else
                {
                    entry.Instance.Dispose();
                }
            }
            _entries.Clear();
            _logging.Debug("LifecycleManager disposed.");
        }
    }
}
