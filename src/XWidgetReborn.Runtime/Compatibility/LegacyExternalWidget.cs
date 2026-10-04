using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Runtime.Compatibility
{
    internal sealed class LegacyExternalWidget : IWidget
    {
        private readonly string _manifestPath;
        private readonly LegacyWidgetManifest _manifest;
        private IntPtr _surfaceHandle;
        private WidgetLayerMode _layerMode = WidgetLayerMode.AboveXWidgetReborn;

        private const uint WmClose = 0x0010;
        private const int SwRestore = 9;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpShowWindow = 0x0040;
        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        private delegate bool EnumWindowsCallback(
            IntPtr window,
            IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(
            EnumWindowsCallback callback,
            IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(
            IntPtr window,
            StringBuilder className,
            int maximumCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr window,
            out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AllowSetForegroundWindow(int processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(
            IntPtr window,
            int command);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(
            IntPtr window,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        public LegacyExternalWidget(
            string id,
            string manifestPath,
            LegacyWidgetManifest manifest)
        {
            Id = id;
            _manifestPath = manifestPath;
            _manifest = manifest;
        }

        public string Id { get; private set; }
        public string Name { get { return _manifest.Name; } }
        public string Description { get { return _manifest.Description; } }
        public string Version { get { return _manifest.Version; } }
        public Size DefaultSize
        {
            get { return new Size(_manifest.Width, _manifest.Height); }
        }
        public Point DefaultLocation
        {
            get { return new Point(_manifest.Left, _manifest.Top); }
        }
        public WidgetUpdateRate UpdateRate
        {
            get { return WidgetUpdateRate.Minute; }
        }

        public void Start(Action invalidate)
        {
            OpenSurface();
        }

        public void Activate()
        {
            if (HasVisibleSurface)
                BringSurfaceForward();
            else
                OpenSurface();
        }

        public void Tick(DateTime now) { }
        public void Pause() { }
        public void Resume() { }
        public void Render(Graphics graphics, Rectangle bounds) { }
        public void Dispose()
        {
            CloseSurface();
        }

        internal void Detach()
        {
            // Reborn is disconnecting from the compatibility session.
            // The surface remains owned by xwidget.exe and must not be closed.
            _surfaceHandle = IntPtr.Zero;
        }

        internal bool HasVisibleSurface
        {
            get
            {
                return _surfaceHandle != IntPtr.Zero &&
                    IsWindow(_surfaceHandle) &&
                    IsWindowVisible(_surfaceHandle);
            }
        }

        internal IntPtr SurfaceHandle
        {
            get { return HasVisibleSurface ? _surfaceHandle : IntPtr.Zero; }
        }


        internal void SetLayerMode(WidgetLayerMode mode)
        {
            _layerMode = mode;
            if (!HasVisibleSurface) return;
            if (mode == WidgetLayerMode.Hidden)
            {
                ShowWindow(_surfaceHandle, 0);
                return;
            }
            ShowWindow(_surfaceHandle, SwRestore);
            uint flags = SwpNoMove | SwpNoSize | SwpShowWindow;
            SetWindowPos(_surfaceHandle,
                mode == WidgetLayerMode.AlwaysOnTop ? HwndTopMost : HwndNoTopMost,
                0, 0, 0, 0, flags);
        }

        internal void BringAboveXWidgetReborn(IntPtr rebornWindow)
        {
            if (_layerMode != WidgetLayerMode.AboveXWidgetReborn || !HasVisibleSurface)
                return;
            // Raise into the normal (non-topmost) Z-order band. Passing the
            // Reborn HWND as hWndInsertAfter placed the widget below it.
            uint flags = SwpNoMove | SwpNoSize | SwpShowWindow | 0x0010;
            SetWindowPos(_surfaceHandle, HwndNoTopMost, 0, 0, 0, 0, flags);
            SetWindowPos(_surfaceHandle, HwndTop, 0, 0, 0, 0, flags);
        }
        private void OpenSurface()
        {
            HashSet<IntPtr> existing = GetVisibleSurfaces();
            SendWidgetCommand("openWidget", _manifestPath);

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            do
            {
                foreach (IntPtr window in GetVisibleSurfaces())
                    if (!existing.Contains(window))
                    {
                        _surfaceHandle = window;
                        BringSurfaceForward();
                        return;
                    }
                Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline);

            throw new InvalidOperationException(
                "The original XWidget host did not create a visible surface " +
                "for " + Name + ".");
        }

        private void BringSurfaceForward()
        {
            if (!HasVisibleSurface)
                return;

            uint processId;
            GetWindowThreadProcessId(
                _surfaceHandle,
                out processId);
            if (processId > 0)
                AllowSetForegroundWindow((int)processId);

            uint flags = SwpNoMove | SwpNoSize | SwpShowWindow;
            ShowWindow(_surfaceHandle, SwRestore);
            SetWindowPos(
                _surfaceHandle,
                HwndTopMost,
                0,
                0,
                0,
                0,
                flags);
            SetWindowPos(
                _surfaceHandle,
                HwndNoTopMost,
                0,
                0,
                0,
                0,
                flags);
            SetForegroundWindow(_surfaceHandle);
        }

        private void CloseSurface()
        {
            if (_surfaceHandle == IntPtr.Zero ||
                !IsWindow(_surfaceHandle))
                return;

            if (!PostMessage(
                _surfaceHandle,
                WmClose,
                IntPtr.Zero,
                IntPtr.Zero))
                throw new InvalidOperationException(
                    "The original XWidget surface could not be closed.");

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (IsWindow(_surfaceHandle) &&
                DateTime.UtcNow < deadline)
                Thread.Sleep(100);

            if (IsWindow(_surfaceHandle))
                throw new InvalidOperationException(
                    "The original XWidget surface did not close.");

            _surfaceHandle = IntPtr.Zero;
        }

        private static HashSet<IntPtr> GetVisibleSurfaces()
        {
            var processIds = new HashSet<uint>();
            foreach (Process process in Process.GetProcessesByName("xwidget"))
            {
                try { processIds.Add((uint)process.Id); }
                finally { process.Dispose(); }
            }

            var windows = new HashSet<IntPtr>();
            EnumWindows(
                delegate(IntPtr window, IntPtr parameter)
                {
                    uint processId;
                    GetWindowThreadProcessId(window, out processId);
                    if (!processIds.Contains(processId) ||
                        !IsWindowVisible(window))
                        return true;

                    var className = new StringBuilder(64);
                    GetClassName(
                        window,
                        className,
                        className.Capacity);
                    if (string.Equals(
                        className.ToString(),
                        "TfrmXWidgetXUL",
                        StringComparison.OrdinalIgnoreCase))
                        windows.Add(window);
                    return true;
                },
                IntPtr.Zero);
            return windows;
        }

        private static void SendWidgetCommand(
            string command,
            string target)
        {
            LegacyRuntimeManager.ExecuteWidgetCommand(command, target);
        }

    }
}
