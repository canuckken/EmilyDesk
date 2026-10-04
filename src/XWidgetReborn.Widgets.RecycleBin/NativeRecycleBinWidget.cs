using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Widgets.RecycleBin
{
    public sealed class NativeRecycleBinWidget :
        IWidget,
        IRuntimeAwareWidget,
        IOfficialWidget,
        IWidgetPointerInput,
        IWidgetDoubleClickInput,
        IWidgetFileDropTarget,
        IWidgetThemeProvider
    {
        private const int BaseWidth = 240;
        private const int BaseHeight = 224;
        private const uint FileOperationDelete = 0x0003;
        private const ushort AllowUndo = 0x0040;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShellFileOperation
        {
            public IntPtr Window;
            public uint Operation;
            [MarshalAs(UnmanagedType.LPWStr)] public string From;
            [MarshalAs(UnmanagedType.LPWStr)] public string To;
            public ushort Flags;
            [MarshalAs(UnmanagedType.Bool)] public bool Aborted;
            public IntPtr NameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string ProgressTitle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RecycleBinInfo
        {
            public int Size;
            public long TotalBytes;
            public long ItemCount;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(
            string rootPath, ref RecycleBinInfo info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(
            IntPtr window, string rootPath, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(
            ref ShellFileOperation operation);

        private Action _invalidate;
        private IWidgetHostContext _host;
        private IRuntimeContext _runtime;

        public void AttachRuntime(IRuntimeContext runtime)
        {
            if (_runtime != null) _runtime.Events.Published -= DesignerSaved;
            _runtime = runtime;
            if (_runtime != null) _runtime.Events.Published += DesignerSaved;
        }

        private void DesignerSaved(object sender, WidgetRuntimeEventArgs e)
        {
            if (e != null && e.Topic == "designer.saved") RequestInvalidate();
        }
        private bool _paused;
        private bool _hovered;
        private bool _isFull;
        private long _itemCount;
        private long _totalBytes;
        private string _appearance = "Modern";

        public string Id { get { return "native.recyclebin"; } }
        public string Name { get { return "Recycle Bin"; } }
        public string Description
        {
            get { return "Official themed Windows Recycle Bin widget for EmilyDesk."; }
        }
        public string Version { get { return "1.0.0"; } }
        public Size DefaultSize { get { return new Size(BaseWidth, BaseHeight); } }
        public Point DefaultLocation { get { return new Point(60, 470); } }
        public WidgetUpdateRate UpdateRate { get { return WidgetUpdateRate.Second; } }

        public IEnumerable<string> Themes
        {
            get { return EmilyDeskThemeCatalog.Names; }
        }

        public string Theme
        {
            get { return _appearance; }
            set
            {
                _appearance = EmilyDeskThemeCatalog.Normalize(value);
                SaveSettings();
                ApplyPresentation();
                RequestInvalidate();
            }
        }

        public void AttachHost(IWidgetHostContext host)
        {
            _host = host;
            if (_host == null) return;
            _appearance = EmilyDeskThemeCatalog.Normalize(
                _host.GetSetting("appearance", "Modern"));
            ApplyPresentation();
            RefreshState();
        }

        public void Start(Action invalidate)
        {
            _invalidate = invalidate;
            RefreshState();
            RequestInvalidate();
        }

        public void Tick(DateTime now)
        {
            if (!_paused) RefreshState();
        }

        public void Pause() { _paused = true; }

        public void Resume()
        {
            _paused = false;
            RefreshState();
            RequestInvalidate();
        }

        public void Render(Graphics graphics, Rectangle bounds)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            GraphicsState state = graphics.Save();
            float scale = Math.Min(
                bounds.Width / (float)BaseWidth,
                bounds.Height / (float)BaseHeight);
            float left = bounds.Left +
                (bounds.Width - BaseWidth * scale) / 2F;
            float top = bounds.Top +
                (bounds.Height - BaseHeight * scale) / 2F;
            graphics.TranslateTransform(left, top);
            graphics.ScaleTransform(scale, scale);
            try
            {
                if (DrawDesignerLayout(graphics)) return;
                EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_appearance);
                if (_hovered)
                {
                    using (var glow = new SolidBrush(Color.FromArgb(
                        50, theme.Accent.R, theme.Accent.G, theme.Accent.B)))
                        graphics.FillEllipse(glow, 20F, 8F, 200F, 218F);
                }
                Image skin = ThemeSkinCache.Get(SkinPath());
                graphics.DrawImage(skin,
                    new RectangleF(8F, 0F, 224F, 224F));
            }
            finally { graphics.Restore(state); }
        }



        public IEnumerable<WidgetMenuCommand> GetMenuCommands()
        {
            return new[]
            {
                new WidgetMenuCommand("Edit in Designer", false, OpenDesigner),
                new WidgetMenuCommand("Open Recycle Bin", false,
                    OpenRecycleBin),
                new WidgetMenuCommand("Empty Recycle Bin...", false,
                    EmptyRecycleBin),
                new WidgetMenuCommand("Refresh Recycle Bin", false,
                    delegate
                    {
                        RefreshState();
                        RequestInvalidate();
                    })
            };
        }

        public void ShowSettings()
        {
            using (var form = new Form())
            {
                form.Text = "Recycle Bin Widget Settings";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(410, 165);
                form.Font = new Font("Segoe UI", 9F);
                form.Controls.Add(new Label
                {
                    Text = "Theme", Location = new Point(24, 31), AutoSize = true
                });
                var themes = new ComboBox
                {
                    Location = new Point(145, 27), Width = 220,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                themes.Items.AddRange(new List<string>(
                    EmilyDeskThemeCatalog.Names).ToArray());
                themes.SelectedItem = _appearance;
                form.Controls.Add(themes);
                var ok = new Button
                {
                    Text = "Save", Location = new Point(145, 95),
                    Size = new Size(105, 34), DialogResult = DialogResult.OK
                };
                var cancel = new Button
                {
                    Text = "Cancel", Location = new Point(260, 95),
                    Size = new Size(105, 34), DialogResult = DialogResult.Cancel
                };
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog() != DialogResult.OK) return;
                _appearance = EmilyDeskThemeCatalog.Normalize(
                    Convert.ToString(themes.SelectedItem));
                SaveSettings();
                ApplyPresentation();
                RequestInvalidate();
            }
        }

        public void ResetSettings()
        {
            _appearance = "Modern";
            if (_host != null)
            {
                _host.Scale = 1F;
                _host.Opacity = 1D;
            }
            SaveSettings();
            ApplyPresentation();
            RefreshState();
            RequestInvalidate();
        }

        public bool PointerDown(Point location, WidgetPointerButton button)
        {
            return false;
        }

        public void PointerMove(Point location)
        {
            if (_hovered) return;
            _hovered = true;
            RequestInvalidate();
        }

        public void PointerUp(Point location, WidgetPointerButton button) { }

        public void PointerLeave()
        {
            if (!_hovered) return;
            _hovered = false;
            RequestInvalidate();
        }

        public bool PointerDoubleClick(
            Point location, WidgetPointerButton button)
        {
            if (button != WidgetPointerButton.Left) return false;
            OpenRecycleBin();
            return true;
        }

        public bool CanAcceptFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return false;
            foreach (string path in paths)
                if (string.IsNullOrWhiteSpace(path) ||
                    (!File.Exists(path) && !Directory.Exists(path)))
                    return false;
            return true;
        }

        public void DropFiles(string[] paths)
        {
            if (!CanAcceptFiles(paths)) return;
            var qualifiedPaths = new string[paths.Length];
            for (int index = 0; index < paths.Length; index++)
                qualifiedPaths[index] = Path.GetFullPath(paths[index]);
            var operation = new ShellFileOperation
            {
                Window = IntPtr.Zero,
                Operation = FileOperationDelete,
                From = string.Join("\0", qualifiedPaths) + "\0\0",
                To = null,
                Flags = AllowUndo,
                Aborted = false,
                NameMappings = IntPtr.Zero,
                ProgressTitle = null
            };
            int result = SHFileOperation(ref operation);
            if (result != 0 && !operation.Aborted)
                MessageBox.Show(
                    "Windows could not move one or more items to the Recycle Bin.",
                    "EmilyDesk Recycle Bin", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            RefreshState();
            RequestInvalidate();
        }

        private void OpenRecycleBin()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "shell:RecycleBinFolder",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "EmilyDesk Recycle Bin",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void EmptyRecycleBin()
        {
            SHEmptyRecycleBin(IntPtr.Zero, null, 0);
            RefreshState();
            RequestInvalidate();
        }



        private void RefreshState()
        {
            var info = new RecycleBinInfo
            {
                Size = Marshal.SizeOf(typeof(RecycleBinInfo))
            };
            if (SHQueryRecycleBin(null, ref info) != 0) return;
            bool full = info.ItemCount > 0;
            if (_isFull == full && _itemCount == info.ItemCount &&
                _totalBytes == info.TotalBytes) return;
            _isFull = full;
            _itemCount = Math.Max(0L, info.ItemCount);
            _totalBytes = Math.Max(0L, info.TotalBytes);
            RequestInvalidate();
        }



        private string SkinPath()
        {
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_appearance);
            string imported = theme.Asset(_isFull ? "recycleFull" : "recycleEmpty");
            if (theme.IsImported && !string.IsNullOrEmpty(imported)) return imported;
            string folder = _appearance.Replace(" ", string.Empty);
            string prefix = _appearance == "Art Deco" ? "art-deco"
                : _appearance == "Botanical Nature" ? "botanical"
                : _appearance == "Woodland Nature" ? "woodland"
                : _appearance.ToLowerInvariant();
            return Path.Combine("Assets", "Themes", folder,
                prefix + "-recycle-bin-" +
                (_isFull ? "full" : "empty") + ".png");
        }

        private bool DrawDesignerLayout(Graphics graphics)
        {
            SavedDesignerLayout layout = SavedDesignerLayout.Current(_appearance, "recyclebin");
            if (layout == null || layout.Elements == null || layout.CanvasWidth <= 0 || layout.CanvasHeight <= 0)
                return false;
            GraphicsState state = graphics.Save();
            try
            {
                graphics.ScaleTransform(BaseWidth / (float)layout.CanvasWidth, BaseHeight / (float)layout.CanvasHeight);
                DesignerLayerPainter.DrawImage(graphics, layout.BackgroundImage,
                    new RectangleF(0, 0, layout.CanvasWidth, layout.CanvasHeight), 1F);
                int surface = _isFull ? 1 : 0;
                foreach (DesignerLayer layer in layout.Elements)
                    DesignerLayerPainter.DrawOnSurface(graphics, layer, null, null, surface);
            }
            finally { graphics.Restore(state); }
            return true;
        }

        private void OpenDesigner()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmilyDesk.Designer.exe");
                if (!File.Exists(path)) throw new FileNotFoundException("EmilyDesk Designer is not installed.");
                Process.Start(new ProcessStartInfo { FileName = path,
                    Arguments = "--widget recyclebin --theme \"" + _appearance + "\"",
                    WorkingDirectory = Path.GetDirectoryName(path), UseShellExecute = true });
            }
            catch (Exception error) { MessageBox.Show(error.Message, "EmilyDesk Designer"); }
        }

        private void ApplyPresentation()
        {
            if (_host == null) return;
            _host.SetPreferredSize(DefaultSize);
            _host.SetWindowShape(WidgetWindowShape.AlphaRectangle);
        }

        private void SaveSettings()
        {
            if (_host == null) return;
            _host.SetSetting("appearance", _appearance);
        }

        private void RequestInvalidate()
        {
            if (_invalidate != null) _invalidate();
            if (_host != null) _host.Invalidate();
        }

        private static GraphicsPath RoundedRectangle(
            RectangleF bounds, float radius)
        {
            float diameter = Math.Min(radius * 2F,
                Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top,
                diameter, diameter, 180F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Top,
                diameter, diameter, 270F, 90F);
            path.AddArc(bounds.Right - diameter,
                bounds.Bottom - diameter,
                diameter, diameter, 0F, 90F);
            path.AddArc(bounds.Left,
                bounds.Bottom - diameter,
                diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        public void Dispose()
        {
            AttachRuntime(null);
            _invalidate = null;
            _host = null;
        }
    }
}
