using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal enum EmilyDeskDockEdge
    {
        Bottom,
        Top,
        Left,
        Right
    }

    internal sealed class EmilyDeskDockItem
    {
        public string Label { get; set; }
        public string Target { get; set; }
        public string Arguments { get; set; }
        public string CustomIconPath { get; set; }
    }

    internal sealed class EmilyDeskDockSettings
    {
        public string Theme { get; set; }
        public string Edge { get; set; }
        public int IconSize { get; set; }
        public bool AutoHide { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool StartWithEmilyDesk { get; set; }
        public bool StartupPreferenceInitialized { get; set; }
        public int TintArgb { get; set; }
        public int LightShade { get; set; }
        public List<EmilyDeskDockItem> Items { get; set; }

        public static EmilyDeskDockSettings CreateDefaults()
        {
            var settings = new EmilyDeskDockSettings
            {
                Theme = "Industrial",
                Edge = EmilyDeskDockEdge.Bottom.ToString(),
                IconSize = 56,
                AutoHide = false,
                AlwaysOnTop = true,
                StartWithEmilyDesk = true,
                StartupPreferenceInitialized = true,
                TintArgb = 0,
                LightShade = 0,
                Items = new List<EmilyDeskDockItem>()
            };
            settings.Items.Add(new EmilyDeskDockItem
            {
                Label = "EmilyDesk",
                Target = Application.ExecutablePath,
                Arguments = string.Empty,
                CustomIconPath = string.Empty
            });
            settings.Items.Add(new EmilyDeskDockItem
            {
                Label = "File Explorer",
                Target = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "explorer.exe"),
                Arguments = string.Empty,
                CustomIconPath = string.Empty
            });
            string documents = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents))
                settings.Items.Add(new EmilyDeskDockItem
                {
                    Label = "Documents",
                    Target = documents,
                    Arguments = string.Empty,
                    CustomIconPath = string.Empty
                });
            return settings;
        }
    }

    internal static class EmilyDeskDockStore
    {
        private static readonly object Sync = new object();
        private static readonly JavaScriptSerializer Json =
            new JavaScriptSerializer();

        public static string FolderPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    AppConstants.ProductName,
                    "Dock");
            }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(FolderPath, "dock.json"); }
        }

        public static EmilyDeskDockSettings Load()
        {
            lock (Sync)
            {
                EmilyDeskDockSettings settings = null;
                try
                {
                    if (File.Exists(SettingsPath))
                        settings = Json.Deserialize<EmilyDeskDockSettings>(
                            File.ReadAllText(SettingsPath));
                }
                catch
                {
                    settings = null;
                }
                if (settings == null)
                    settings = EmilyDeskDockSettings.CreateDefaults();
                Normalize(settings);
                return settings;
            }
        }

        public static void Save(EmilyDeskDockSettings settings)
        {
            if (settings == null) return;
            lock (Sync)
            {
                Normalize(settings);
                Directory.CreateDirectory(FolderPath);
                string temporary = SettingsPath + ".tmp";
                File.WriteAllText(temporary, Json.Serialize(settings));
                File.Copy(temporary, SettingsPath, true);
                File.Delete(temporary);
            }
        }

        private static void Normalize(EmilyDeskDockSettings settings)
        {
            if (!IsSupportedTheme(settings.Theme))
                settings.Theme = "Industrial";
            if (!settings.StartupPreferenceInitialized)
            {
                // v81 and earlier wrote false by default. Migrate those
                // installations once so the dock loads with EmilyDesk.
                settings.StartWithEmilyDesk = true;
                settings.StartupPreferenceInitialized = true;
            }
            EmilyDeskDockEdge parsed;
            if (!Enum.TryParse(settings.Edge, true, out parsed))
                parsed = EmilyDeskDockEdge.Bottom;
            settings.Edge = parsed.ToString();
            settings.IconSize = Math.Max(36, Math.Min(96,
                settings.IconSize <= 0 ? 56 : settings.IconSize));
            settings.LightShade = Math.Max(-60, Math.Min(60,
                settings.LightShade));
            if (settings.Items == null)
                settings.Items = new List<EmilyDeskDockItem>();
            for (int i = settings.Items.Count - 1; i >= 0; i--)
            {
                EmilyDeskDockItem item = settings.Items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Target))
                {
                    settings.Items.RemoveAt(i);
                    continue;
                }
                item.Target = item.Target.Trim();
                item.Label = string.IsNullOrWhiteSpace(item.Label)
                    ? FriendlyName(item.Target)
                    : item.Label.Trim();
                item.Arguments = item.Arguments ?? string.Empty;
                item.CustomIconPath = item.CustomIconPath ?? string.Empty;
            }
        }

        internal static bool IsSupportedTheme(string theme)
        {
            string normalized = EmilyDeskThemeCatalog.Normalize(theme);
            foreach (string name in EmilyDeskThemeCatalog.Names)
                if (string.Equals(normalized, name,
                    StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static string NormalizeTheme(string theme)
        {
            string normalized = EmilyDeskThemeCatalog.Normalize(theme);
            return normalized == "Modern" && !string.Equals(theme, "Modern",
                StringComparison.OrdinalIgnoreCase) ? "Industrial" : normalized;
        }

        internal static string FriendlyName(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return "Dock item";
            Uri uri;
            if (Uri.TryCreate(target, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps))
                return string.IsNullOrWhiteSpace(uri.Host)
                    ? target : uri.Host;
            string trimmed = target.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string name = Path.GetFileNameWithoutExtension(trimmed);
            return string.IsNullOrWhiteSpace(name) ? target : name;
        }
    }

    internal static class EmilyDeskDockController
    {
        private static IndustrialDockForm _dock;

        public static bool IsRunning
        {
            get { return _dock != null && !_dock.IsDisposed; }
        }

        public static string CurrentThemeName
        {
            get
            {
                return IsRunning
                    ? _dock.ThemeName
                    : EmilyDeskDockStore.NormalizeTheme(
                        EmilyDeskDockStore.Load().Theme);
            }
        }

        public static string StatusText
        {
            get
            {
                return IsRunning
                    ? "Running — " + _dock.ThemeName
                    : "Not running";
            }
        }

        public static void StartIfConfigured()
        {
            EmilyDeskDockSettings settings = EmilyDeskDockStore.Load();
            if (settings.StartWithEmilyDesk)
                ShowDock();
        }

        public static void ShowDock()
        {
            if (_dock == null || _dock.IsDisposed)
            {
                IndustrialDockForm created = new IndustrialDockForm(
                    EmilyDeskDockStore.Load());
                created.FormClosed += delegate
                {
                    if (ReferenceEquals(_dock, created))
                        _dock = null;
                };
                _dock = created;
                created.Show();
            }
            else if (!_dock.Visible)
                _dock.Show();
            _dock.Reveal();
            _dock.BringToFront();
        }

        public static void ShowDockForTheme(string theme)
        {
            EmilyDeskDockSettings settings = EmilyDeskDockStore.Load();
            settings.Theme = EmilyDeskDockStore.NormalizeTheme(theme);
            EmilyDeskDockStore.Save(settings);
            if (IsRunning)
            {
                _dock.ApplySettings(settings);
                _dock.Reveal();
                _dock.BringToFront();
                return;
            }
            ShowDock();
        }

        public static void CloseDock()
        {
            if (_dock == null) return;
            IndustrialDockForm dock = _dock;
            _dock = null;
            if (!dock.IsDisposed)
                dock.CloseDock();
        }

        public static void ShowSettings(IWin32Window owner)
        {
            try
            {
                EmilyDeskDockSettings settings = EmilyDeskDockStore.Load();
                using (var form = new EmilyDeskDockSettingsForm(settings))
                {
                    // Do not make the dock itself the modal owner. A hidden or
                    // closing borderless owner can take the entire application
                    // down with its owned settings window on some Windows 10
                    // configurations.
                    Form ownerForm = owner as Form;
                    bool useOwner = ownerForm != null &&
                        !(ownerForm is IndustrialDockForm) &&
                        !ownerForm.IsDisposed && ownerForm.IsHandleCreated &&
                        ownerForm.Visible;
                    if (!useOwner)
                        form.StartPosition = FormStartPosition.CenterScreen;
                    DialogResult result = useOwner
                        ? form.ShowDialog(ownerForm)
                        : form.ShowDialog();
                    if (result != DialogResult.OK)
                        return;
                }
                if (IsRunning)
                    _dock.ApplySettings(EmilyDeskDockStore.Load());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Dock Settings could not be opened.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk Dock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        public static void ApplyCurrentSettings()
        {
            if (IsRunning)
                _dock.ApplySettings(EmilyDeskDockStore.Load());
        }
    }

    internal sealed class IndustrialDockForm : Form
    {
        private const int OuterPadding = 16;
        private const int ItemGap = 12;
        private const string BotanicalDockSkinPath =
            @"Assets\Themes\BotanicalNature\botanical-dock-skin.png";
        private readonly Timer _autoHideTimer;
        private readonly DockHoverLabelForm _hoverLabel;
        private readonly List<Image> _icons = new List<Image>();
        private EmilyDeskDockSettings _settings;
        private List<RectangleF> _itemBounds = new List<RectangleF>();
        private RectangleF _addBounds;
        private int _hoverIndex = -1;
        private int _mouseDownIndex = -1;
        private int _dragIndex = -1;
        private int _dropIndex = -1;
        private Point _mouseDownPoint;
        private bool _dragStarted;
        private bool _closing;
        private DateTime _lastInteraction = DateTime.UtcNow;
        private Point _shownLocation;
        private Point _hiddenLocation;
        private Point _targetLocation;

        public IndustrialDockForm(EmilyDeskDockSettings settings)
        {
            Text = "EmilyDesk Industrial Dock";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            AllowDrop = true;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(29, 29, 28);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            _hoverLabel = new DockHoverLabelForm();
            _autoHideTimer = new Timer { Interval = 35 };
            _autoHideTimer.Tick += AutoHideTick;

            MouseDown += DockMouseDown;
            MouseMove += DockMouseMove;
            MouseUp += DockMouseUp;
            MouseLeave += delegate
            {
                if (!_dragStarted)
                {
                    _hoverIndex = -1;
                    _hoverLabel.HideLabel();
                    Invalidate();
                }
            };
            DragEnter += DockDragEnter;
            DragDrop += DockDragDrop;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape && _dragStarted)
                {
                    ResetDrag();
                    Invalidate();
                }
            };
            FormClosed += delegate
            {
                _autoHideTimer.Stop();
                SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
                DisposeIcons();
                _hoverLabel.Dispose();
                _autoHideTimer.Dispose();
            };
            SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
            ApplySettings(settings);
            _autoHideTimer.Start();
        }

        public string ThemeName
        {
            get
            {
                return _settings == null
                    ? "Industrial"
                    : EmilyDeskDockStore.NormalizeTheme(_settings.Theme);
            }
        }

        private bool IsSteampunk
        {
            get
            {
                return string.Equals(ThemeName, "Steampunk",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private bool IsArtDeco
        {
            get
            {
                return string.Equals(ThemeName, "Art Deco",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private bool IsVintage
        {
            get { return ThemeIs("Vintage"); }
        }

        private bool IsModern
        {
            get { return ThemeIs("Modern"); }
        }

        private bool IsBotanical
        {
            get { return ThemeIs("Botanical Nature"); }
        }

        private bool IsWoodland
        {
            get { return ThemeIs("Woodland Nature"); }
        }

        private bool IsImportedTheme
        {
            get { return EmilyDeskThemeCatalog.Get(ThemeName).IsImported; }
        }

        private bool ThemeIs(string theme)
        {
            return string.Equals(ThemeName, theme,
                StringComparison.OrdinalIgnoreCase);
        }

        public void ApplySettings(EmilyDeskDockSettings settings)
        {
            _settings = settings ?? EmilyDeskDockSettings.CreateDefaults();
            _settings.Theme = EmilyDeskDockStore.NormalizeTheme(
                _settings.Theme);
            Text = "EmilyDesk " + ThemeName + " Dock";
            BackColor = IsBotanical
                ? Color.FromArgb(174, 180, 139)
                : Color.FromArgb(29, 29, 28);
            TopMost = _settings.AlwaysOnTop;
            _hoverLabel.ApplyTheme(ThemeName);
            ReloadIcons();
            LayoutDock();
            Invalidate();
        }

        public void Reveal()
        {
            _lastInteraction = DateTime.UtcNow;
            _targetLocation = _shownLocation;
            Location = _shownLocation;
        }

        public void CloseDock()
        {
            _closing = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_closing && e.CloseReason == CloseReason.UserClosing)
            {
                _closing = true;
            }
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            if (IsImportedTheme)
                DrawImportedBackground(graphics);
            else if (IsVintage)
                DrawVintageBackground(graphics);
            else if (IsModern)
                DrawModernBackground(graphics);
            else if (IsBotanical)
                DrawBotanicalBackground(graphics);
            else if (IsWoodland)
                DrawWoodlandBackground(graphics);
            else if (IsArtDeco)
                DrawArtDecoBackground(graphics);
            else if (IsSteampunk)
                DrawSteampunkBackground(graphics);
            else
                DrawIndustrialBackground(graphics);

            ApplyColourAdjustment(graphics);

            for (int i = 0; i < _itemBounds.Count; i++)
            {
                RectangleF bounds = DisplayBounds(i, _itemBounds[i]);
                if (i == _hoverIndex)
                {
                    using (var glow = new SolidBrush(DockGlowColor()))
                        graphics.FillEllipse(glow,
                            RectangleF.Inflate(bounds, 5F, 5F));
                }
                if (i < _icons.Count && _icons[i] != null)
                    graphics.DrawImage(_icons[i], bounds);
                else
                    DrawMissingIcon(graphics, bounds);
            }
            DrawAddButton(graphics, _addBounds,
                _hoverIndex == _settings.Items.Count);
            if (_dragStarted && _dropIndex >= 0)
                DrawInsertionMarker(graphics, _dropIndex);
        }

        private void ApplyColourAdjustment(Graphics graphics)
        {
            if (_settings == null) return;
            if (_settings.TintArgb != 0)
            {
                Color tint = Color.FromArgb(_settings.TintArgb);
                using (var overlay = new SolidBrush(Color.FromArgb(
                    72, tint.R, tint.G, tint.B)))
                    graphics.FillRectangle(overlay, ClientRectangle);
            }
            int shade = Math.Max(-60, Math.Min(60,
                _settings.LightShade));
            if (shade == 0) return;
            int alpha = Math.Min(150, Math.Abs(shade) * 2);
            Color colour = shade > 0
                ? Color.FromArgb(alpha, Color.White)
                : Color.FromArgb(alpha, Color.Black);
            using (var overlay = new SolidBrush(colour))
                graphics.FillRectangle(overlay, ClientRectangle);
        }

        private void DrawAddButton(
            Graphics graphics, RectangleF bounds, bool hovered)
        {
            if (IsImportedTheme)
            {
                EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(ThemeName);
                RectangleF themedCircle = RectangleF.Inflate(bounds, -8F, -8F);
                using (var fill = new SolidBrush(hovered ? theme.Accent : theme.Border))
                using (var edge = new Pen(theme.SecondaryText, 1.5F))
                using (var plus = new Pen(theme.PrimaryText, 2F))
                {
                    graphics.FillEllipse(fill, themedCircle); graphics.DrawEllipse(edge, themedCircle);
                    float cx = themedCircle.X + themedCircle.Width / 2F, cy = themedCircle.Y + themedCircle.Height / 2F;
                    graphics.DrawLine(plus, cx - 6F, cy, cx + 6F, cy);
                    graphics.DrawLine(plus, cx, cy - 6F, cx, cy + 6F);
                }
                return;
            }
            if (IsVintage)
            {
                DrawVintageAddButton(graphics, bounds, hovered);
                return;
            }
            if (IsModern)
            {
                DrawModernAddButton(graphics, bounds, hovered);
                return;
            }
            if (IsBotanical)
            {
                DrawBotanicalAddButton(graphics, bounds, hovered);
                return;
            }
            if (IsWoodland)
            {
                DrawWoodlandAddButton(graphics, bounds, hovered);
                return;
            }
            if (IsArtDeco)
            {
                DrawArtDecoAddButton(graphics, bounds, hovered);
                return;
            }
            if (IsSteampunk)
            {
                DrawSteampunkAddButton(graphics, bounds, hovered);
                return;
            }
            RectangleF circle = RectangleF.Inflate(bounds, -8F, -8F);
            using (var fill = new SolidBrush(hovered
                ? Color.FromArgb(205, 157, 78)
                : Color.FromArgb(95, 97, 94)))
            using (var edge = new Pen(Color.FromArgb(210, 183, 128), 1.5F))
            using (var plus = new Pen(Color.White, 2F))
            {
                graphics.FillEllipse(fill, circle);
                graphics.DrawEllipse(edge, circle);
                float cx = circle.X + circle.Width / 2F;
                float cy = circle.Y + circle.Height / 2F;
                graphics.DrawLine(plus, cx - 6F, cy, cx + 6F, cy);
                graphics.DrawLine(plus, cx, cy - 6F, cx, cy + 6F);
            }
        }

        private Color DockGlowColor()
        {
            if (IsImportedTheme)
            {
                Color c = EmilyDeskThemeCatalog.Get(ThemeName).Accent;
                return Color.FromArgb(78, c.R, c.G, c.B);
            }
            if (IsVintage) return Color.FromArgb(62, 178, 73, 67);
            if (IsModern) return Color.FromArgb(72, 70, 174, 238);
            if (IsBotanical) return Color.FromArgb(70, 218, 132, 151);
            if (IsWoodland) return Color.FromArgb(70, 197, 139, 78);
            if (IsArtDeco) return Color.FromArgb(72, 244, 208, 104);
            if (IsSteampunk) return Color.FromArgb(78, 240, 166, 54);
            return Color.FromArgb(55, 221, 161, 65);
        }

        private Color DockAccentColor()
        {
            if (IsImportedTheme) return EmilyDeskThemeCatalog.Get(ThemeName).Accent;
            if (IsVintage) return Color.FromArgb(170, 71, 65);
            if (IsModern) return Color.FromArgb(73, 178, 239);
            if (IsBotanical) return Color.FromArgb(210, 126, 139);
            if (IsWoodland) return Color.FromArgb(204, 139, 83);
            if (IsArtDeco) return Color.FromArgb(240, 210, 122);
            if (IsSteampunk) return Color.FromArgb(245, 180, 74);
            return Color.FromArgb(239, 173, 64);
        }

        private void DrawImportedBackground(Graphics graphics)
        {
            string path = EmilyDeskThemeCatalog.Get(ThemeName).Asset("dock");
            if (string.IsNullOrEmpty(path)) { DrawIndustrialBackground(graphics); return; }
            Image image = ThemeSkinCache.Get(path);
            Rectangle source = ThemeSkinCache.GetAlphaBounds(path);
            if (source.Width <= 0 || source.Height <= 0)
                source = new Rectangle(0, 0, image.Width, image.Height);
            if (IsHorizontal)
                graphics.DrawImage(image, ClientRectangle, source,
                    GraphicsUnit.Pixel);
            else
            {
                GraphicsState state = graphics.Save();
                graphics.TranslateTransform(Width, 0F);
                graphics.RotateTransform(90F);
                graphics.DrawImage(image,
                    new Rectangle(0, 0, Height, Width), source,
                    GraphicsUnit.Pixel);
                graphics.Restore(state);
            }
        }

        private void DrawVintageBackground(Graphics graphics)
        {
            RectangleF outer = new RectangleF(1F, 1F,
                Width - 2F, Height - 2F);
            using (GraphicsPath path = RoundedRectangle(outer, 19F))
            using (var enamel = new LinearGradientBrush(outer,
                Color.FromArgb(247, 235, 202),
                Color.FromArgb(185, 156, 111),
                IsHorizontal ? LinearGradientMode.Vertical
                    : LinearGradientMode.Horizontal))
            using (var chrome = new Pen(
                Color.FromArgb(202, 207, 199), 3F))
            using (var darkChrome = new Pen(
                Color.FromArgb(75, 78, 73), 1F))
            {
                graphics.FillPath(enamel, path);
                graphics.DrawPath(chrome, path);
                RectangleF inner = RectangleF.Inflate(outer, -5F, -5F);
                using (GraphicsPath innerPath = RoundedRectangle(inner, 14F))
                    graphics.DrawPath(darkChrome, innerPath);
            }
            RectangleF colourBand = RectangleF.Inflate(outer, -9F, -9F);
            using (GraphicsPath bandPath = RoundedRectangle(colourBand, 11F))
            using (var band = new Pen(
                Color.FromArgb(151, 73, 68), 3F))
            using (var pinstripe = new Pen(
                Color.FromArgb(103, 66, 42), 1F))
            {
                graphics.DrawPath(band, bandPath);
                RectangleF stripe = RectangleF.Inflate(colourBand, -4F, -4F);
                using (GraphicsPath stripePath = RoundedRectangle(stripe, 8F))
                    graphics.DrawPath(pinstripe, stripePath);
            }
            DrawVintageKnob(graphics, 13F, 13F);
            DrawVintageKnob(graphics, Width - 13F, 13F);
            DrawVintageKnob(graphics, 13F, Height - 13F);
            DrawVintageKnob(graphics, Width - 13F, Height - 13F);
            using (var line = new Pen(Color.FromArgb(151, 73, 68), 1.4F))
            {
                if (IsHorizontal)
                {
                    graphics.DrawLine(line, Width / 2F - 22F, 8F,
                        Width / 2F - 8F, 13F);
                    graphics.DrawLine(line, Width / 2F + 8F, 13F,
                        Width / 2F + 22F, 8F);
                }
                else
                {
                    graphics.DrawLine(line, 8F, Height / 2F - 22F,
                        13F, Height / 2F - 8F);
                    graphics.DrawLine(line, 13F, Height / 2F + 8F,
                        8F, Height / 2F + 22F);
                }
            }
        }

        private static void DrawVintageKnob(
            Graphics graphics, float x, float y)
        {
            RectangleF knob = new RectangleF(x - 5F, y - 5F, 10F, 10F);
            using (var chrome = new LinearGradientBrush(knob,
                Color.FromArgb(235, 237, 226),
                Color.FromArgb(91, 92, 85),
                LinearGradientMode.ForwardDiagonal))
            using (var red = new SolidBrush(
                Color.FromArgb(126, 57, 54)))
            {
                graphics.FillEllipse(chrome, knob);
                graphics.FillEllipse(red,
                    x - 2F, y - 2F, 4F, 4F);
            }
        }

        private void DrawModernBackground(Graphics graphics)
        {
            RectangleF outer = new RectangleF(1F, 1F,
                Width - 2F, Height - 2F);
            using (GraphicsPath path = RoundedRectangle(outer, 24F))
            using (var glass = new LinearGradientBrush(outer,
                Color.FromArgb(54, 71, 91),
                Color.FromArgb(10, 16, 25),
                IsHorizontal ? LinearGradientMode.Vertical
                    : LinearGradientMode.Horizontal))
            using (var cyan = new Pen(
                Color.FromArgb(117, 200, 244), 1.8F))
            {
                graphics.FillPath(glass, path);
                graphics.DrawPath(cyan, path);
            }
            RectangleF inner = RectangleF.Inflate(outer, -6F, -6F);
            using (GraphicsPath innerPath = RoundedRectangle(inner, 18F))
            using (var subtle = new Pen(
                Color.FromArgb(95, 171, 204, 229), 1F))
                graphics.DrawPath(subtle, innerPath);
            using (var highlight = new Pen(
                Color.FromArgb(110, 255, 255, 255), 1F))
            using (var node = new SolidBrush(
                Color.FromArgb(190, 73, 178, 239)))
            {
                if (IsHorizontal)
                {
                    graphics.DrawLine(highlight, 28F, 7F,
                        Width - 28F, 7F);
                    for (float x = 24F; x < Width - 20F; x += 36F)
                        graphics.FillEllipse(node, x, Height - 8F, 3F, 3F);
                }
                else
                {
                    graphics.DrawLine(highlight, 7F, 28F,
                        7F, Height - 28F);
                    for (float y = 24F; y < Height - 20F; y += 36F)
                        graphics.FillEllipse(node, Width - 8F, y, 3F, 3F);
                }
            }
        }

        private void DrawBotanicalBackground(Graphics graphics)
        {
            Image image = ThemeSkinCache.Get(BotanicalDockSkinPath);
            Rectangle source = ThemeSkinCache.GetAlphaBounds(
                BotanicalDockSkinPath);
            if (source.Width <= 0 || source.Height <= 0)
                source = new Rectangle(0, 0, image.Width, image.Height);
            // The original dock artwork has a much wider frame than the
            // Botanical widgets. Crop into that frame before stretching it so
            // the visible border matches the optional-widget border weight.
            if (source.Width > 24 && source.Height > 24)
                source.Inflate(-10, -10);
            if (IsHorizontal)
                graphics.DrawImage(image, ClientRectangle, source,
                    GraphicsUnit.Pixel);
            else
            {
                GraphicsState state = graphics.Save();
                graphics.TranslateTransform(Width, 0F);
                graphics.RotateTransform(90F);
                graphics.DrawImage(image,
                    new Rectangle(0, 0, Height, Width), source,
                    GraphicsUnit.Pixel);
                graphics.Restore(state);
            }
        }

        private static void DrawBotanicalCorner(Graphics graphics,
            float x, float y, float dx, float dy)
        {
            using (var stem = new Pen(
                Color.FromArgb(100, 128, 87), 1.4F))
            {
                graphics.DrawLine(stem, x, y,
                    x + dx * 14F, y + dy * 7F);
                graphics.DrawLine(stem, x, y,
                    x + dx * 7F, y + dy * 14F);
            }
            DrawNatureLeaf(graphics, x + dx * 10F,
                y + dy * 4F, 8F, dx,
                Color.FromArgb(112, 140, 95));
            DrawNatureLeaf(graphics, x + dx * 4F,
                y + dy * 10F, 8F, dy,
                Color.FromArgb(91, 122, 82));
            DrawSmallFlower(graphics, x, y,
                Color.FromArgb(211, 126, 143));
        }

        private void DrawWoodlandBackground(Graphics graphics)
        {
            RectangleF outer = new RectangleF(1F, 1F,
                Width - 2F, Height - 2F);
            using (GraphicsPath path = RoundedRectangle(outer, 18F))
            using (var wood = new LinearGradientBrush(outer,
                Color.FromArgb(126, 87, 54),
                Color.FromArgb(52, 34, 25),
                IsHorizontal ? LinearGradientMode.Vertical
                    : LinearGradientMode.Horizontal))
            using (var bark = new Pen(
                Color.FromArgb(42, 28, 21), 3F))
            {
                graphics.FillPath(wood, path);
                graphics.DrawPath(bark, path);
            }
            RectangleF leather = RectangleF.Inflate(outer, -7F, -7F);
            using (GraphicsPath leatherPath = RoundedRectangle(leather, 12F))
            using (var green = new LinearGradientBrush(leather,
                Color.FromArgb(58, 78, 59),
                Color.FromArgb(18, 34, 28),
                IsHorizontal ? LinearGradientMode.Vertical
                    : LinearGradientMode.Horizontal))
            using (var stitch = new Pen(
                Color.FromArgb(188, 160, 112), 1F))
            {
                graphics.FillPath(green, leatherPath);
                stitch.DashStyle = DashStyle.Dash;
                graphics.DrawPath(stitch, leatherPath);
            }
            using (var grain = new Pen(
                Color.FromArgb(75, 211, 171, 109), 1F))
            {
                if (IsHorizontal)
                {
                    graphics.DrawArc(grain, 20F, 4F, 65F, 16F, 190F, 140F);
                    graphics.DrawArc(grain, Width - 86F, Height - 20F,
                        65F, 16F, 10F, 140F);
                }
                else
                {
                    graphics.DrawArc(grain, 4F, 20F, 16F, 65F, 100F, 140F);
                    graphics.DrawArc(grain, Width - 20F, Height - 86F,
                        16F, 65F, 280F, 140F);
                }
            }
            DrawWoodlandSprig(graphics, 17F, 17F, 1F, 1F);
            DrawWoodlandSprig(graphics,
                Width - 17F, Height - 17F, -1F, -1F);
        }

        private static void DrawWoodlandSprig(Graphics graphics,
            float x, float y, float dx, float dy)
        {
            using (var stem = new Pen(
                Color.FromArgb(181, 151, 92), 1.5F))
                graphics.DrawLine(stem, x, y,
                    x + dx * 18F, y + dy * 12F);
            DrawNatureLeaf(graphics,
                x + dx * 8F, y + dy * 4F, 10F, dx,
                Color.FromArgb(85, 111, 73));
            DrawNatureLeaf(graphics,
                x + dx * 14F, y + dy * 9F, 9F, dy,
                Color.FromArgb(70, 96, 65));
            using (var acorn = new SolidBrush(
                Color.FromArgb(170, 105, 57)))
            using (var cap = new SolidBrush(
                Color.FromArgb(86, 57, 40)))
            {
                graphics.FillEllipse(acorn,
                    x - 3F, y - 2F, 6F, 8F);
                graphics.FillRectangle(cap,
                    x - 3F, y - 3F, 6F, 3F);
            }
        }

        private static void DrawNatureLeaf(Graphics graphics,
            float x, float y, float size, float direction, Color colour)
        {
            using (var path = new GraphicsPath())
            {
                float dx = direction >= 0F ? size : -size;
                path.AddBezier(x, y,
                    x + dx * .35F, y - size * .55F,
                    x + dx * .8F, y - size * .4F,
                    x + dx, y);
                path.AddBezier(x + dx, y,
                    x + dx * .75F, y + size * .45F,
                    x + dx * .3F, y + size * .45F,
                    x, y);
                path.CloseFigure();
                using (var fill = new SolidBrush(colour))
                    graphics.FillPath(fill, path);
            }
        }

        private static void DrawSmallFlower(
            Graphics graphics, float x, float y, Color colour)
        {
            using (var petal = new SolidBrush(colour))
            using (var centre = new SolidBrush(
                Color.FromArgb(223, 175, 70)))
            {
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3D;
                    float px = x + (float)Math.Cos(angle) * 4F;
                    float py = y + (float)Math.Sin(angle) * 4F;
                    graphics.FillEllipse(petal, px - 2.5F, py - 2.5F,
                        5F, 5F);
                }
                graphics.FillEllipse(centre, x - 2F, y - 2F, 4F, 4F);
            }
        }

        private static void DrawVintageAddButton(
            Graphics graphics, RectangleF bounds, bool hovered)
        {
            RectangleF circle = RectangleF.Inflate(bounds, -8F, -8F);
            using (var chrome = new LinearGradientBrush(circle,
                Color.FromArgb(239, 239, 226),
                Color.FromArgb(91, 93, 87),
                LinearGradientMode.ForwardDiagonal))
            using (var red = new SolidBrush(hovered
                ? Color.FromArgb(184, 84, 76)
                : Color.FromArgb(126, 57, 54)))
            using (var plus = new Pen(
                Color.FromArgb(249, 239, 207), 2.2F))
            {
                graphics.FillEllipse(chrome, circle);
                RectangleF inner = RectangleF.Inflate(circle, -4F, -4F);
                graphics.FillEllipse(red, inner);
                float cx = inner.X + inner.Width / 2F;
                float cy = inner.Y + inner.Height / 2F;
                graphics.DrawLine(plus, cx - 6F, cy, cx + 6F, cy);
                graphics.DrawLine(plus, cx, cy - 6F, cx, cy + 6F);
            }
        }

        private static void DrawModernAddButton(
            Graphics graphics, RectangleF bounds, bool hovered)
        {
            RectangleF button = RectangleF.Inflate(bounds, -8F, -8F);
            using (GraphicsPath path = RoundedRectangle(button, 12F))
            using (var glass = new LinearGradientBrush(button,
                hovered ? Color.FromArgb(74, 182, 239)
                    : Color.FromArgb(42, 89, 128),
                Color.FromArgb(12, 28, 43),
                LinearGradientMode.Vertical))
            using (var edge = new Pen(
                Color.FromArgb(132, 217, 252), 1.5F))
            using (var plus = new Pen(Color.White, 2F))
            {
                graphics.FillPath(glass, path);
                graphics.DrawPath(edge, path);
                float cx = button.X + button.Width / 2F;
                float cy = button.Y + button.Height / 2F;
                graphics.DrawLine(plus, cx - 7F, cy, cx + 7F, cy);
                graphics.DrawLine(plus, cx, cy - 7F, cx, cy + 7F);
            }
        }

        private static void DrawBotanicalAddButton(
            Graphics graphics, RectangleF bounds, bool hovered)
        {
            float cx = bounds.X + bounds.Width / 2F;
            float cy = bounds.Y + bounds.Height / 2F;
            float radius = Math.Min(bounds.Width, bounds.Height) * .32F;
            using (var petal = new SolidBrush(hovered
                ? Color.FromArgb(226, 144, 158)
                : Color.FromArgb(210, 126, 139)))
            using (var centre = new SolidBrush(
                Color.FromArgb(87, 118, 82)))
            using (var plus = new Pen(
                Color.FromArgb(249, 243, 224), 2F))
            {
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4D;
                    float px = cx + (float)Math.Cos(angle) * radius * .68F;
                    float py = cy + (float)Math.Sin(angle) * radius * .68F;
                    graphics.FillEllipse(petal,
                        px - radius * .42F, py - radius * .42F,
                        radius * .84F, radius * .84F);
                }
                graphics.FillEllipse(centre,
                    cx - radius * .58F, cy - radius * .58F,
                    radius * 1.16F, radius * 1.16F);
                graphics.DrawLine(plus, cx - 5F, cy, cx + 5F, cy);
                graphics.DrawLine(plus, cx, cy - 5F, cx, cy + 5F);
            }
        }

        private static void DrawWoodlandAddButton(
            Graphics graphics, RectangleF bounds, bool hovered)
        {
            RectangleF disk = RectangleF.Inflate(bounds, -8F, -8F);
            using (var wood = new LinearGradientBrush(disk,
                hovered ? Color.FromArgb(183, 119, 67)
                    : Color.FromArgb(129, 82, 48),
                Color.FromArgb(49, 34, 26),
                LinearGradientMode.ForwardDiagonal))
            using (var edge = new Pen(
                Color.FromArgb(208, 166, 101), 2F))
            using (var ring = new Pen(
                Color.FromArgb(91, 61, 40), 1F))
            using (var plus = new Pen(
                Color.FromArgb(241, 224, 188), 2.2F))
            {
                graphics.FillEllipse(wood, disk);
                graphics.DrawEllipse(edge, disk);
                RectangleF inner = RectangleF.Inflate(disk, -5F, -5F);
                graphics.DrawEllipse(ring, inner);
                float cx = disk.X + disk.Width / 2F;
                float cy = disk.Y + disk.Height / 2F;
                graphics.DrawLine(plus, cx - 6F, cy, cx + 6F, cy);
                graphics.DrawLine(plus, cx, cy - 6F, cx, cy + 6F);
            }
        }

        private void DrawIndustrialBackground(Graphics graphics)
        {
            RectangleF outer = new RectangleF(1F, 1F,
                Width - 2F, Height - 2F);
            using (GraphicsPath path = RoundedRectangle(outer, 16F))
            using (var fill = new LinearGradientBrush(
                outer,
                Color.FromArgb(74, 75, 73),
                Color.FromArgb(20, 21, 21),
                LinearGradientMode.Vertical))
            using (var steel = new Pen(
                Color.FromArgb(168, 170, 166), 2F))
            using (var inner = new Pen(
                Color.FromArgb(181, 126, 54), 1.2F))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(steel, path);
                RectangleF innerBounds = RectangleF.Inflate(
                    outer, -5F, -5F);
                using (GraphicsPath innerPath = RoundedRectangle(
                    innerBounds, 12F))
                    graphics.DrawPath(inner, innerPath);
            }
            DrawRivet(graphics, 11F, 11F);
            DrawRivet(graphics, Width - 11F, 11F);
            DrawRivet(graphics, 11F, Height - 11F);
            DrawRivet(graphics, Width - 11F, Height - 11F);
        }

        private void DrawArtDecoBackground(Graphics graphics)
        {
            RectangleF outer = new RectangleF(1F, 1F,
                Width - 2F, Height - 2F);
            using (GraphicsPath outerPath = RoundedRectangle(outer, 10F))
            using (var lacquer = new LinearGradientBrush(
                outer,
                Color.FromArgb(42, 52, 52),
                Color.FromArgb(8, 13, 14),
                IsHorizontal
                    ? LinearGradientMode.Vertical
                    : LinearGradientMode.Horizontal))
            using (var outerGold = new Pen(
                Color.FromArgb(226, 188, 91), 2F))
            {
                graphics.FillPath(lacquer, outerPath);
                graphics.DrawPath(outerGold, outerPath);
            }

            RectangleF ivoryBand = RectangleF.Inflate(outer, -5F, -5F);
            using (GraphicsPath bandPath = RoundedRectangle(ivoryBand, 7F))
            using (var band = new Pen(Color.FromArgb(229, 218, 181), 1.2F))
            using (var innerGold = new Pen(
                Color.FromArgb(166, 120, 39), 1F))
            {
                graphics.DrawPath(band, bandPath);
                RectangleF inner = RectangleF.Inflate(ivoryBand, -4F, -4F);
                using (GraphicsPath innerPath = RoundedRectangle(inner, 4F))
                    graphics.DrawPath(innerGold, innerPath);
            }

            DrawArtDecoCornerFan(graphics, 14F, 14F, 1F, 1F);
            DrawArtDecoCornerFan(graphics, Width - 14F, 14F, -1F, 1F);
            DrawArtDecoCornerFan(graphics, 14F, Height - 14F, 1F, -1F);
            DrawArtDecoCornerFan(graphics,
                Width - 14F, Height - 14F, -1F, -1F);

            using (var centreGold = new Pen(
                Color.FromArgb(198, 155, 61), 1.2F))
            {
                if (IsHorizontal)
                {
                    float cx = Width / 2F;
                    graphics.DrawLine(centreGold, cx - 18F, 8F,
                        cx, 13F);
                    graphics.DrawLine(centreGold, cx, 13F,
                        cx + 18F, 8F);
                    graphics.DrawLine(centreGold, cx - 18F, Height - 8F,
                        cx, Height - 13F);
                    graphics.DrawLine(centreGold, cx, Height - 13F,
                        cx + 18F, Height - 8F);
                }
                else
                {
                    float cy = Height / 2F;
                    graphics.DrawLine(centreGold, 8F, cy - 18F,
                        13F, cy);
                    graphics.DrawLine(centreGold, 13F, cy,
                        8F, cy + 18F);
                    graphics.DrawLine(centreGold, Width - 8F, cy - 18F,
                        Width - 13F, cy);
                    graphics.DrawLine(centreGold, Width - 13F, cy,
                        Width - 8F, cy + 18F);
                }
            }
        }

        private static void DrawArtDecoCornerFan(Graphics graphics,
            float x, float y, float directionX, float directionY)
        {
            using (var gold = new Pen(
                Color.FromArgb(221, 182, 80), 1.2F))
            using (var centre = new SolidBrush(
                Color.FromArgb(230, 206, 139)))
            {
                for (int ray = 0; ray < 3; ray++)
                {
                    float offset = 4F + ray * 3F;
                    graphics.DrawLine(gold, x, y,
                        x + directionX * offset,
                        y + directionY * 10F);
                    graphics.DrawLine(gold, x, y,
                        x + directionX * 10F,
                        y + directionY * offset);
                }
                graphics.FillRectangle(centre,
                    x - 1.5F, y - 1.5F, 3F, 3F);
            }
        }

        private static void DrawArtDecoAddButton(
            Graphics graphics, RectangleF bounds, bool hovered)
        {
            RectangleF button = RectangleF.Inflate(bounds, -8F, -8F);
            float cx = button.X + button.Width / 2F;
            float cy = button.Y + button.Height / 2F;
            PointF[] octagon = new PointF[8];
            float radius = Math.Min(button.Width, button.Height) / 2F;
            for (int i = 0; i < octagon.Length; i++)
            {
                double angle = -Math.PI / 8D + i * Math.PI / 4D;
                octagon[i] = new PointF(
                    cx + (float)Math.Cos(angle) * radius,
                    cy + (float)Math.Sin(angle) * radius);
            }
            using (var fill = new LinearGradientBrush(button,
                hovered ? Color.FromArgb(82, 101, 95)
                    : Color.FromArgb(36, 49, 48),
                Color.FromArgb(8, 14, 15),
                LinearGradientMode.Vertical))
            using (var edge = new Pen(
                Color.FromArgb(229, 192, 94), 2F))
            using (var inner = new Pen(
                Color.FromArgb(232, 220, 181), 1F))
            using (var plus = new Pen(
                Color.FromArgb(242, 222, 155), 2.2F))
            {
                graphics.FillPolygon(fill, octagon);
                graphics.DrawPolygon(edge, octagon);
                graphics.DrawRectangle(inner,
                    cx - radius * .52F, cy - radius * .52F,
                    radius * 1.04F, radius * 1.04F);
                graphics.DrawLine(plus,
                    cx - radius * .28F, cy,
                    cx + radius * .28F, cy);
                graphics.DrawLine(plus,
                    cx, cy - radius * .28F,
                    cx, cy + radius * .28F);
            }
        }

        private void DrawSteampunkBackground(Graphics graphics)
        {
            RectangleF outer = new RectangleF(1F, 1F,
                Width - 2F, Height - 2F);
            using (GraphicsPath outerPath = RoundedRectangle(outer, 18F))
            using (var wood = new LinearGradientBrush(
                outer,
                Color.FromArgb(95, 48, 25),
                Color.FromArgb(31, 17, 13),
                IsHorizontal
                    ? LinearGradientMode.Vertical
                    : LinearGradientMode.Horizontal))
            using (var darkEdge = new Pen(
                Color.FromArgb(48, 24, 15), 3F))
            {
                graphics.FillPath(wood, outerPath);
                graphics.DrawPath(darkEdge, outerPath);
            }

            RectangleF brassPlate = RectangleF.Inflate(outer, -4F, -4F);
            using (GraphicsPath platePath = RoundedRectangle(
                brassPlate, 14F))
            using (var brass = new LinearGradientBrush(
                brassPlate,
                Color.FromArgb(225, 176, 81),
                Color.FromArgb(91, 52, 25),
                LinearGradientMode.ForwardDiagonal))
            {
                graphics.FillPath(brass, platePath);
            }

            RectangleF leatherBounds = RectangleF.Inflate(
                brassPlate, -5F, -5F);
            using (GraphicsPath leatherPath = RoundedRectangle(
                leatherBounds, 10F))
            using (var leather = new LinearGradientBrush(
                leatherBounds,
                Color.FromArgb(83, 42, 29),
                Color.FromArgb(25, 17, 15),
                IsHorizontal
                    ? LinearGradientMode.Vertical
                    : LinearGradientMode.Horizontal))
            using (var leatherEdge = new Pen(
                Color.FromArgb(242, 190, 88), 1.2F))
            {
                graphics.FillPath(leather, leatherPath);
                graphics.DrawPath(leatherEdge, leatherPath);
            }

            DrawCopperRail(graphics, leatherBounds);
            DrawSteampunkGear(graphics, 13F, 13F, 8F);
            DrawSteampunkGear(graphics, Width - 13F, 13F, 8F);
            DrawSteampunkGear(graphics, 13F, Height - 13F, 8F);
            DrawSteampunkGear(graphics,
                Width - 13F, Height - 13F, 8F);
        }

        private void DrawCopperRail(
            Graphics graphics, RectangleF bounds)
        {
            using (var shadow = new Pen(
                Color.FromArgb(90, 27, 12, 8), 5F))
            using (var copper = new Pen(
                Color.FromArgb(191, 105, 48), 3F))
            using (var shine = new Pen(
                Color.FromArgb(222, 174, 92), 1F))
            {
                if (IsHorizontal)
                {
                    float y = bounds.Bottom - 5F;
                    graphics.DrawLine(shadow,
                        bounds.Left + 16F, y + 1F,
                        bounds.Right - 16F, y + 1F);
                    graphics.DrawLine(copper,
                        bounds.Left + 16F, y,
                        bounds.Right - 16F, y);
                    graphics.DrawLine(shine,
                        bounds.Left + 17F, y - 1F,
                        bounds.Right - 17F, y - 1F);
                }
                else
                {
                    float x = bounds.Right - 5F;
                    graphics.DrawLine(shadow,
                        x + 1F, bounds.Top + 16F,
                        x + 1F, bounds.Bottom - 16F);
                    graphics.DrawLine(copper,
                        x, bounds.Top + 16F,
                        x, bounds.Bottom - 16F);
                    graphics.DrawLine(shine,
                        x - 1F, bounds.Top + 17F,
                        x - 1F, bounds.Bottom - 17F);
                }
            }
        }

        private static void DrawSteampunkGear(
            Graphics graphics, float x, float y, float radius)
        {
            graphics.TranslateTransform(x, y);
            PointF[] teeth = new PointF[16];
            for (int i = 0; i < teeth.Length; i++)
            {
                double angle = i * Math.PI * 2D / teeth.Length;
                float toothRadius = i % 2 == 0
                    ? radius : radius * 0.72F;
                teeth[i] = new PointF(
                    (float)Math.Cos(angle) * toothRadius,
                    (float)Math.Sin(angle) * toothRadius);
            }
            using (var fill = new LinearGradientBrush(
                new RectangleF(-radius, -radius,
                    radius * 2F, radius * 2F),
                Color.FromArgb(239, 190, 91),
                Color.FromArgb(91, 49, 22),
                LinearGradientMode.ForwardDiagonal))
            using (var edge = new Pen(
                Color.FromArgb(50, 25, 14), 1F))
            using (var hub = new SolidBrush(
                Color.FromArgb(43, 25, 19)))
            {
                graphics.FillPolygon(fill, teeth);
                graphics.DrawPolygon(edge, teeth);
                graphics.FillEllipse(hub, -2.2F, -2.2F, 4.4F, 4.4F);
            }
            graphics.ResetTransform();
        }

        private static void DrawSteampunkAddButton(
            Graphics graphics, RectangleF bounds, bool hovered)
        {
            RectangleF gearBounds = RectangleF.Inflate(
                bounds, -8F, -8F);
            float cx = gearBounds.X + gearBounds.Width / 2F;
            float cy = gearBounds.Y + gearBounds.Height / 2F;
            float radius = Math.Min(gearBounds.Width,
                gearBounds.Height) / 2F;
            graphics.TranslateTransform(cx, cy);
            PointF[] teeth = new PointF[24];
            for (int i = 0; i < teeth.Length; i++)
            {
                double angle = i * Math.PI * 2D / teeth.Length;
                float toothRadius = i % 2 == 0
                    ? radius : radius * 0.82F;
                teeth[i] = new PointF(
                    (float)Math.Cos(angle) * toothRadius,
                    (float)Math.Sin(angle) * toothRadius);
            }
            using (var fill = new LinearGradientBrush(
                new RectangleF(-radius, -radius,
                    radius * 2F, radius * 2F),
                hovered
                    ? Color.FromArgb(250, 197, 89)
                    : Color.FromArgb(187, 119, 52),
                Color.FromArgb(70, 37, 20),
                LinearGradientMode.ForwardDiagonal))
            using (var edge = new Pen(
                Color.FromArgb(241, 196, 105), 1.5F))
            using (var hub = new SolidBrush(
                Color.FromArgb(53, 29, 22)))
            using (var plus = new Pen(
                Color.FromArgb(244, 218, 158), 2.2F))
            {
                graphics.FillPolygon(fill, teeth);
                graphics.DrawPolygon(edge, teeth);
                graphics.FillEllipse(hub,
                    -radius * 0.55F, -radius * 0.55F,
                    radius * 1.1F, radius * 1.1F);
                graphics.DrawLine(plus,
                    -radius * 0.26F, 0F, radius * 0.26F, 0F);
                graphics.DrawLine(plus,
                    0F, -radius * 0.26F, 0F, radius * 0.26F);
            }
            graphics.ResetTransform();
        }

        private static void DrawRivet(Graphics graphics, float x, float y)
        {
            RectangleF rect = new RectangleF(x - 4F, y - 4F, 8F, 8F);
            using (var fill = new LinearGradientBrush(
                rect,
                Color.FromArgb(205, 207, 202),
                Color.FromArgb(65, 66, 64),
                LinearGradientMode.ForwardDiagonal))
            using (var edge = new Pen(Color.FromArgb(18, 18, 18), 1F))
            using (var slot = new Pen(Color.FromArgb(55, 55, 53), 1F))
            {
                graphics.FillEllipse(fill, rect);
                graphics.DrawEllipse(edge, rect);
                graphics.DrawLine(slot, x - 2F, y, x + 2F, y);
            }
        }

        private static void DrawMissingIcon(
            Graphics graphics, RectangleF bounds)
        {
            using (var fill = new SolidBrush(Color.FromArgb(110, 45, 45)))
            using (var text = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 18F,
                FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                graphics.FillEllipse(fill, bounds);
                graphics.DrawString("?", font, text, bounds, format);
            }
        }

        private void DrawInsertionMarker(Graphics graphics, int index)
        {
            bool horizontal = IsHorizontal;
            RectangleF reference = index >= _itemBounds.Count
                ? _addBounds : _itemBounds[index];
            using (var marker = new Pen(DockAccentColor(), 3F))
            {
                if (horizontal)
                {
                    float x = reference.Left - ItemGap / 2F;
                    graphics.DrawLine(marker, x, 13F, x, Height - 13F);
                }
                else
                {
                    float y = reference.Top - ItemGap / 2F;
                    graphics.DrawLine(marker, 13F, y, Width - 13F, y);
                }
            }
        }

        private RectangleF DisplayBounds(int index, RectangleF original)
        {
            if (index != _hoverIndex || _dragStarted)
                return original;
            float scale = 1.20F;
            float width = original.Width * scale;
            float height = original.Height * scale;
            EmilyDeskDockEdge edge = CurrentEdge;
            if (edge == EmilyDeskDockEdge.Bottom)
                return new RectangleF(
                    original.X - (width - original.Width) / 2F,
                    original.Bottom - height, width, height);
            if (edge == EmilyDeskDockEdge.Top)
                return new RectangleF(
                    original.X - (width - original.Width) / 2F,
                    original.Top, width, height);
            if (edge == EmilyDeskDockEdge.Left)
                return new RectangleF(
                    original.Left,
                    original.Y - (height - original.Height) / 2F,
                    width, height);
            return new RectangleF(
                original.Right - width,
                original.Y - (height - original.Height) / 2F,
                width, height);
        }

        private void LayoutDock()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle working = screen.WorkingArea;
            bool horizontal = IsHorizontal;
            int count = Math.Max(1, _settings.Items.Count + 1);
            int stride = _settings.IconSize + ItemGap;
            int longSize = count * stride + OuterPadding * 2;
            int maximum = horizontal
                ? working.Width - 16
                : working.Height - 16;
            longSize = Math.Max(230, Math.Min(maximum, longSize));
            int thickness = _settings.IconSize + 42;
            Size = horizontal
                ? new Size(longSize, thickness)
                : new Size(thickness, longSize);

            _itemBounds = new List<RectangleF>();
            float available = (horizontal ? Width : Height) -
                OuterPadding * 2F;
            float actualStride = Math.Min(stride, available / count);
            float icon = Math.Min(_settings.IconSize,
                Math.Max(28F, actualStride - 4F));
            for (int i = 0; i < _settings.Items.Count; i++)
            {
                float along = OuterPadding + i * actualStride +
                    (actualStride - icon) / 2F;
                _itemBounds.Add(horizontal
                    ? new RectangleF(along, (Height - icon) / 2F,
                        icon, icon)
                    : new RectangleF((Width - icon) / 2F, along,
                        icon, icon));
            }
            float addAlong = OuterPadding + _settings.Items.Count *
                actualStride + (actualStride - icon) / 2F;
            _addBounds = horizontal
                ? new RectangleF(addAlong, (Height - icon) / 2F, icon, icon)
                : new RectangleF((Width - icon) / 2F, addAlong, icon, icon);

            EmilyDeskDockEdge edge = CurrentEdge;
            if (edge == EmilyDeskDockEdge.Bottom)
                _shownLocation = new Point(
                    working.Left + (working.Width - Width) / 2,
                    working.Bottom - Height - 8);
            else if (edge == EmilyDeskDockEdge.Top)
                _shownLocation = new Point(
                    working.Left + (working.Width - Width) / 2,
                    working.Top + 8);
            else if (edge == EmilyDeskDockEdge.Left)
                _shownLocation = new Point(
                    working.Left + 8,
                    working.Top + (working.Height - Height) / 2);
            else
                _shownLocation = new Point(
                    working.Right - Width - 8,
                    working.Top + (working.Height - Height) / 2);

            Rectangle screenBounds = screen.Bounds;
            if (edge == EmilyDeskDockEdge.Bottom)
                _hiddenLocation = new Point(
                    _shownLocation.X, screenBounds.Bottom - 5);
            else if (edge == EmilyDeskDockEdge.Top)
                _hiddenLocation = new Point(
                    _shownLocation.X, screenBounds.Top - Height + 5);
            else if (edge == EmilyDeskDockEdge.Left)
                _hiddenLocation = new Point(
                    screenBounds.Left - Width + 5, _shownLocation.Y);
            else
                _hiddenLocation = new Point(
                    screenBounds.Right - 5, _shownLocation.Y);

            _targetLocation = _shownLocation;
            Location = _shownLocation;
            using (GraphicsPath regionPath = RoundedRectangle(
                new RectangleF(0F, 0F, Width, Height),
                IsBotanical ? 24F : 16F))
            {
                Region previousRegion = Region;
                Region = new Region(regionPath);
                if (previousRegion != null)
                    previousRegion.Dispose();
            }
        }

        private void AutoHideTick(object sender, EventArgs e)
        {
            if (_settings == null) return;
            Rectangle hoverArea = Bounds;
            hoverArea.Inflate(18, 18);
            bool pointerNear = hoverArea.Contains(Cursor.Position);
            if (pointerNear)
                _lastInteraction = DateTime.UtcNow;
            if (!_settings.AutoHide || pointerNear || ContainsFocus ||
                DateTime.UtcNow - _lastInteraction <
                    TimeSpan.FromMilliseconds(900))
                _targetLocation = _shownLocation;
            else
                _targetLocation = _hiddenLocation;
            MoveTowardTarget();
        }

        private void MoveTowardTarget()
        {
            int dx = _targetLocation.X - Left;
            int dy = _targetLocation.Y - Top;
            if (dx == 0 && dy == 0) return;
            int x = Math.Abs(dx) <= 2 ? _targetLocation.X
                : Left + Math.Sign(dx) * Math.Max(2, Math.Abs(dx) / 4);
            int y = Math.Abs(dy) <= 2 ? _targetLocation.Y
                : Top + Math.Sign(dy) * Math.Max(2, Math.Abs(dy) / 4);
            Location = new Point(x, y);
        }

        private void DockMouseDown(object sender, MouseEventArgs e)
        {
            _lastInteraction = DateTime.UtcNow;
            _hoverLabel.HideLabel();
            if (e.Button != MouseButtons.Left) return;
            _mouseDownIndex = HitTest(e.Location);
            _mouseDownPoint = e.Location;
            _dragStarted = false;
            Capture = true;
        }

        private void DockMouseMove(object sender, MouseEventArgs e)
        {
            _lastInteraction = DateTime.UtcNow;
            int hit = HitTest(e.Location);
            if (!_dragStarted && _mouseDownIndex >= 0 &&
                _mouseDownIndex < _settings.Items.Count &&
                e.Button == MouseButtons.Left &&
                (Math.Abs(e.X - _mouseDownPoint.X) >
                    SystemInformation.DragSize.Width / 2 ||
                 Math.Abs(e.Y - _mouseDownPoint.Y) >
                    SystemInformation.DragSize.Height / 2))
            {
                _dragStarted = true;
                _dragIndex = _mouseDownIndex;
                Cursor = Cursors.Hand;
            }
            if (_dragStarted)
            {
                _dropIndex = Math.Max(0, Math.Min(
                    _settings.Items.Count, hit < 0
                        ? NearestDropIndex(e.Location) : hit));
                Invalidate();
                return;
            }
            if (hit == _hoverIndex) return;
            _hoverIndex = hit;
            string label = hit >= 0 && hit < _settings.Items.Count
                ? _settings.Items[hit].Label
                : hit == _settings.Items.Count
                    ? "Add to dock" : string.Empty;
            ShowHoverLabel(hit, label);
            Invalidate();
        }

        private void ShowHoverLabel(int hit, string label)
        {
            _hoverLabel.HideLabel();
            if (hit < 0 || string.IsNullOrWhiteSpace(label)) return;
            RectangleF bounds = hit < _itemBounds.Count
                ? _itemBounds[hit] : _addBounds;
            Rectangle iconBounds = Rectangle.Round(bounds);
            iconBounds.Offset(PointToScreen(Point.Empty));
            _hoverLabel.ShowLabel(label, iconBounds, CurrentEdge);
        }

        private void DockMouseUp(object sender, MouseEventArgs e)
        {
            _lastInteraction = DateTime.UtcNow;
            Capture = false;
            if (e.Button == MouseButtons.Right)
            {
                int rightHit = HitTest(e.Location);
                if (rightHit >= 0 && rightHit < _settings.Items.Count)
                    ShowItemMenu(rightHit, e.Location);
                else
                    ShowDockMenu(e.Location);
                ResetDrag();
                return;
            }
            if (e.Button != MouseButtons.Left)
            {
                ResetDrag();
                return;
            }
            if (_dragStarted && _dragIndex >= 0)
            {
                Reorder(_dragIndex, _dropIndex);
                ResetDrag();
                return;
            }
            int hit = HitTest(e.Location);
            if (hit == _mouseDownIndex)
            {
                if (hit >= 0 && hit < _settings.Items.Count)
                    Launch(_settings.Items[hit]);
                else if (hit == _settings.Items.Count)
                    ShowAddMenu(e.Location);
            }
            ResetDrag();
        }

        private void ResetDrag()
        {
            _mouseDownIndex = -1;
            _dragIndex = -1;
            _dropIndex = -1;
            _dragStarted = false;
            Cursor = Cursors.Default;
            Capture = false;
            Invalidate();
        }

        private int HitTest(Point point)
        {
            for (int i = 0; i < _itemBounds.Count; i++)
                if (RectangleF.Inflate(_itemBounds[i], 5F, 5F)
                    .Contains(point))
                    return i;
            if (RectangleF.Inflate(_addBounds, 5F, 5F)
                .Contains(point))
                return _settings.Items.Count;
            return -1;
        }

        private int NearestDropIndex(Point point)
        {
            float coordinate = IsHorizontal ? point.X : point.Y;
            for (int i = 0; i < _itemBounds.Count; i++)
            {
                RectangleF bounds = _itemBounds[i];
                float center = IsHorizontal
                    ? bounds.X + bounds.Width / 2F
                    : bounds.Y + bounds.Height / 2F;
                if (coordinate < center) return i;
            }
            return _settings.Items.Count;
        }

        private void Reorder(int source, int destination)
        {
            if (source < 0 || source >= _settings.Items.Count) return;
            destination = Math.Max(0, Math.Min(
                _settings.Items.Count, destination));
            EmilyDeskDockItem item = _settings.Items[source];
            _settings.Items.RemoveAt(source);
            if (destination > source) destination--;
            destination = Math.Max(0, Math.Min(
                _settings.Items.Count, destination));
            _settings.Items.Insert(destination, item);
            SaveAndRefresh(false);
        }

        private void DockDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) ||
                e.Data.GetDataPresent(DataFormats.Text))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void DockDragDrop(object sender, DragEventArgs e)
        {
            _lastInteraction = DateTime.UtcNow;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = e.Data.GetData(DataFormats.FileDrop)
                    as string[];
                if (paths != null)
                    foreach (string path in paths)
                        AddTarget(path);
            }
            else if (e.Data.GetDataPresent(DataFormats.Text))
            {
                string text = Convert.ToString(
                    e.Data.GetData(DataFormats.Text));
                Uri uri;
                if (Uri.TryCreate(text, UriKind.Absolute, out uri))
                    AddTarget(uri.AbsoluteUri);
            }
            SaveAndRefresh(false);
        }

        private void AddApplications()
        {
            try
            {
                using (var dialog = new OpenFileDialog
                {
                    Title = "Add applications or shortcuts to EmilyDesk Dock",
                    Filter = "Programs and shortcuts|*.exe;*.lnk;*.url;*.bat;*.cmd|All files|*.*",
                    Multiselect = true,
                    CheckFileExists = true
                })
                {
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    foreach (string path in dialog.FileNames)
                        AddTarget(path);
                    SaveAndRefresh(false);
                }
            }
            catch (Exception ex)
            {
                ShowDockActionError("application picker", ex);
            }
        }

        private void AddFolder()
        {
            try
            {
                using (var dialog = new FolderBrowserDialog
                {
                    Description =
                        "Choose a folder or library location to add to EmilyDesk Dock",
                    RootFolder = Environment.SpecialFolder.Desktop,
                    ShowNewFolderButton = false
                })
                {
                    // This native dialog must not be owned by the borderless,
                    // topmost dock window. Windows 10 can destroy that owner
                    // when the shell folder picker tears down.
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    AddTarget(dialog.SelectedPath);
                    SaveAndRefresh(false);
                }
            }
            catch (Exception ex)
            {
                ShowDockActionError("folder picker", ex);
            }
        }

        private void AddWebLink()
        {
            string address = Prompt.Show(
                this,
                "Web address",
                "Enter the webpage URL to add to the dock:",
                "https://");
            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
                return;
            AddTarget(uri.AbsoluteUri);
            SaveAndRefresh(false);
        }

        private void QueueDockAction(MethodInvoker action)
        {
            if (action == null || IsDisposed || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(action);
            }
            catch (Exception ex)
            {
                ShowDockActionError("dock command", ex);
            }
        }

        private void ShowDockActionError(string action, Exception ex)
        {
            MessageBox.Show(
                "EmilyDesk could not open the " + action + ".\r\n\r\n" +
                (ex == null ? "Unknown error." : ex.Message),
                "EmilyDesk Dock",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void DisposeMenuAfterClose(ContextMenuStrip menu)
        {
            if (menu == null) return;
            menu.Closed += delegate
            {
                // ContextMenuStrip still performs internal work after raising
                // Closed. Disposing it inside that event causes the
                // ObjectDisposedException seen on every dock menu.
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        if (!menu.IsDisposed)
                            menu.Dispose();
                    }));
                }
                catch
                {
                }
            };
        }

        private void ShowAddMenu(Point location)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Application or Shortcut...", null,
                delegate
                {
                    QueueDockAction(new MethodInvoker(AddApplications));
                });
            menu.Items.Add("Folder or Library...", null,
                delegate
                {
                    QueueDockAction(new MethodInvoker(AddFolder));
                });
            menu.Items.Add("Web Link...", null,
                delegate
                {
                    QueueDockAction(new MethodInvoker(AddWebLink));
                });

            var common = new ToolStripMenuItem("Common Locations");
            AddCommonLocation(common, "Desktop",
                Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory));
            AddCommonLocation(common, "Documents",
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments));
            AddCommonLocation(common, "Pictures",
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyPictures));
            AddCommonLocation(common, "Music",
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyMusic));
            AddCommonLocation(common, "Videos",
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyVideos));
            string userProfile = Environment.ExpandEnvironmentVariables(
                "%USERPROFILE%");
            AddCommonLocation(common, "Downloads",
                Path.Combine(userProfile, "Downloads"));
            menu.Items.Add(common);
            DisposeMenuAfterClose(menu);
            menu.Show(this, location);
        }

        private void AddCommonLocation(
            ToolStripMenuItem parent, string label, string path)
        {
            if (parent == null || string.IsNullOrWhiteSpace(path) ||
                !Directory.Exists(path))
                return;
            parent.DropDownItems.Add(label, null, delegate
            {
                QueueDockAction(delegate
                {
                    AddTarget(path);
                    SaveAndRefresh(false);
                });
            });
        }

        private void AddTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return;
            foreach (EmilyDeskDockItem existing in _settings.Items)
                if (string.Equals(existing.Target, target,
                    StringComparison.OrdinalIgnoreCase))
                    return;
            _settings.Items.Add(new EmilyDeskDockItem
            {
                Label = EmilyDeskDockStore.FriendlyName(target),
                Target = target,
                Arguments = string.Empty,
                CustomIconPath = string.Empty
            });
        }

        private void ShowItemMenu(int index, Point location)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Launch", null, delegate
            {
                Launch(_settings.Items[index]);
            });
            menu.Items.Add("Rename", null, delegate
            {
                string name = Prompt.Show(this, "Rename dock item",
                    "Name:", _settings.Items[index].Label);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _settings.Items[index].Label = name.Trim();
                    SaveAndRefresh(false);
                }
            });
            menu.Items.Add("Change Icon...", null, delegate
            {
                ChangeIcon(index);
            });
            if (!string.IsNullOrWhiteSpace(
                _settings.Items[index].CustomIconPath))
                menu.Items.Add("Use Default Icon", null, delegate
                {
                    _settings.Items[index].CustomIconPath = string.Empty;
                    SaveAndRefreshIcon(index);
                });
            menu.Items.Add("Open File Location", null, delegate
            {
                OpenLocation(_settings.Items[index]);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Remove from Dock", null, delegate
            {
                _settings.Items.RemoveAt(index);
                SaveAndRefresh(false);
            });
            DisposeMenuAfterClose(menu);
            menu.Show(this, location);
        }

        private void ShowDockMenu(Point location)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Add Item...", null,
                delegate
                {
                    QueueDockAction(delegate
                    {
                        ShowAddMenu(location);
                    });
                });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Dock Settings...", null,
                delegate
                {
                    EmilyDeskDockController.ShowSettings(this);
                });
            menu.Items.Add("Close Dock", null,
                delegate { EmilyDeskDockController.CloseDock(); });
            DisposeMenuAfterClose(menu);
            menu.Show(this, location);
        }

        private void ChangeIcon(int index)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Choose a dock icon",
                Filter = "Icons and images|*.ico;*.png;*.jpg;*.bmp;*.exe;*.dll|All files|*.*",
                CheckFileExists = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _settings.Items[index].CustomIconPath = dialog.FileName;
                SaveAndRefreshIcon(index);
            }
        }

        private void SaveAndRefreshIcon(int index)
        {
            try
            {
                EmilyDeskDockStore.Save(_settings);
                if (index < 0 || index >= _settings.Items.Count)
                    return;
                Image replacement = ShellIconLoader.Load(
                    _settings.Items[index], ThemeName);
                if (index < _icons.Count)
                {
                    Image previous = _icons[index];
                    _icons[index] = replacement;
                    if (previous != null) previous.Dispose();
                }
                else
                {
                    while (_icons.Count < index) _icons.Add(null);
                    _icons.Add(replacement);
                }
                Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "The dock icon could not be saved.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk Dock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void Launch(EmilyDeskDockItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.Target,
                    Arguments = item.Arguments ?? string.Empty,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "EmilyDesk could not open this dock item.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk Dock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void OpenLocation(EmilyDeskDockItem item)
        {
            try
            {
                if (Directory.Exists(item.Target))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.Target,
                        UseShellExecute = true
                    });
                    return;
                }
                if (File.Exists(item.Target))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + item.Target + "\"",
                        UseShellExecute = true
                    });
                    return;
                }
                Uri uri;
                if (Uri.TryCreate(item.Target, UriKind.Absolute, out uri))
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.Target,
                        UseShellExecute = true
                    });
            }
            catch { }
        }

        private void SaveAndRefresh(bool relayoutOnly)
        {
            try
            {
                if (!relayoutOnly)
                    EmilyDeskDockStore.Save(_settings);
                ReloadIcons();
                LayoutDock();
                Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "The dock change could not be saved.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk Dock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ReloadIcons()
        {
            DisposeIcons();
            if (_settings == null || _settings.Items == null) return;
            foreach (EmilyDeskDockItem item in _settings.Items)
                _icons.Add(ShellIconLoader.Load(item, ThemeName));
        }

        private void DisposeIcons()
        {
            foreach (Image image in _icons)
                if (image != null) image.Dispose();
            _icons.Clear();
        }

        private void DisplaySettingsChanged(object sender, EventArgs e)
        {
            LayoutDock();
            Invalidate();
        }

        private EmilyDeskDockEdge CurrentEdge
        {
            get
            {
                EmilyDeskDockEdge edge;
                return Enum.TryParse(_settings.Edge, true, out edge)
                    ? edge : EmilyDeskDockEdge.Bottom;
            }
        }

        private bool IsHorizontal
        {
            get
            {
                EmilyDeskDockEdge edge = CurrentEdge;
                return edge == EmilyDeskDockEdge.Bottom ||
                    edge == EmilyDeskDockEdge.Top;
            }
        }

        private static GraphicsPath RoundedRectangle(
            RectangleF bounds, float radius)
        {
            float diameter = radius * 2F;
            diameter = Math.Min(diameter,
                Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top,
                diameter, diameter, 180F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Top,
                diameter, diameter, 270F, 90F);
            path.AddArc(bounds.Right - diameter,
                bounds.Bottom - diameter,
                diameter, diameter, 0F, 90F);
            path.AddArc(bounds.Left, bounds.Bottom - diameter,
                diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class DockHoverLabelForm : Form
    {
        private string _label = string.Empty;
        private string _theme = "Industrial";

        public DockHoverLabelForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            BackColor = Color.FromArgb(24, 25, 24);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void ApplyTheme(string theme)
        {
            _theme = EmilyDeskDockStore.NormalizeTheme(theme);
            BackColor = LabelBackColor();
            Invalidate();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WsExTransparent = 0x00000020;
                const int WsExToolWindow = 0x00000080;
                const int WsExNoActivate = 0x08000000;
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExTransparent |
                    WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        public void ShowLabel(
            string label,
            Rectangle iconBounds,
            EmilyDeskDockEdge edge)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                HideLabel();
                return;
            }
            _label = label.Trim();
            Size textSize = TextRenderer.MeasureText(
                _label, SystemFonts.MessageBoxFont);
            Size = new Size(
                Math.Max(58, textSize.Width + 24),
                textSize.Height + 12);

            int x = iconBounds.Left +
                (iconBounds.Width - Width) / 2;
            int y = edge == EmilyDeskDockEdge.Top
                ? iconBounds.Bottom + 8
                : iconBounds.Top - Height - 8;
            Rectangle working = Screen.FromRectangle(
                iconBounds).WorkingArea;
            x = Math.Max(working.Left + 2,
                Math.Min(working.Right - Width - 2, x));
            if (y < working.Top + 2)
                y = iconBounds.Bottom + 8;
            if (y + Height > working.Bottom - 2)
                y = iconBounds.Top - Height - 8;
            Location = new Point(x, y);

            using (GraphicsPath path = RoundedRectangle(
                new RectangleF(0F, 0F, Width, Height), 7F))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
            if (!Visible)
                Show();
            Invalidate();
        }

        public void HideLabel()
        {
            if (Visible) Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF bounds = new RectangleF(
                0.5F, 0.5F, Width - 1F, Height - 1F);
            using (GraphicsPath path = RoundedRectangle(bounds, 7F))
            using (var fill = new LinearGradientBrush(
                bounds, LabelTopColor(), LabelBackColor(),
                LinearGradientMode.Vertical))
            using (var border = new Pen(LabelBorderColor(), 1.2F))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(
                graphics,
                _label,
                SystemFonts.MessageBoxFont,
                Rectangle.Inflate(ClientRectangle, -8, -4),
                LabelTextColor(),
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding);
        }

        private bool IsSteampunk
        {
            get
            {
                return string.Equals(_theme, "Steampunk",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private bool IsArtDeco
        {
            get
            {
                return string.Equals(_theme, "Art Deco",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private bool IsVintage
        {
            get { return ThemeIs("Vintage"); }
        }

        private bool IsModern
        {
            get { return ThemeIs("Modern"); }
        }

        private bool IsBotanical
        {
            get { return ThemeIs("Botanical Nature"); }
        }

        private bool IsWoodland
        {
            get { return ThemeIs("Woodland Nature"); }
        }

        private bool ThemeIs(string value)
        {
            return string.Equals(_theme, value,
                StringComparison.OrdinalIgnoreCase);
        }

        private Color LabelBackColor()
        {
            if (IsVintage) return Color.FromArgb(105, 50, 48);
            if (IsModern) return Color.FromArgb(8, 19, 30);
            if (IsBotanical) return Color.FromArgb(232, 229, 207);
            if (IsWoodland) return Color.FromArgb(23, 45, 34);
            if (IsArtDeco) return Color.FromArgb(7, 13, 14);
            if (IsSteampunk) return Color.FromArgb(35, 20, 16);
            return Color.FromArgb(22, 23, 22);
        }

        private Color LabelTopColor()
        {
            if (IsVintage) return Color.FromArgb(230, 215, 177);
            if (IsModern) return Color.FromArgb(40, 78, 108);
            if (IsBotanical) return Color.FromArgb(251, 247, 232);
            if (IsWoodland) return Color.FromArgb(73, 91, 59);
            if (IsArtDeco) return Color.FromArgb(46, 61, 59);
            if (IsSteampunk) return Color.FromArgb(105, 57, 31);
            return Color.FromArgb(70, 72, 69);
        }

        private Color LabelBorderColor()
        {
            if (IsVintage) return Color.FromArgb(189, 193, 183);
            if (IsModern) return Color.FromArgb(102, 205, 248);
            if (IsBotanical) return Color.FromArgb(199, 119, 137);
            if (IsWoodland) return Color.FromArgb(201, 151, 91);
            if (IsArtDeco) return Color.FromArgb(229, 193, 97);
            if (IsSteampunk) return Color.FromArgb(232, 180, 82);
            return Color.FromArgb(211, 157, 78);
        }

        private Color LabelTextColor()
        {
            if (IsVintage) return Color.FromArgb(64, 42, 32);
            if (IsModern) return Color.FromArgb(238, 249, 255);
            if (IsBotanical) return Color.FromArgb(48, 74, 52);
            if (IsWoodland) return Color.FromArgb(244, 229, 194);
            if (IsArtDeco) return Color.FromArgb(240, 228, 190);
            if (IsSteampunk) return Color.FromArgb(250, 225, 169);
            return Color.FromArgb(246, 236, 213);
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
            path.AddArc(bounds.Left, bounds.Bottom - diameter,
                diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }
    }

    internal static class ShellIconLoader
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeIcon = 0x000000000;

        private enum DockFolderKind
        {
            Folder,
            Desktop,
            Documents,
            Pictures,
            Music,
            Videos,
            Downloads
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct ShFileInfo
        {
            public IntPtr IconHandle;
            public int IconIndex;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string TypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string path,
            uint fileAttributes,
            out ShFileInfo info,
            uint infoSize,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint PrivateExtractIcons(
            string fileName,
            int iconIndex,
            int iconWidth,
            int iconHeight,
            IntPtr[] iconHandles,
            uint[] iconIdentifiers,
            uint iconCount,
            uint flags);

        public static Image Load(EmilyDeskDockItem item)
        {
            return Load(item, "Industrial");
        }

        public static Image Load(
            EmilyDeskDockItem item, string theme)
        {
            if (item == null) return null;
            if (!string.IsNullOrWhiteSpace(item.CustomIconPath) &&
                File.Exists(item.CustomIconPath))
            {
                Image custom = LoadCustom(item.CustomIconPath);
                if (custom != null) return custom;
            }
            if (IsWebTarget(item.Target))
                return CreateWebIcon(theme);
            if (Directory.Exists(item.Target))
                return CreateFolderIcon(item.Target, theme);
            if (IsFileExplorerTarget(item.Target))
            {
                if (string.Equals(theme, "Steampunk",
                    StringComparison.OrdinalIgnoreCase))
                    return CreateSteampunkThisPcIcon();
                if (string.Equals(theme, "Art Deco",
                    StringComparison.OrdinalIgnoreCase))
                    return CreateArtDecoThisPcIcon();
                if (IsNewDockTheme(theme))
                    return CreateCollectionThisPcIcon(theme);
                return CreateIndustrialThisPcIcon();
            }
            Image highResolution = LoadHighResolutionIcon(item.Target);
            if (highResolution != null)
                return highResolution;
            if (!File.Exists(item.Target) &&
                !Directory.Exists(item.Target))
                return SystemIcons.Application.ToBitmap();
            ShFileInfo info;
            IntPtr result = SHGetFileInfo(
                item.Target,
                0,
                out info,
                (uint)Marshal.SizeOf(typeof(ShFileInfo)),
                ShgfiIcon | ShgfiLargeIcon);
            if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
                return SystemIcons.Application.ToBitmap();
            try
            {
                using (Icon icon = (Icon)Icon.FromHandle(
                    info.IconHandle).Clone())
                    return icon.ToBitmap();
            }
            finally
            {
                DestroyIcon(info.IconHandle);
            }
        }

        private static Image LoadHighResolutionIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".lnk",
                StringComparison.OrdinalIgnoreCase))
            {
                string shortcutIcon;
                int shortcutIconIndex;
                if (ResolveShortcutIcon(
                    path, out shortcutIcon, out shortcutIconIndex))
                {
                    Image explicitIcon = ExtractIcon(
                        shortcutIcon, shortcutIconIndex);
                    if (explicitIcon != null) return explicitIcon;
                }
                string shortcutTarget = ResolveShortcutTarget(path);
                return string.IsNullOrWhiteSpace(shortcutTarget) ||
                    string.Equals(shortcutTarget, path,
                        StringComparison.OrdinalIgnoreCase)
                    ? null
                    : LoadHighResolutionIcon(shortcutTarget);
            }
            if (!string.Equals(extension, ".exe",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".dll",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".ico",
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return ExtractIcon(path, 0);
        }

        private static Image ExtractIcon(string path, int iconIndex)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            var handles = new IntPtr[1];
            var identifiers = new uint[1];
            try
            {
                uint extracted = PrivateExtractIcons(
                    path, iconIndex, 256, 256,
                    handles, identifiers, 1, 0);
                if (extracted == 0 || extracted == uint.MaxValue ||
                    handles[0] == IntPtr.Zero)
                    return null;
                using (Icon icon = (Icon)Icon.FromHandle(
                    handles[0]).Clone())
                    return icon.ToBitmap();
            }
            catch
            {
                return null;
            }
            finally
            {
                if (handles[0] != IntPtr.Zero)
                    DestroyIcon(handles[0]);
            }
        }

        private static bool ResolveShortcutIcon(
            string path, out string iconPath, out int iconIndex)
        {
            iconPath = string.Empty;
            iconIndex = 0;
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, shell, new object[] { path });
                if (shortcut == null) return false;
                string location = Convert.ToString(
                    shortcut.GetType().InvokeMember(
                        "IconLocation",
                        System.Reflection.BindingFlags.GetProperty,
                        null, shortcut, null)) ?? string.Empty;
                int comma = location.LastIndexOf(',');
                if (comma >= 0)
                {
                    int parsed;
                    if (int.TryParse(location.Substring(comma + 1).Trim(),
                        out parsed))
                        iconIndex = parsed;
                    location = location.Substring(0, comma);
                }
                iconPath = Environment.ExpandEnvironmentVariables(
                    location.Trim().Trim('"'));
                return File.Exists(iconPath);
            }
            catch { return false; }
            finally
            {
                try
                {
                    if (shortcut != null && Marshal.IsComObject(shortcut))
                        Marshal.FinalReleaseComObject(shortcut);
                }
                catch { }
                try
                {
                    if (shell != null && Marshal.IsComObject(shell))
                        Marshal.FinalReleaseComObject(shell);
                }
                catch { }
            }
        }

        private static string ResolveShortcutTarget(string path)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return string.Empty;
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { path });
                if (shortcut == null) return string.Empty;
                object target = shortcut.GetType().InvokeMember(
                    "TargetPath",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    shortcut,
                    null);
                return Convert.ToString(target) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                try
                {
                    if (shortcut != null && Marshal.IsComObject(shortcut))
                        Marshal.FinalReleaseComObject(shortcut);
                }
                catch { }
                try
                {
                    if (shell != null && Marshal.IsComObject(shell))
                        Marshal.FinalReleaseComObject(shell);
                }
                catch { }
            }
        }

        private static bool IsWebTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;
            if (string.Equals(Path.GetExtension(target), ".url",
                StringComparison.OrdinalIgnoreCase))
                return true;
            Uri uri;
            return Uri.TryCreate(target, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsFileExplorerTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;
            string candidate = target;
            if (string.Equals(Path.GetExtension(candidate), ".lnk",
                StringComparison.OrdinalIgnoreCase))
            {
                string resolved = ResolveShortcutTarget(candidate);
                if (!string.IsNullOrWhiteSpace(resolved))
                    candidate = resolved;
            }
            return string.Equals(Path.GetFileName(candidate),
                "explorer.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static Image CreateWebIcon(string theme)
        {
            if (string.Equals(theme, "Steampunk",
                StringComparison.OrdinalIgnoreCase))
                return CreateSteampunkWebIcon();
            if (string.Equals(theme, "Art Deco",
                StringComparison.OrdinalIgnoreCase))
                return CreateArtDecoWebIcon();
            if (IsNewDockTheme(theme))
                return CreateCollectionWebIcon(theme);
            return CreateIndustrialWebIcon();
        }

        private static bool IsNewDockTheme(string theme)
        {
            return string.Equals(theme, "Vintage",
                       StringComparison.OrdinalIgnoreCase) ||
                string.Equals(theme, "Modern",
                       StringComparison.OrdinalIgnoreCase) ||
                string.Equals(theme, "Botanical Nature",
                       StringComparison.OrdinalIgnoreCase) ||
                string.Equals(theme, "Woodland Nature",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static Image CreateIndustrialWebIcon()
        {
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                RectangleF outer = new RectangleF(8F, 8F, 112F, 112F);
                using (var shadow = new SolidBrush(
                    Color.FromArgb(85, 0, 0, 0)))
                    graphics.FillEllipse(shadow,
                        RectangleF.Inflate(outer, 2F, 3F));
                using (var metal = new LinearGradientBrush(
                    outer,
                    Color.FromArgb(73, 77, 78),
                    Color.FromArgb(20, 22, 24),
                    LinearGradientMode.ForwardDiagonal))
                using (var rim = new Pen(
                    Color.FromArgb(216, 157, 76), 4F))
                {
                    graphics.FillEllipse(metal, outer);
                    graphics.DrawEllipse(rim, outer);
                }

                RectangleF globe = new RectangleF(27F, 25F, 74F, 74F);
                using (var globeFill = new LinearGradientBrush(
                    globe,
                    Color.FromArgb(73, 153, 196),
                    Color.FromArgb(24, 65, 91),
                    LinearGradientMode.Vertical))
                using (var globeEdge = new Pen(
                    Color.FromArgb(198, 224, 232), 2.5F))
                using (var grid = new Pen(
                    Color.FromArgb(175, 219, 229), 2F))
                {
                    graphics.FillEllipse(globeFill, globe);
                    graphics.DrawEllipse(globeEdge, globe);
                    graphics.DrawEllipse(grid,
                        new RectangleF(45F, 25F, 38F, 74F));
                    graphics.DrawLine(grid, 28F, 62F, 100F, 62F);
                    graphics.DrawArc(grid,
                        new RectangleF(29F, 39F, 70F, 46F),
                        180F, 180F);
                    graphics.DrawArc(grid,
                        new RectangleF(29F, 39F, 70F, 46F),
                        0F, 180F);
                }
                using (var labelFill = new SolidBrush(
                    Color.FromArgb(211, 151, 69)))
                using (var labelFont = new Font("Segoe UI", 13F,
                    FontStyle.Bold, GraphicsUnit.Pixel))
                using (var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                    graphics.DrawString("WEB", labelFont, labelFill,
                        new RectangleF(22F, 94F, 84F, 22F), format);
            }
            return bitmap;
        }

        private static Image CreateSteampunkWebIcon()
        {
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                RectangleF shadowBounds = new RectangleF(
                    9F, 11F, 110F, 110F);
                using (var shadow = new SolidBrush(
                    Color.FromArgb(90, 0, 0, 0)))
                    graphics.FillEllipse(shadow, shadowBounds);

                RectangleF porthole = new RectangleF(
                    8F, 7F, 112F, 112F);
                using (var brass = new LinearGradientBrush(
                    porthole,
                    Color.FromArgb(244, 195, 91),
                    Color.FromArgb(82, 43, 20),
                    LinearGradientMode.ForwardDiagonal))
                using (var edge = new Pen(
                    Color.FromArgb(64, 31, 17), 2.5F))
                {
                    graphics.FillEllipse(brass, porthole);
                    graphics.DrawEllipse(edge, porthole);
                }

                RectangleF glass = new RectangleF(
                    22F, 21F, 84F, 84F);
                using (var glassFill = new LinearGradientBrush(
                    glass,
                    Color.FromArgb(54, 104, 111),
                    Color.FromArgb(13, 38, 45),
                    LinearGradientMode.Vertical))
                using (var glassEdge = new Pen(
                    Color.FromArgb(225, 173, 78), 2.5F))
                using (var grid = new Pen(
                    Color.FromArgb(190, 210, 183), 2F))
                {
                    graphics.FillEllipse(glassFill, glass);
                    graphics.DrawEllipse(glassEdge, glass);
                    graphics.DrawEllipse(grid,
                        new RectangleF(43F, 21F, 42F, 84F));
                    graphics.DrawLine(grid, 22F, 63F, 106F, 63F);
                    graphics.DrawArc(grid,
                        new RectangleF(24F, 37F, 80F, 52F),
                        180F, 180F);
                    graphics.DrawArc(grid,
                        new RectangleF(24F, 37F, 80F, 52F),
                        0F, 180F);
                }

                DrawIconBolt(graphics, 64F, 13F);
                DrawIconBolt(graphics, 64F, 113F);
                DrawIconBolt(graphics, 14F, 63F);
                DrawIconBolt(graphics, 114F, 63F);
                DrawSmallIconGear(graphics, 98F, 95F, 12F);
            }
            return bitmap;
        }

        private static Image CreateArtDecoWebIcon()
        {
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                PointF[] outer = ArtDecoOctagon(64F, 64F, 57F);
                PointF[] inner = ArtDecoOctagon(64F, 64F, 46F);
                using (var shadow = new SolidBrush(
                    Color.FromArgb(95, 0, 0, 0)))
                    graphics.FillPolygon(shadow,
                        Offset(outer, 1F, 4F));
                using (var gold = new LinearGradientBrush(
                    new RectangleF(7F, 7F, 114F, 114F),
                    Color.FromArgb(244, 214, 128),
                    Color.FromArgb(128, 83, 24),
                    LinearGradientMode.ForwardDiagonal))
                using (var edge = new Pen(
                    Color.FromArgb(65, 42, 18), 2F))
                {
                    graphics.FillPolygon(gold, outer);
                    graphics.DrawPolygon(edge, outer);
                }
                using (var lacquer = new LinearGradientBrush(
                    new RectangleF(18F, 18F, 92F, 92F),
                    Color.FromArgb(43, 81, 78),
                    Color.FromArgb(8, 18, 21),
                    LinearGradientMode.Vertical))
                using (var ivory = new Pen(
                    Color.FromArgb(236, 225, 187), 2F))
                {
                    graphics.FillPolygon(lacquer, inner);
                    graphics.DrawPolygon(ivory, inner);
                }

                RectangleF globe = new RectangleF(34F, 32F, 60F, 60F);
                using (var globeEdge = new Pen(
                    Color.FromArgb(232, 193, 91), 2.2F))
                using (var grid = new Pen(
                    Color.FromArgb(226, 218, 184), 1.6F))
                {
                    graphics.DrawEllipse(globeEdge, globe);
                    graphics.DrawEllipse(grid,
                        new RectangleF(49F, 32F, 30F, 60F));
                    graphics.DrawLine(grid, 34F, 62F, 94F, 62F);
                    graphics.DrawArc(grid,
                        new RectangleF(36F, 43F, 56F, 38F),
                        180F, 180F);
                    graphics.DrawArc(grid,
                        new RectangleF(36F, 43F, 56F, 38F),
                        0F, 180F);
                }
                using (var ray = new Pen(
                    Color.FromArgb(210, 158, 55), 1.4F))
                {
                    graphics.DrawLine(ray, 64F, 17F, 64F, 27F);
                    graphics.DrawLine(ray, 25F, 63F, 31F, 63F);
                    graphics.DrawLine(ray, 97F, 63F, 103F, 63F);
                    graphics.DrawLine(ray, 41F, 99F, 47F, 92F);
                    graphics.DrawLine(ray, 87F, 99F, 81F, 92F);
                }
            }
            return bitmap;
        }

        private static Image CreateCollectionWebIcon(string theme)
        {
            Color light, dark, accent, ink;
            GetCollectionPalette(theme, out light, out dark,
                out accent, out ink);
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                RectangleF frame = new RectangleF(9F, 9F, 110F, 110F);
                using (var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    graphics.FillEllipse(shadow, 10F, 13F, 110F, 110F);
                using (var fill = new LinearGradientBrush(frame, light, dark,
                    LinearGradientMode.ForwardDiagonal))
                using (var edge = new Pen(accent, 3F))
                {
                    if (string.Equals(theme, "Modern",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        graphics.FillEllipse(fill, frame);
                        graphics.DrawEllipse(edge, frame);
                    }
                    else
                    {
                        using (GraphicsPath path = RoundedIconRectangle(frame,
                            string.Equals(theme, "Vintage",
                                StringComparison.OrdinalIgnoreCase) ? 16F : 25F))
                        {
                            graphics.FillPath(fill, path);
                            graphics.DrawPath(edge, path);
                        }
                    }
                }

                RectangleF globe = new RectangleF(29F, 27F, 70F, 70F);
                using (var globeFill = new SolidBrush(ink))
                using (var grid = new Pen(light, 2F))
                using (var globeEdge = new Pen(accent, 2.5F))
                {
                    graphics.FillEllipse(globeFill, globe);
                    graphics.DrawEllipse(globeEdge, globe);
                    graphics.DrawEllipse(grid, 46F, 27F, 36F, 70F);
                    graphics.DrawLine(grid, 29F, 62F, 99F, 62F);
                    graphics.DrawArc(grid, 31F, 39F, 66F, 46F, 180F, 180F);
                    graphics.DrawArc(grid, 31F, 39F, 66F, 46F, 0F, 180F);
                }
                DrawCollectionIconDecoration(graphics, theme, 98F, 97F);
            }
            return bitmap;
        }

        private static Image CreateCollectionThisPcIcon(string theme)
        {
            Color light, dark, accent, ink;
            GetCollectionPalette(theme, out light, out dark,
                out accent, out ink);
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                RectangleF caseBounds = new RectangleF(9F, 11F, 110F, 82F);
                using (GraphicsPath casePath = RoundedIconRectangle(caseBounds,
                    string.Equals(theme, "Modern",
                        StringComparison.OrdinalIgnoreCase) ? 18F : 11F))
                using (var body = new LinearGradientBrush(caseBounds, light,
                    dark, LinearGradientMode.ForwardDiagonal))
                using (var edge = new Pen(accent, 2.5F))
                {
                    graphics.FillPath(body, casePath);
                    graphics.DrawPath(edge, casePath);
                }
                RectangleF screen = new RectangleF(22F, 23F, 84F, 54F);
                using (GraphicsPath screenPath = RoundedIconRectangle(screen, 5F))
                using (var glass = new LinearGradientBrush(screen, ink,
                    Color.FromArgb(18, 27, 28), LinearGradientMode.Vertical))
                using (var rim = new Pen(accent, 1.7F))
                {
                    graphics.FillPath(glass, screenPath);
                    graphics.DrawPath(rim, screenPath);
                }
                DrawComputerFolder(graphics, accent, dark, light);
                using (var stand = new LinearGradientBrush(
                    new RectangleF(38F, 89F, 52F, 27F), light, dark,
                    LinearGradientMode.Vertical))
                using (var standEdge = new Pen(accent, 1.5F))
                {
                    PointF[] support =
                    {
                        new PointF(56F, 91F), new PointF(72F, 91F),
                        new PointF(78F, 106F), new PointF(50F, 106F)
                    };
                    graphics.FillPolygon(stand, support);
                    graphics.DrawPolygon(standEdge, support);
                    graphics.FillRectangle(stand, 35F, 105F, 58F, 10F);
                    graphics.DrawRectangle(standEdge, 35F, 105F, 58F, 10F);
                }
                DrawCollectionIconDecoration(graphics, theme, 105F, 99F);
            }
            return bitmap;
        }

        private static void GetCollectionPalette(string theme,
            out Color light, out Color dark, out Color accent, out Color ink)
        {
            if (string.Equals(theme, "Modern",
                StringComparison.OrdinalIgnoreCase))
            {
                light = Color.FromArgb(74, 116, 151);
                dark = Color.FromArgb(8, 19, 30);
                accent = Color.FromArgb(91, 205, 249);
                ink = Color.FromArgb(25, 72, 98);
            }
            else if (string.Equals(theme, "Botanical Nature",
                StringComparison.OrdinalIgnoreCase))
            {
                light = Color.FromArgb(249, 244, 224);
                dark = Color.FromArgb(151, 169, 132);
                accent = Color.FromArgb(205, 119, 139);
                ink = Color.FromArgb(61, 91, 66);
            }
            else if (string.Equals(theme, "Woodland Nature",
                StringComparison.OrdinalIgnoreCase))
            {
                light = Color.FromArgb(173, 119, 72);
                dark = Color.FromArgb(54, 35, 25);
                accent = Color.FromArgb(207, 157, 91);
                ink = Color.FromArgb(35, 67, 48);
            }
            else
            {
                light = Color.FromArgb(242, 229, 193);
                dark = Color.FromArgb(117, 57, 53);
                accent = Color.FromArgb(199, 204, 194);
                ink = Color.FromArgb(72, 51, 38);
            }
        }

        private static void DrawCollectionIconDecoration(Graphics graphics,
            string theme, float x, float y)
        {
            if (string.Equals(theme, "Modern",
                StringComparison.OrdinalIgnoreCase))
            {
                using (var glow = new SolidBrush(Color.FromArgb(105, 220, 255)))
                {
                    graphics.FillEllipse(glow, x - 6F, y - 6F, 12F, 12F);
                    graphics.FillEllipse(glow, x - 17F, y + 4F, 5F, 5F);
                }
            }
            else if (string.Equals(theme, "Botanical Nature",
                StringComparison.OrdinalIgnoreCase))
            {
                DrawIconLeaf(graphics, x - 8F, y, 14F,
                    Color.FromArgb(84, 122, 76));
                DrawIconFlower(graphics, x + 4F, y + 3F,
                    Color.FromArgb(211, 126, 145));
            }
            else if (string.Equals(theme, "Woodland Nature",
                StringComparison.OrdinalIgnoreCase))
            {
                DrawIconLeaf(graphics, x - 10F, y, 16F,
                    Color.FromArgb(75, 112, 68));
                using (var nut = new SolidBrush(Color.FromArgb(177, 104, 54)))
                using (var cap = new SolidBrush(Color.FromArgb(74, 48, 34)))
                {
                    graphics.FillEllipse(nut, x + 3F, y, 9F, 13F);
                    graphics.FillRectangle(cap, x + 2F, y - 2F, 11F, 5F);
                }
            }
            else
            {
                using (var chrome = new SolidBrush(Color.FromArgb(224, 226, 216)))
                using (var red = new SolidBrush(Color.FromArgb(131, 57, 54)))
                {
                    graphics.FillEllipse(chrome, x - 8F, y - 8F, 16F, 16F);
                    graphics.FillEllipse(red, x - 4F, y - 4F, 8F, 8F);
                }
            }
        }

        private static void DrawIconLeaf(Graphics graphics, float x, float y,
            float size, Color colour)
        {
            using (var path = new GraphicsPath())
            using (var fill = new SolidBrush(colour))
            using (var vein = new Pen(Color.FromArgb(220, 225, 202), 1F))
            {
                path.AddBezier(x, y, x + size * .25F, y - size * .7F,
                    x + size * .9F, y - size * .55F, x + size, y);
                path.AddBezier(x + size, y, x + size * .75F, y + size * .55F,
                    x + size * .2F, y + size * .45F, x, y);
                path.CloseFigure();
                graphics.FillPath(fill, path);
                graphics.DrawLine(vein, x + 2F, y, x + size - 2F, y);
            }
        }

        private static void DrawIconFlower(Graphics graphics, float x, float y,
            Color colour)
        {
            using (var petal = new SolidBrush(colour))
            using (var centre = new SolidBrush(Color.FromArgb(232, 184, 77)))
            {
                for (int i = 0; i < 5; i++)
                {
                    double angle = i * Math.PI * 2D / 5D;
                    graphics.FillEllipse(petal,
                        x + (float)Math.Cos(angle) * 5F - 3F,
                        y + (float)Math.Sin(angle) * 5F - 3F, 6F, 6F);
                }
                graphics.FillEllipse(centre, x - 2F, y - 2F, 4F, 4F);
            }
        }

        private static Image CreateIndustrialThisPcIcon()
        {
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                RectangleF caseBounds = new RectangleF(9F, 12F, 110F, 79F);
                using (GraphicsPath casePath = RoundedIconRectangle(
                    caseBounds, 11F))
                using (var steel = new LinearGradientBrush(
                    caseBounds, Color.FromArgb(151, 156, 153),
                    Color.FromArgb(42, 47, 48),
                    LinearGradientMode.ForwardDiagonal))
                using (var edge = new Pen(
                    Color.FromArgb(25, 28, 29), 2.5F))
                {
                    graphics.FillPath(steel, casePath);
                    graphics.DrawPath(edge, casePath);
                }
                RectangleF screen = new RectangleF(22F, 24F, 84F, 52F);
                using (var glass = new LinearGradientBrush(screen,
                    Color.FromArgb(57, 91, 105),
                    Color.FromArgb(12, 25, 31),
                    LinearGradientMode.Vertical))
                using (var rim = new Pen(
                    Color.FromArgb(225, 166, 60), 2F))
                {
                    graphics.FillRectangle(glass, screen);
                    graphics.DrawRectangle(rim,
                        screen.X, screen.Y, screen.Width, screen.Height);
                }
                DrawComputerFolder(graphics,
                    Color.FromArgb(235, 181, 78),
                    Color.FromArgb(138, 84, 31),
                    Color.FromArgb(255, 222, 145));
                using (var stand = new LinearGradientBrush(
                    new RectangleF(44F, 88F, 40F, 27F),
                    Color.FromArgb(133, 137, 133),
                    Color.FromArgb(42, 44, 43),
                    LinearGradientMode.Vertical))
                using (var standEdge = new Pen(
                    Color.FromArgb(24, 25, 24), 2F))
                {
                    graphics.FillPolygon(stand, new[]
                    {
                        new PointF(55F, 89F), new PointF(73F, 89F),
                        new PointF(78F, 106F), new PointF(50F, 106F)
                    });
                    graphics.FillRectangle(stand,
                        new RectangleF(36F, 105F, 56F, 10F));
                    graphics.DrawRectangle(standEdge,
                        36F, 105F, 56F, 10F);
                }
                DrawIndustrialIconRivet(graphics, 17F, 20F);
                DrawIndustrialIconRivet(graphics, 111F, 20F);
            }
            return bitmap;
        }

        private static Image CreateArtDecoThisPcIcon()
        {
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                PointF[] caseShape =
                {
                    new PointF(19F, 10F), new PointF(109F, 10F),
                    new PointF(119F, 20F), new PointF(119F, 85F),
                    new PointF(109F, 95F), new PointF(19F, 95F),
                    new PointF(9F, 85F), new PointF(9F, 20F)
                };
                using (var shadow = new SolidBrush(
                    Color.FromArgb(100, 0, 0, 0)))
                    graphics.FillPolygon(shadow,
                        Offset(caseShape, 1F, 4F));
                using (var gold = new LinearGradientBrush(
                    new RectangleF(9F, 10F, 110F, 85F),
                    Color.FromArgb(243, 214, 129),
                    Color.FromArgb(119, 75, 20),
                    LinearGradientMode.ForwardDiagonal))
                using (var dark = new Pen(
                    Color.FromArgb(54, 37, 19), 2F))
                {
                    graphics.FillPolygon(gold, caseShape);
                    graphics.DrawPolygon(dark, caseShape);
                }
                RectangleF screen = new RectangleF(23F, 24F, 82F, 53F);
                using (var lacquer = new LinearGradientBrush(screen,
                    Color.FromArgb(44, 77, 73),
                    Color.FromArgb(7, 16, 18),
                    LinearGradientMode.Vertical))
                using (var ivory = new Pen(
                    Color.FromArgb(235, 224, 187), 1.5F))
                {
                    graphics.FillRectangle(lacquer, screen);
                    graphics.DrawRectangle(ivory,
                        screen.X, screen.Y, screen.Width, screen.Height);
                }
                DrawComputerFolder(graphics,
                    Color.FromArgb(238, 205, 111),
                    Color.FromArgb(149, 96, 29),
                    Color.FromArgb(250, 235, 182));
                using (var stand = new SolidBrush(
                    Color.FromArgb(33, 43, 42)))
                using (var goldEdge = new Pen(
                    Color.FromArgb(222, 181, 76), 2F))
                {
                    PointF[] baseShape =
                    {
                        new PointF(54F, 94F), new PointF(74F, 94F),
                        new PointF(80F, 108F), new PointF(95F, 114F),
                        new PointF(33F, 114F), new PointF(48F, 108F)
                    };
                    graphics.FillPolygon(stand, baseShape);
                    graphics.DrawPolygon(goldEdge, baseShape);
                }
                using (var ray = new Pen(
                    Color.FromArgb(226, 190, 91), 1.3F))
                {
                    graphics.DrawLine(ray, 14F, 52F, 21F, 52F);
                    graphics.DrawLine(ray, 107F, 52F, 114F, 52F);
                    graphics.DrawLine(ray, 64F, 14F, 64F, 21F);
                }
            }
            return bitmap;
        }

        private static void DrawComputerFolder(Graphics graphics,
            Color light, Color dark, Color edge)
        {
            PointF[] folder =
            {
                new PointF(35F, 48F), new PointF(51F, 48F),
                new PointF(57F, 42F), new PointF(88F, 42F),
                new PointF(93F, 69F), new PointF(38F, 69F)
            };
            using (var fill = new LinearGradientBrush(
                new RectangleF(35F, 42F, 58F, 27F),
                light, dark, LinearGradientMode.Vertical))
            using (var outline = new Pen(edge, 1.5F))
            {
                graphics.FillPolygon(fill, folder);
                graphics.DrawPolygon(outline, folder);
            }
        }

        private static void DrawIndustrialIconRivet(
            Graphics graphics, float x, float y)
        {
            using (var fill = new SolidBrush(
                Color.FromArgb(45, 48, 47)))
            using (var edge = new Pen(
                Color.FromArgb(211, 174, 100), 1F))
            {
                graphics.FillEllipse(fill,
                    new RectangleF(x - 3F, y - 3F, 6F, 6F));
                graphics.DrawEllipse(edge,
                    new RectangleF(x - 3F, y - 3F, 6F, 6F));
            }
        }

        private static PointF[] ArtDecoOctagon(
            float cx, float cy, float radius)
        {
            var points = new PointF[8];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = -Math.PI / 8D + i * Math.PI / 4D;
                points[i] = new PointF(
                    cx + (float)Math.Cos(angle) * radius,
                    cy + (float)Math.Sin(angle) * radius);
            }
            return points;
        }

        private static Image CreateSteampunkThisPcIcon()
        {
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                RectangleF shadow = new RectangleF(13F, 17F, 102F, 79F);
                using (GraphicsPath shadowPath = RoundedIconRectangle(
                    shadow, 13F))
                using (var shadowFill = new SolidBrush(
                    Color.FromArgb(95, 0, 0, 0)))
                    graphics.FillPath(shadowFill, shadowPath);

                RectangleF caseBounds = new RectangleF(
                    10F, 11F, 108F, 82F);
                using (GraphicsPath casePath = RoundedIconRectangle(
                    caseBounds, 13F))
                using (var brass = new LinearGradientBrush(
                    caseBounds,
                    Color.FromArgb(244, 195, 93),
                    Color.FromArgb(91, 47, 21),
                    LinearGradientMode.ForwardDiagonal))
                using (var caseEdge = new Pen(
                    Color.FromArgb(54, 27, 16), 2.5F))
                {
                    graphics.FillPath(brass, casePath);
                    graphics.DrawPath(caseEdge, casePath);
                }

                RectangleF screen = new RectangleF(
                    23F, 23F, 82F, 55F);
                using (GraphicsPath screenPath = RoundedIconRectangle(
                    screen, 7F))
                using (var glass = new LinearGradientBrush(
                    screen,
                    Color.FromArgb(66, 116, 116),
                    Color.FromArgb(15, 37, 42),
                    LinearGradientMode.Vertical))
                using (var screenEdge = new Pen(
                    Color.FromArgb(52, 29, 19), 3F))
                {
                    graphics.FillPath(glass, screenPath);
                    graphics.DrawPath(screenEdge, screenPath);
                }

                PointF[] folder =
                {
                    new PointF(35F, 48F),
                    new PointF(52F, 48F),
                    new PointF(58F, 42F),
                    new PointF(88F, 42F),
                    new PointF(92F, 68F),
                    new PointF(38F, 68F)
                };
                using (var folderFill = new LinearGradientBrush(
                    new RectangleF(35F, 42F, 57F, 27F),
                    Color.FromArgb(239, 182, 78),
                    Color.FromArgb(145, 75, 31),
                    LinearGradientMode.Vertical))
                using (var folderEdge = new Pen(
                    Color.FromArgb(253, 220, 143), 1.5F))
                {
                    graphics.FillPolygon(folderFill, folder);
                    graphics.DrawPolygon(folderEdge, folder);
                }

                using (var stand = new LinearGradientBrush(
                    new RectangleF(49F, 88F, 30F, 25F),
                    Color.FromArgb(211, 147, 59),
                    Color.FromArgb(78, 40, 21),
                    LinearGradientMode.Vertical))
                using (var standEdge = new Pen(
                    Color.FromArgb(54, 27, 16), 2F))
                {
                    graphics.FillPolygon(stand, new[]
                    {
                        new PointF(57F, 89F),
                        new PointF(71F, 89F),
                        new PointF(76F, 107F),
                        new PointF(52F, 107F)
                    });
                    graphics.DrawPolygon(standEdge, new[]
                    {
                        new PointF(57F, 89F),
                        new PointF(71F, 89F),
                        new PointF(76F, 107F),
                        new PointF(52F, 107F)
                    });
                    graphics.FillRectangle(stand,
                        new RectangleF(38F, 105F, 52F, 10F));
                    graphics.DrawRectangle(standEdge,
                        38F, 105F, 52F, 10F);
                }

                DrawIconBolt(graphics, 19F, 20F);
                DrawIconBolt(graphics, 109F, 20F);
                DrawSmallIconGear(graphics, 99F, 98F, 15F);
            }
            return bitmap;
        }

        private static GraphicsPath RoundedIconRectangle(
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
            path.AddArc(bounds.Left, bounds.Bottom - diameter,
                diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        private static Image CreateFolderIcon(
            string path, string theme)
        {
            if (string.Equals(theme, "Steampunk",
                StringComparison.OrdinalIgnoreCase))
                return CreateSteampunkFolderIcon(path);
            if (string.Equals(theme, "Art Deco",
                StringComparison.OrdinalIgnoreCase))
                return CreateArtDecoFolderIcon(path);
            if (IsNewDockTheme(theme))
                return CreateCollectionFolderIcon(path, theme);
            return CreateIndustrialFolderIcon(path);
        }

        private static Image CreateCollectionFolderIcon(
            string path, string theme)
        {
            DockFolderKind kind = GetFolderKind(path);
            Color light, dark, accent, ink;
            GetCollectionPalette(theme, out light, out dark,
                out accent, out ink);
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                PointF[] backShape =
                {
                    new PointF(8F, 36F), new PointF(43F, 36F),
                    new PointF(55F, 47F), new PointF(120F, 47F),
                    new PointF(114F, 112F), new PointF(14F, 112F)
                };
                using (var shadow = new SolidBrush(Color.FromArgb(85, 0, 0, 0)))
                    graphics.FillPolygon(shadow, Offset(backShape, 1F, 5F));
                using (var back = new SolidBrush(dark))
                using (var backEdge = new Pen(accent, 2F))
                {
                    graphics.FillPolygon(back, backShape);
                    graphics.DrawPolygon(backEdge, backShape);
                }
                PointF[] faceShape =
                {
                    new PointF(10F, 51F), new PointF(118F, 51F),
                    new PointF(110F, 112F), new PointF(17F, 112F)
                };
                using (var face = new LinearGradientBrush(
                    new RectangleF(10F, 51F, 108F, 61F), light, dark,
                    LinearGradientMode.Vertical))
                using (var faceEdge = new Pen(accent, 2.2F))
                {
                    graphics.FillPolygon(face, faceShape);
                    graphics.DrawPolygon(faceEdge, faceShape);
                }

                if (string.Equals(theme, "Vintage",
                    StringComparison.OrdinalIgnoreCase))
                {
                    using (var label = new SolidBrush(Color.FromArgb(239, 225, 190)))
                    using (var red = new Pen(Color.FromArgb(134, 60, 56), 2F))
                    {
                        graphics.FillRectangle(label, 35F, 64F, 57F, 27F);
                        graphics.DrawRectangle(red, 35F, 64F, 57F, 27F);
                        graphics.DrawLine(red, 42F, 74F, 84F, 74F);
                        graphics.DrawLine(red, 42F, 82F, 75F, 82F);
                    }
                }
                else if (string.Equals(theme, "Modern",
                    StringComparison.OrdinalIgnoreCase))
                {
                    using (var bar = new Pen(Color.FromArgb(103, 218, 255), 3F))
                    {
                        graphics.DrawLine(bar, 27F, 68F, 101F, 68F);
                        graphics.DrawLine(bar, 27F, 76F, 83F, 76F);
                    }
                    DrawCollectionIconDecoration(graphics, theme, 94F, 94F);
                }
                else
                {
                    DrawCollectionIconDecoration(graphics, theme, 90F, 87F);
                    if (string.Equals(theme, "Woodland Nature",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        using (var grain = new Pen(Color.FromArgb(211, 166, 104), 1F))
                        {
                            graphics.DrawArc(grain, 23F, 72F, 49F, 22F, 190F, 150F);
                            graphics.DrawArc(grain, 37F, 82F, 56F, 18F, 10F, 150F);
                        }
                    }
                }
                if (kind != DockFolderKind.Folder)
                    DrawCollectionFolderBadge(graphics, kind, theme);
            }
            return bitmap;
        }

        private static void DrawCollectionFolderBadge(Graphics graphics,
            DockFolderKind kind, string theme)
        {
            Color light, dark, accent, ink;
            GetCollectionPalette(theme, out light, out dark,
                out accent, out ink);
            RectangleF badge = new RectangleF(42F, 67F, 44F, 34F);
            using (var fill = new LinearGradientBrush(badge, ink, dark,
                LinearGradientMode.Vertical))
            using (var edge = new Pen(accent, 1.8F))
            {
                graphics.FillEllipse(fill, badge);
                graphics.DrawEllipse(edge, badge);
            }
            string mark = kind == DockFolderKind.Desktop ? "PC"
                : kind == DockFolderKind.Documents ? "DOC"
                : kind == DockFolderKind.Pictures ? "PIC"
                : kind == DockFolderKind.Music ? "MUS"
                : kind == DockFolderKind.Videos ? "VID" : "DOWN";
            float size = mark.Length > 3 ? 9F : 11F;
            using (var font = new Font("Segoe UI", size,
                FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(light))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
                graphics.DrawString(mark, font, brush, badge, format);
        }

        private static Image CreateIndustrialFolderIcon(string path)
        {
            DockFolderKind kind = GetFolderKind(path);
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                PointF[] folderShape =
                {
                    new PointF(8F, 37F),
                    new PointF(44F, 37F),
                    new PointF(55F, 48F),
                    new PointF(120F, 48F),
                    new PointF(114F, 112F),
                    new PointF(14F, 112F)
                };
                using (var shadow = new SolidBrush(
                    Color.FromArgb(90, 0, 0, 0)))
                    graphics.FillPolygon(shadow,
                        Offset(folderShape, 1F, 4F));
                using (var back = new SolidBrush(
                    Color.FromArgb(126, 88, 42)))
                    graphics.FillPolygon(back, folderShape);

                RectangleF faceBounds = new RectangleF(
                    10F, 48F, 108F, 62F);
                using (var face = new LinearGradientBrush(
                    faceBounds,
                    Color.FromArgb(233, 183, 83),
                    Color.FromArgb(154, 94, 36),
                    LinearGradientMode.Vertical))
                using (var edge = new Pen(
                    Color.FromArgb(255, 215, 133), 2F))
                {
                    graphics.FillPolygon(face, new[]
                    {
                        new PointF(10F, 50F),
                        new PointF(118F, 50F),
                        new PointF(111F, 112F),
                        new PointF(16F, 112F)
                    });
                    graphics.DrawPolygon(edge, new[]
                    {
                        new PointF(10F, 50F),
                        new PointF(118F, 50F),
                        new PointF(111F, 112F),
                        new PointF(16F, 112F)
                    });
                }
                if (kind != DockFolderKind.Folder)
                    DrawFolderBadge(graphics, kind);
            }
            return bitmap;
        }

        private static Image CreateArtDecoFolderIcon(string path)
        {
            DockFolderKind kind = GetFolderKind(path);
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                PointF[] rear =
                {
                    new PointF(10F, 37F), new PointF(44F, 37F),
                    new PointF(55F, 47F), new PointF(117F, 47F),
                    new PointF(112F, 111F), new PointF(16F, 111F)
                };
                using (var shadow = new SolidBrush(
                    Color.FromArgb(95, 0, 0, 0)))
                    graphics.FillPolygon(shadow,
                        Offset(rear, 1F, 5F));
                using (var rearGold = new LinearGradientBrush(
                    new RectangleF(10F, 37F, 107F, 74F),
                    Color.FromArgb(229, 198, 111),
                    Color.FromArgb(121, 76, 22),
                    LinearGradientMode.Vertical))
                using (var darkEdge = new Pen(
                    Color.FromArgb(54, 37, 19), 2F))
                {
                    graphics.FillPolygon(rearGold, rear);
                    graphics.DrawPolygon(darkEdge, rear);
                }
                PointF[] face =
                {
                    new PointF(14F, 53F), new PointF(114F, 53F),
                    new PointF(106F, 111F), new PointF(22F, 111F)
                };
                using (var lacquer = new LinearGradientBrush(
                    new RectangleF(14F, 53F, 100F, 58F),
                    Color.FromArgb(49, 72, 68),
                    Color.FromArgb(10, 20, 22),
                    LinearGradientMode.Vertical))
                using (var ivory = new Pen(
                    Color.FromArgb(237, 225, 188), 1.5F))
                using (var goldLine = new Pen(
                    Color.FromArgb(220, 179, 75), 1F))
                {
                    graphics.FillPolygon(lacquer, face);
                    graphics.DrawPolygon(ivory, face);
                    graphics.DrawLine(goldLine, 25F, 102F, 103F, 102F);
                    graphics.DrawLine(goldLine, 29F, 98F, 99F, 98F);
                }
                DrawArtDecoFanBadge(graphics, kind);
            }
            return bitmap;
        }

        private static void DrawArtDecoFanBadge(
            Graphics graphics, DockFolderKind kind)
        {
            RectangleF badge = new RectangleF(43F, 66F, 42F, 34F);
            using (var gold = new LinearGradientBrush(badge,
                Color.FromArgb(241, 211, 125),
                Color.FromArgb(133, 84, 23),
                LinearGradientMode.Vertical))
            using (var edge = new Pen(
                Color.FromArgb(247, 232, 179), 1.2F))
            using (var dark = new SolidBrush(
                Color.FromArgb(20, 34, 34)))
            {
                graphics.FillPie(gold,
                    badge.X, badge.Y, badge.Width, badge.Height,
                    180F, 180F);
                graphics.DrawArc(edge, badge, 180F, 180F);
                float cx = 64F;
                float cy = 83F;
                for (int i = 0; i <= 4; i++)
                {
                    double angle = Math.PI + i * Math.PI / 4D;
                    graphics.DrawLine(edge, cx, cy,
                        cx + (float)Math.Cos(angle) * 18F,
                        cy + (float)Math.Sin(angle) * 15F);
                }
                graphics.FillEllipse(dark,
                    new RectangleF(59F, 78F, 10F, 10F));
            }
            using (var glyph = new Pen(
                Color.FromArgb(239, 228, 188), 2.2F))
            using (var glyphFill = new SolidBrush(
                Color.FromArgb(239, 228, 188)))
            {
                if (kind == DockFolderKind.Music)
                {
                    graphics.DrawLine(glyph, 61F, 70F, 61F, 82F);
                    graphics.DrawLine(glyph, 61F, 70F, 70F, 68F);
                    graphics.DrawLine(glyph, 70F, 68F, 70F, 79F);
                    graphics.FillEllipse(glyphFill,
                        new RectangleF(56F, 79F, 7F, 6F));
                    graphics.FillEllipse(glyphFill,
                        new RectangleF(65F, 76F, 7F, 6F));
                }
                else if (kind == DockFolderKind.Pictures)
                {
                    graphics.DrawRectangle(glyph, 55F, 70F, 18F, 13F);
                    graphics.DrawLine(glyph, 57F, 81F, 62F, 75F);
                    graphics.DrawLine(glyph, 62F, 75F, 67F, 80F);
                    graphics.DrawLine(glyph, 67F, 80F, 71F, 76F);
                }
                else if (kind == DockFolderKind.Videos)
                {
                    graphics.DrawRectangle(glyph, 55F, 70F, 18F, 13F);
                    graphics.FillPolygon(glyphFill, new[]
                    {
                        new PointF(62F, 73F), new PointF(62F, 81F),
                        new PointF(69F, 77F)
                    });
                }
                else if (kind == DockFolderKind.Downloads)
                {
                    graphics.DrawLine(glyph, 64F, 68F, 64F, 79F);
                    graphics.DrawLine(glyph, 59F, 75F, 64F, 81F);
                    graphics.DrawLine(glyph, 69F, 75F, 64F, 81F);
                }
                else if (kind == DockFolderKind.Desktop)
                {
                    graphics.DrawRectangle(glyph, 55F, 70F, 18F, 12F);
                    graphics.DrawLine(glyph, 64F, 82F, 64F, 86F);
                }
                else
                {
                    graphics.FillRectangle(glyphFill,
                        new RectangleF(58F, 68F, 13F, 17F));
                }
            }
        }

        private static Image CreateSteampunkFolderIcon(string path)
        {
            DockFolderKind kind = GetFolderKind(path);
            var bitmap = new Bitmap(128, 128);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                PointF[] rear =
                {
                    new PointF(9F, 36F),
                    new PointF(43F, 36F),
                    new PointF(55F, 46F),
                    new PointF(118F, 46F),
                    new PointF(114F, 109F),
                    new PointF(14F, 109F)
                };
                using (var shadow = new SolidBrush(
                    Color.FromArgb(100, 0, 0, 0)))
                    graphics.FillPolygon(shadow,
                        Offset(rear, 1F, 5F));
                using (var rearLeather = new LinearGradientBrush(
                    new RectangleF(9F, 36F, 109F, 75F),
                    Color.FromArgb(120, 65, 37),
                    Color.FromArgb(48, 25, 20),
                    LinearGradientMode.Vertical))
                using (var rearEdge = new Pen(
                    Color.FromArgb(203, 127, 56), 2F))
                {
                    graphics.FillPolygon(rearLeather, rear);
                    graphics.DrawPolygon(rearEdge, rear);
                }

                PointF[] flap =
                {
                    new PointF(12F, 51F),
                    new PointF(116F, 51F),
                    new PointF(108F, 112F),
                    new PointF(20F, 112F)
                };
                using (var flapLeather = new LinearGradientBrush(
                    new RectangleF(12F, 51F, 104F, 61F),
                    Color.FromArgb(142, 77, 42),
                    Color.FromArgb(65, 31, 24),
                    LinearGradientMode.Vertical))
                using (var stitching = new Pen(
                    Color.FromArgb(222, 173, 104), 1.5F))
                {
                    graphics.FillPolygon(flapLeather, flap);
                    graphics.DrawPolygon(stitching, flap);
                    stitching.DashStyle = DashStyle.Dash;
                    graphics.DrawLine(stitching,
                        24F, 105F, 104F, 105F);
                }

                using (var brass = new LinearGradientBrush(
                    new RectangleF(48F, 49F, 32F, 18F),
                    Color.FromArgb(244, 194, 88),
                    Color.FromArgb(109, 59, 25),
                    LinearGradientMode.Vertical))
                using (var brassEdge = new Pen(
                    Color.FromArgb(62, 29, 16), 1F))
                {
                    graphics.FillRectangle(brass,
                        new RectangleF(48F, 49F, 32F, 18F));
                    graphics.DrawRectangle(brassEdge,
                        48F, 49F, 32F, 18F);
                }
                using (var keyhole = new SolidBrush(
                    Color.FromArgb(53, 27, 18)))
                {
                    graphics.FillEllipse(keyhole,
                        new RectangleF(61F, 54F, 6F, 6F));
                    graphics.FillRectangle(keyhole,
                        new RectangleF(63F, 58F, 2F, 5F));
                }

                DrawBrassCorner(graphics, 18F, 99F, false);
                DrawBrassCorner(graphics, 110F, 99F, true);
                if (kind != DockFolderKind.Folder)
                    DrawSteampunkFolderBadge(graphics, kind);
            }
            return bitmap;
        }

        private static void DrawBrassCorner(
            Graphics graphics, float x, float y, bool right)
        {
            PointF[] corner = right
                ? new[]
                {
                    new PointF(x, y - 10F),
                    new PointF(x, y + 9F),
                    new PointF(x - 17F, y + 9F)
                }
                : new[]
                {
                    new PointF(x, y - 10F),
                    new PointF(x, y + 9F),
                    new PointF(x + 17F, y + 9F)
                };
            using (var fill = new SolidBrush(
                Color.FromArgb(184, 113, 45)))
                graphics.FillPolygon(fill, corner);
        }

        private static void DrawSteampunkFolderBadge(
            Graphics graphics, DockFolderKind kind)
        {
            RectangleF badge = new RectangleF(42F, 70F, 44F, 34F);
            using (var brass = new LinearGradientBrush(
                badge,
                Color.FromArgb(236, 184, 85),
                Color.FromArgb(99, 50, 23),
                LinearGradientMode.Vertical))
            using (var edge = new Pen(
                Color.FromArgb(48, 24, 16), 1.5F))
            {
                graphics.FillEllipse(brass, badge);
                graphics.DrawEllipse(edge, badge);
            }
            using (var glyph = new Pen(
                Color.FromArgb(247, 224, 168), 2.6F))
            using (var fill = new SolidBrush(
                Color.FromArgb(247, 224, 168)))
            using (var dark = new Pen(
                Color.FromArgb(69, 39, 25), 1.8F))
            {
                glyph.StartCap = LineCap.Round;
                glyph.EndCap = LineCap.Round;
                if (kind == DockFolderKind.Music)
                {
                    graphics.DrawLine(glyph, 59F, 78F, 59F, 94F);
                    graphics.DrawLine(glyph, 59F, 78F, 73F, 76F);
                    graphics.DrawLine(glyph, 73F, 76F, 73F, 91F);
                    graphics.FillEllipse(fill,
                        new RectangleF(52F, 91F, 9F, 7F));
                    graphics.FillEllipse(fill,
                        new RectangleF(66F, 88F, 9F, 7F));
                }
                else if (kind == DockFolderKind.Pictures)
                {
                    graphics.DrawRectangle(glyph, 54F, 78F, 21F, 16F);
                    graphics.FillEllipse(fill,
                        new RectangleF(68F, 80F, 4F, 4F));
                    graphics.DrawLine(glyph, 56F, 92F, 62F, 85F);
                    graphics.DrawLine(glyph, 62F, 85F, 67F, 90F);
                    graphics.DrawLine(glyph, 67F, 90F, 72F, 86F);
                }
                else if (kind == DockFolderKind.Videos)
                {
                    graphics.DrawRectangle(glyph, 54F, 78F, 21F, 16F);
                    graphics.FillPolygon(fill, new[]
                    {
                        new PointF(62F, 81F),
                        new PointF(62F, 91F),
                        new PointF(70F, 86F)
                    });
                }
                else if (kind == DockFolderKind.Downloads)
                {
                    graphics.DrawLine(glyph, 64F, 76F, 64F, 91F);
                    graphics.DrawLine(glyph, 57F, 85F, 64F, 93F);
                    graphics.DrawLine(glyph, 71F, 85F, 64F, 93F);
                    graphics.DrawLine(glyph, 56F, 97F, 72F, 97F);
                }
                else if (kind == DockFolderKind.Desktop)
                {
                    graphics.DrawRectangle(glyph, 54F, 78F, 21F, 14F);
                    graphics.DrawLine(glyph, 64F, 92F, 64F, 97F);
                    graphics.DrawLine(glyph, 58F, 97F, 70F, 97F);
                }
                else
                {
                    graphics.FillRectangle(fill,
                        new RectangleF(57F, 76F, 15F, 22F));
                    graphics.DrawLine(dark, 60F, 83F, 69F, 83F);
                    graphics.DrawLine(dark, 60F, 87F, 69F, 87F);
                    graphics.DrawLine(dark, 60F, 91F, 67F, 91F);
                }
            }
        }

        private static void DrawIconBolt(
            Graphics graphics, float x, float y)
        {
            using (var fill = new SolidBrush(
                Color.FromArgb(67, 35, 20)))
            using (var shine = new Pen(
                Color.FromArgb(236, 190, 102), 1F))
            {
                graphics.FillEllipse(fill,
                    new RectangleF(x - 3F, y - 3F, 6F, 6F));
                graphics.DrawLine(shine, x - 1.5F, y, x + 1.5F, y);
            }
        }

        private static void DrawSmallIconGear(
            Graphics graphics, float x, float y, float radius)
        {
            graphics.TranslateTransform(x, y);
            PointF[] teeth = new PointF[16];
            for (int i = 0; i < teeth.Length; i++)
            {
                double angle = i * Math.PI * 2D / teeth.Length;
                float toothRadius = i % 2 == 0
                    ? radius : radius * 0.72F;
                teeth[i] = new PointF(
                    (float)Math.Cos(angle) * toothRadius,
                    (float)Math.Sin(angle) * toothRadius);
            }
            using (var fill = new SolidBrush(
                Color.FromArgb(191, 112, 46)))
            using (var hole = new SolidBrush(
                Color.FromArgb(29, 27, 23)))
            {
                graphics.FillPolygon(fill, teeth);
                graphics.FillEllipse(hole,
                    -radius * 0.28F, -radius * 0.28F,
                    radius * 0.56F, radius * 0.56F);
            }
            graphics.ResetTransform();
        }

        private static PointF[] Offset(
            PointF[] points, float x, float y)
        {
            var result = new PointF[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = new PointF(
                    points[i].X + x, points[i].Y + y);
            return result;
        }

        private static void DrawFolderBadge(
            Graphics graphics, DockFolderKind kind)
        {
            RectangleF badge = new RectangleF(40F, 58F, 48F, 48F);
            using (var fill = new SolidBrush(
                Color.FromArgb(220, 35, 43, 47)))
            using (var edge = new Pen(
                Color.FromArgb(231, 198, 135), 1.5F))
            {
                graphics.FillEllipse(fill, badge);
                graphics.DrawEllipse(edge, badge);
            }
            using (var glyph = new Pen(
                Color.FromArgb(235, 237, 226), 3F))
            using (var accent = new SolidBrush(
                Color.FromArgb(77, 164, 207)))
            using (var light = new SolidBrush(
                Color.FromArgb(239, 235, 214)))
            {
                glyph.StartCap = LineCap.Round;
                glyph.EndCap = LineCap.Round;
                if (kind == DockFolderKind.Music)
                {
                    graphics.DrawLine(glyph, 60F, 69F, 60F, 92F);
                    graphics.DrawLine(glyph, 60F, 69F, 77F, 66F);
                    graphics.DrawLine(glyph, 77F, 66F, 77F, 87F);
                    graphics.FillEllipse(accent,
                        new RectangleF(51F, 87F, 12F, 9F));
                    graphics.FillEllipse(accent,
                        new RectangleF(68F, 82F, 12F, 9F));
                }
                else if (kind == DockFolderKind.Pictures)
                {
                    graphics.DrawRectangle(glyph, 51F, 69F, 27F, 23F);
                    graphics.FillEllipse(accent,
                        new RectangleF(68F, 72F, 6F, 6F));
                    graphics.FillPolygon(light, new[]
                    {
                        new PointF(53F, 89F),
                        new PointF(61F, 79F),
                        new PointF(67F, 85F),
                        new PointF(72F, 81F),
                        new PointF(77F, 89F)
                    });
                }
                else if (kind == DockFolderKind.Videos)
                {
                    graphics.DrawRectangle(glyph, 51F, 70F, 27F, 22F);
                    graphics.FillPolygon(accent, new[]
                    {
                        new PointF(61F, 75F),
                        new PointF(61F, 88F),
                        new PointF(72F, 81.5F)
                    });
                }
                else if (kind == DockFolderKind.Downloads)
                {
                    graphics.DrawLine(glyph, 64F, 67F, 64F, 87F);
                    graphics.DrawLine(glyph, 55F, 79F, 64F, 89F);
                    graphics.DrawLine(glyph, 73F, 79F, 64F, 89F);
                    graphics.DrawLine(glyph, 53F, 95F, 75F, 95F);
                }
                else if (kind == DockFolderKind.Desktop)
                {
                    graphics.DrawRectangle(glyph, 50F, 69F, 29F, 20F);
                    graphics.DrawLine(glyph, 64F, 89F, 64F, 96F);
                    graphics.DrawLine(glyph, 56F, 96F, 72F, 96F);
                }
                else
                {
                    graphics.FillRectangle(light,
                        new RectangleF(54F, 67F, 22F, 29F));
                    using (var line = new Pen(
                        Color.FromArgb(71, 92, 103), 2F))
                    {
                        graphics.DrawLine(line, 58F, 76F, 72F, 76F);
                        graphics.DrawLine(line, 58F, 82F, 72F, 82F);
                        graphics.DrawLine(line, 58F, 88F, 69F, 88F);
                    }
                }
            }
        }

        private static DockFolderKind GetFolderKind(string path)
        {
            if (SamePath(path, Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory)))
                return DockFolderKind.Desktop;
            if (SamePath(path, Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments)))
                return DockFolderKind.Documents;
            if (SamePath(path, Environment.GetFolderPath(
                    Environment.SpecialFolder.MyPictures)))
                return DockFolderKind.Pictures;
            if (SamePath(path, Environment.GetFolderPath(
                    Environment.SpecialFolder.MyMusic)))
                return DockFolderKind.Music;
            if (SamePath(path, Environment.GetFolderPath(
                    Environment.SpecialFolder.MyVideos)))
                return DockFolderKind.Videos;
            string downloads = Path.Combine(
                Environment.ExpandEnvironmentVariables("%USERPROFILE%"),
                "Downloads");
            return SamePath(path, downloads)
                ? DockFolderKind.Downloads
                : DockFolderKind.Folder;
        }

        private static bool SamePath(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) ||
                string.IsNullOrWhiteSpace(second))
                return false;
            try
            {
                string a = Path.GetFullPath(first).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string b = Path.GetFullPath(second).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                return string.Equals(a, b,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static Image LoadCustom(string path)
        {
            try
            {
                if (string.Equals(Path.GetExtension(path), ".ico",
                    StringComparison.OrdinalIgnoreCase))
                {
                    using (var icon = new Icon(
                        path, new Size(256, 256)))
                        return icon.ToBitmap();
                }
                string extension = Path.GetExtension(path);
                if (string.Equals(extension, ".exe",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".dll",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Image highResolution =
                        LoadHighResolutionIcon(path);
                    if (highResolution != null)
                        return highResolution;
                    Icon icon = Icon.ExtractAssociatedIcon(path);
                    if (icon == null) return null;
                    using (icon) return icon.ToBitmap();
                }
                using (Image source = Image.FromFile(path))
                    return new Bitmap(source);
            }
            catch { return null; }
        }
    }

    internal sealed class EmilyDeskDockSettingsForm : Form
    {
        private readonly EmilyDeskDockSettings _settings;
        private readonly ComboBox _theme;
        private readonly ComboBox _edge;
        private readonly NumericUpDown _iconSize;
        private readonly CheckBox _autoHide;
        private readonly CheckBox _alwaysOnTop;
        private readonly CheckBox _startWithEmilyDesk;
        private readonly Button _tint;
        private readonly Button _resetTint;
        private readonly TrackBar _lightShade;
        private int _tintArgb;

        public EmilyDeskDockSettingsForm(EmilyDeskDockSettings settings)
        {
            _settings = settings;
            Text = "EmilyDesk Dock Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(500, 566);
            Font = new Font("Segoe UI", 9F);
            BackColor = EmilyDeskDesignTokens.Canvas;

            Controls.Add(new Label
            {
                Text = "EmilyDesk Dock",
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(28, 24)
            });
            Controls.Add(new Label
            {
                Text = "Choose a dock theme while keeping the same items, shortcuts, and behaviour.",
                ForeColor = EmilyDeskDesignTokens.Muted,
                AutoSize = false,
                Location = new Point(30, 64),
                Size = new Size(430, 42)
            });

            AddCaption("Theme", 30, 124);
            _theme = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(190, 120),
                Width = 220
            };
            foreach (string themeName in EmilyDeskThemeCatalog.Names)
                _theme.Items.Add(themeName);
            _theme.SelectedItem = EmilyDeskDockStore.NormalizeTheme(
                _settings.Theme);
            Controls.Add(_theme);

            AddCaption("Screen edge", 30, 170);
            _edge = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(190, 166),
                Width = 220
            };
            _edge.Items.AddRange(new object[]
            {
                EmilyDeskDockEdge.Bottom,
                EmilyDeskDockEdge.Top,
                EmilyDeskDockEdge.Left,
                EmilyDeskDockEdge.Right
            });
            EmilyDeskDockEdge selectedEdge;
            if (!Enum.TryParse(_settings.Edge, true, out selectedEdge))
                selectedEdge = EmilyDeskDockEdge.Bottom;
            _edge.SelectedItem = selectedEdge;
            Controls.Add(_edge);

            AddCaption("Icon size", 30, 216);
            _iconSize = new NumericUpDown
            {
                Minimum = 36,
                Maximum = 96,
                Increment = 4,
                Value = Math.Max(36, Math.Min(96, _settings.IconSize)),
                Location = new Point(190, 212),
                Width = 100
            };
            Controls.Add(_iconSize);

            AddCaption("Dock colour", 30, 258);
            _tintArgb = _settings.TintArgb;
            _tint = new Button
            {
                Text = "Choose colour...",
                Location = new Point(190, 252),
                Size = new Size(132, 31),
                FlatStyle = FlatStyle.Flat
            };
            UpdateTintButton();
            _tint.Click += delegate
            {
                using (var picker = new ColorDialog
                {
                    FullOpen = true,
                    Color = _tintArgb == 0
                        ? Color.FromArgb(115, 72, 45)
                        : Color.FromArgb(_tintArgb)
                })
                {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    _tintArgb = picker.Color.ToArgb();
                    UpdateTintButton();
                }
            };
            Controls.Add(_tint);
            _resetTint = new Button
            {
                Text = "Use theme colour",
                Location = new Point(328, 252),
                Size = new Size(132, 31),
                FlatStyle = FlatStyle.System
            };
            _resetTint.Click += delegate
            {
                _tintArgb = 0;
                UpdateTintButton();
            };
            Controls.Add(_resetTint);
            UpdateTintButton();

            AddCaption("Light / shade", 30, 304);
            _lightShade = new TrackBar
            {
                Minimum = -60,
                Maximum = 60,
                TickFrequency = 10,
                SmallChange = 2,
                LargeChange = 10,
                Value = Math.Max(-60, Math.Min(60,
                    _settings.LightShade)),
                Location = new Point(184, 292),
                Size = new Size(276, 45)
            };
            Controls.Add(_lightShade);
            Controls.Add(new Label
            {
                Text = "Darker                         Lighter",
                ForeColor = EmilyDeskDesignTokens.Muted,
                Location = new Point(190, 334),
                Size = new Size(270, 20)
            });

            _alwaysOnTop = CheckBox("Keep dock above other windows",
                30, 366, _settings.AlwaysOnTop);
            _autoHide = CheckBox("Automatically hide at the screen edge",
                30, 400, _settings.AutoHide);
            _startWithEmilyDesk = CheckBox(
                "Start this dock with EmilyDesk",
                30, 434, _settings.StartWithEmilyDesk);
            Controls.Add(_alwaysOnTop);
            Controls.Add(_autoHide);
            Controls.Add(_startWithEmilyDesk);

            var defaults = new Button
            {
                Text = "Restore Default Items",
                Location = new Point(30, 488),
                Size = new Size(165, 34),
                FlatStyle = FlatStyle.System
            };
            defaults.Click += delegate
            {
                if (MessageBox.Show(this,
                    "Replace the current dock items with the EmilyDesk defaults?",
                    "EmilyDesk Dock",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                _settings.Items = EmilyDeskDockSettings
                    .CreateDefaults().Items;
            };
            Controls.Add(defaults);

            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(280, 488),
                Size = new Size(88, 34)
            };
            Controls.Add(cancel);
            var save = new Button
            {
                Text = "Save",
                BackColor = EmilyDeskDesignTokens.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(380, 488),
                Size = new Size(88, 34)
            };
            save.FlatAppearance.BorderSize = 0;
            save.Click += delegate
            {
                try
                {
                    _settings.Theme = EmilyDeskDockStore.NormalizeTheme(
                        Convert.ToString(_theme.SelectedItem));
                    _settings.Edge = Convert.ToString(
                        _edge.SelectedItem);
                    _settings.IconSize = Decimal.ToInt32(
                        _iconSize.Value);
                    _settings.TintArgb = _tintArgb;
                    _settings.LightShade = _lightShade.Value;
                    _settings.AlwaysOnTop = _alwaysOnTop.Checked;
                    _settings.AutoHide = _autoHide.Checked;
                    _settings.StartWithEmilyDesk =
                        _startWithEmilyDesk.Checked;
                    _settings.StartupPreferenceInitialized = true;
                    EmilyDeskDockStore.Save(_settings);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "The dock settings could not be saved.\r\n\r\n" +
                        ex.Message,
                        "EmilyDesk Dock",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void UpdateTintButton()
        {
            Color colour = _tintArgb == 0
                ? EmilyDeskDesignTokens.Surface
                : Color.FromArgb(_tintArgb);
            _tint.BackColor = colour;
            _tint.ForeColor = colour.GetBrightness() < .52F
                ? Color.White : Color.Black;
            _tint.Text = _tintArgb == 0
                ? "Choose colour..." : "Change colour...";
            if (_resetTint != null)
                _resetTint.Enabled = _tintArgb != 0;
        }

        private void AddCaption(string text, int left, int top)
        {
            Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(left, top + 4)
            });
        }

        private static CheckBox CheckBox(
            string text, int left, int top, bool value)
        {
            return new CheckBox
            {
                Text = text,
                AutoSize = true,
                Checked = value,
                Location = new Point(left, top)
            };
        }
    }

    internal static class Prompt
    {
        public static string Show(
            IWin32Window owner,
            string title,
            string message,
            string value)
        {
            using (var form = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(430, 145),
                Font = new Font("Segoe UI", 9F)
            })
            {
                form.Controls.Add(new Label
                {
                    Text = message,
                    AutoSize = false,
                    Location = new Point(18, 18),
                    Size = new Size(390, 28)
                });
                var text = new TextBox
                {
                    Text = value ?? string.Empty,
                    Location = new Point(18, 51),
                    Width = 390
                };
                form.Controls.Add(text);
                var cancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(238, 92),
                    Size = new Size(80, 30)
                };
                var okay = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(328, 92),
                    Size = new Size(80, 30)
                };
                form.Controls.Add(cancel);
                form.Controls.Add(okay);
                form.AcceptButton = okay;
                form.CancelButton = cancel;
                form.Shown += delegate
                {
                    text.SelectAll();
                    text.Focus();
                };
                return form.ShowDialog(owner) == DialogResult.OK
                    ? text.Text : string.Empty;
            }
        }
    }
}
