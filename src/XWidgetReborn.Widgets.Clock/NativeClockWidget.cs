using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Widgets.Clock
{
    public sealed partial class NativeClockWidget :
        IWidget,
        IRuntimeAwareWidget,
        IOfficialWidget,
        IWidgetThemeProvider,
        IWidgetStyleProvider,
        IWidgetSnapBoundsProvider
    {
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
            if (e == null || e.Topic != "designer.saved") return;
            ReloadDesignerLayoutForCurrentTheme();
            RequestInvalidate();
        }
        private DateTime _displayTime = DateTime.Now;
        private DateTime _lastRenderedTime = DateTime.MinValue;
        private bool _paused;
        private string _mode;
        private bool _showSeconds;
        private bool _showDate;
        private bool _use24Hour;
        private bool _showAmPm;
        private bool _smoothSeconds;
        private string _animationLevel;
        private string _appearance;
        private string _style;
        private static readonly IClockStyleRenderer
            ModernRenderer =
                new ModernClockStyleRenderer();
        private static readonly IClockStyleRenderer
            SteampunkRenderer =
                new SteampunkClockStyleRenderer();
        private static readonly IClockStyleRenderer
            ArtDecoRenderer =
                new ArtDecoClockStyleRenderer();
        private static readonly IClockStyleRenderer
            IndustrialRenderer =
                new IndustrialClockStyleRenderer();
        private const string IndustrialSkinPath =
            @"Assets\Themes\Industrial\industrial-clock-skin.png";
        private const string IndustrialHourHandPath =
            @"Assets\Themes\Industrial\industrial-hour-hand.png";
        private const string IndustrialMinuteHandPath =
            @"Assets\Themes\Industrial\industrial-minute-hand.png";
        private const string IndustrialSecondHandPath =
            @"Assets\Themes\Industrial\industrial-second-hand.png";
        private const string ArtDecoSkinPath =
            @"Assets\Themes\ArtDeco\art-deco-clock-skin.png";
        private const string WoodlandSkinPath =
            @"Assets\Themes\WoodlandNature\woodland-clock-skin.png";
        private const string WoodlandHourHandPath =
            @"Assets\Themes\WoodlandNature\woodland-hour-hand.png";
        private const string WoodlandMinuteHandPath =
            @"Assets\Themes\WoodlandNature\woodland-minute-hand.png";
        private const string WoodlandSecondHandPath =
            @"Assets\Themes\WoodlandNature\woodland-second-hand.png";
        private const string BotanicalSkinPath =
            @"Assets\Themes\BotanicalNature\botanical-clock-skin.png";
        private const string BotanicalHourHandPath =
            @"Assets\Themes\BotanicalNature\botanical-hour-hand.png";
        private const string BotanicalMinuteHandPath =
            @"Assets\Themes\BotanicalNature\botanical-minute-hand.png";
        private const string BotanicalSecondHandPath =
            @"Assets\Themes\BotanicalNature\botanical-second-hand.png";
        private const string SteampunkSkinPath =
            @"Assets\Themes\Steampunk\steampunk-clock-skin.png";
        private static readonly object CustomClockImageSync = new object();
        private static readonly Dictionary<string, Image> CustomClockImages =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> CustomClockImageTimes =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public NativeClockWidget()
        {
            SetDefaults();
        }

        public string Id { get { return "native.clock"; } }
        public string Name { get { return "Clock"; } }
        public Size DefaultSize { get { return new Size(300, 300); } }
        public Point DefaultLocation { get { return new Point(80, 80); } }
        public string Description
        {
            get
            {
                return "Official dual-mode Analog and Digital clock for EmilyDesk.";
            }
        }
        public string Version { get { return "2.0.0"; } }
        public WidgetUpdateRate UpdateRate
        {
            get { return WidgetUpdateRate.QuarterSecond; }
        }
        public IEnumerable<string> Themes
        {
            get { return EmilyDeskThemeCatalog.Names; }
        }
        public string Theme
        {
            get { return _appearance; }
            set
            {
                string normalized =
                    EmilyDeskThemeCatalog.Normalize(value);
                if (_appearance == normalized)
                {
                    SaveSettings();
                    return;
                }
                _appearance = normalized;
                _style = normalized == "Steampunk"
                    ? "Clock.Steampunk"
                    : "Clock.Modern";
                ReloadDesignerLayoutForCurrentTheme();
                SaveSettings();
                ActivateMode();
            }
        }
        public IEnumerable<WidgetStyleMetadata> Styles
        {
            get
            {
                return new[]
                {
                    new WidgetStyleMetadata(
                        "Clock.Modern",
                        "Modern",
                        "The stable EmilyDesk Clock design.",
                        "Clock",
                        new Size(300, 300),
                        new Size(150, 150),
                        "preview.png",
                        new[]
                        {
                            "Midnight",
                            "Light",
                            "Ocean"
                        }),
                    new WidgetStyleMetadata(
                        "Clock.Steampunk",
                        "Steampunk",
                        "A brass-framed mechanical clock with vintage details.",
                        "Clock",
                        new Size(300, 300),
                        new Size(150, 150),
                        "preview-steampunk.png",
                        new[]
                        {
                            "Brass",
                            "Copper",
                            "Aged Steel"
                        })
                };
            }
        }
        public string Style
        {
            get { return _style; }
            set { ApplyStyle(value); }
        }
        public void ManageStyles()
        {
            ShowSettings();
        }

        private bool IsSteampunk
        {
            get { return _style == "Clock.Steampunk"; }
        }

        private bool IsArtDeco
        {
            get { return _appearance == "Art Deco"; }
        }

        private bool IsVintage
        {
            get { return _appearance == "Vintage"; }
        }

        private bool IsModern
        {
            get { return _appearance == "Modern"; }
        }

        private bool IsIndustrial
        {
            get { return _appearance == "Industrial"; }
        }

        private bool IsWoodland
        {
            get { return _appearance == "Woodland Nature"; }
        }

        private bool IsBotanical
        {
            get { return _appearance == "Botanical Nature"; }
        }

        private bool IsImportedTheme
        {
            get { return EmilyDeskThemeCatalog.Get(_appearance).IsImported; }
        }

        private bool UsesWoodlandNaturePresentation
        {
            get { return IsWoodland || IsBotanical; }
        }

        private bool UsesIndustrialClockPresentation
        {
            get { return IsImportedTheme || IsIndustrial || IsSteampunk ||
                UsesWoodlandNaturePresentation; }
        }

        private string IndustrialPresentationSkinPath
        {
            get
            {
                string imported = EmilyDeskThemeCatalog.Get(_appearance).Asset("clock");
                return IsImportedTheme && !string.IsNullOrEmpty(imported) ? imported
                : IsWoodland ? WoodlandSkinPath
                : IsBotanical ? BotanicalSkinPath
                : IsSteampunk ? SteampunkSkinPath : IndustrialSkinPath;
            }
        }

        public void AttachHost(IWidgetHostContext host)
        {
            _host = host;
            LoadSettings();
            ReloadDesignerLayoutForCurrentTheme();
            ActivateMode();
        }

        private void ReloadDesignerLayoutForCurrentTheme()
        {
            IndustrialClockDesignerLayout.Select(
                DesignerLayoutFiles.FileName(_appearance, "clock"));
        }

        public void Start(Action invalidate)
        {
            ReloadDesignerLayoutForCurrentTheme();
            _invalidate = invalidate;
            _displayTime = DateTime.Now;
            _lastRenderedTime = DateTime.MinValue;
            ActivateMode();
            RequestInvalidate();
        }

        public void Tick(DateTime now)
        {
            if (_paused) return;
            _displayTime = now;
            bool needsFrame =
                _mode == "Analog" &&
                _showSeconds &&
                _smoothSeconds;
            bool changed = needsFrame ||
                (_showSeconds
                    ? now.Second != _lastRenderedTime.Second
                    : now.Minute != _lastRenderedTime.Minute);
            if (changed && _invalidate != null)
                _invalidate();
        }

        public void Pause()
        {
            _paused = true;
            RequestInvalidate();
        }

        public void Resume()
        {
            ReloadDesignerLayoutForCurrentTheme();
            _paused = false;
            _displayTime = DateTime.Now;
            RequestInvalidate();
        }

        public void Render(Graphics graphics, Rectangle bounds)
        {
            _lastRenderedTime = _displayTime;
            GraphicsState state = graphics.Save();
            try
            {
                Size baseSize = PreferredSize();
                graphics.ScaleTransform(
                    (float)bounds.Width / baseSize.Width,
                    (float)bounds.Height / baseSize.Height);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint
                        .ClearTypeGridFit;
                Rectangle logicalBounds =
                    new Rectangle(
                        0,
                        0,
                        baseSize.Width - 1,
                        baseSize.Height - 1);
                try
                {
                    if (RenderEditableClock(graphics, baseSize)) return;
                    ActiveRenderer.Render(
                        this,
                        graphics,
                        logicalBounds);
                    DrawSavedClockLayers(graphics, baseSize);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(
                        "Clock style '{0}' failed: {1}",
                        _style,
                        ex);
                    if (_host != null)
                        _host.ReportDiagnostic(
                            "Style '" + _style +
                            "' failed and was replaced by Modern: " +
                            ex.Message);
                    FallBackToModern();
                    ModernRenderer.Render(
                        this,
                        graphics,
                        logicalBounds);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        public IEnumerable<WidgetMenuCommand>
            GetMenuCommands()
        {
            return new[]
            {
                new WidgetMenuCommand(
                    "Analog mode",
                    delegate { return _mode == "Analog"; },
                    delegate { SetMode("Analog"); }),
                new WidgetMenuCommand(
                    "Digital mode",
                    delegate { return _mode == "Digital"; },
                    delegate { SetMode("Digital"); }),
                new WidgetMenuCommand(
                    "Edit in Designer",
                    false,
                    delegate { OpenDesigner("clock"); })
            };
        }

        public void ShowSettings()
        {
            using (var form = new Form())
            {
                form.Text = "Official Clock Settings";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(430, 523);
                form.Font = new Font("Segoe UI", 9F);

                var style = AddCombo(
                    form,
                    "Style",
                    18,
                    new[] { "Modern", "Steampunk" },
                    IsSteampunk
                        ? "Steampunk"
                        : "Modern");
                var styleDescription = new Label
                {
                    Text = IsSteampunk
                        ? "Mechanical brass frame and vintage dial."
                        : "Stable EmilyDesk Clock renderer.",
                    ForeColor = Color.DimGray,
                    Font = new Font("Segoe UI", 7.5F),
                    Location = new Point(174, 47),
                    Size = new Size(228, 12)
                };
                form.Controls.Add(styleDescription);
                var mode = AddCombo(
                    form,
                    "Mode",
                    60,
                    new[] { "Analog", "Digital" },
                    _mode);
                var seconds = AddCheck(
                    form,
                    "Show seconds",
                    112,
                    _showSeconds);
                var date = AddCheck(
                    form,
                    "Show date",
                    144,
                    _showDate);
                var format = AddCombo(
                    form,
                    "Time format",
                    180,
                    new[] { "12-hour", "24-hour" },
                    _use24Hour ? "24-hour" : "12-hour");
                var ampm = AddCheck(
                    form,
                    "Show AM/PM in 12-hour mode",
                    232,
                    _showAmPm);
                var smooth = AddCheck(
                    form,
                    "Smooth analog second movement",
                    264,
                    _smoothSeconds);
                var animation = AddCombo(
                    form,
                    "Animation",
                    300,
                    new[] { "Off", "Minimal", "Enhanced" },
                    _animationLevel);
                var appearance = AddCombo(
                    form,
                    "Theme",
                    336,
                    new List<string>(EmilyDeskThemeCatalog.Names).ToArray(),
                    _appearance);
                style.SelectedIndexChanged += delegate
                {
                    bool steampunk =
                        style.SelectedItem.ToString() ==
                        "Steampunk";
                    styleDescription.Text = steampunk
                        ? "Mechanical brass frame and vintage dial."
                        : "Stable EmilyDesk Clock renderer.";
                    appearance.SelectedItem = steampunk
                        ? "Steampunk"
                        : "Modern";
                };
                NumericUpDown scale = AddNumber(
                    form,
                    "Scale",
                    388,
                    50,
                    200,
                    (decimal)Math.Round(
                        (_host == null ? 1F : _host.Scale) * 100));
                NumericUpDown opacity = AddNumber(
                    form,
                    "Opacity",
                    424,
                    20,
                    100,
                    (decimal)Math.Round(
                        (_host == null ? 1D : _host.Opacity) * 100));

                var ok = new Button
                {
                    Text = "Apply",
                    DialogResult = DialogResult.OK,
                    Location = new Point(246, 484),
                    Size = new Size(78, 30)
                };
                var cancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(334, 484),
                    Size = new Size(78, 30)
                };
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                if (form.ShowDialog() != DialogResult.OK)
                    return;
                _appearance = EmilyDeskThemeCatalog.Normalize(
                    appearance.SelectedItem.ToString());
                _style = _appearance == "Steampunk"
                    ? "Clock.Steampunk"
                    : "Clock.Modern";
                _mode = mode.SelectedItem.ToString();
                _showSeconds = seconds.Checked;
                _showDate = date.Checked;
                _use24Hour =
                    format.SelectedItem.ToString() == "24-hour";
                _showAmPm = ampm.Checked;
                _smoothSeconds = smooth.Checked &&
                    animation.SelectedItem.ToString() == "Enhanced";
                _animationLevel = NormalizeAnimationLevel(
                    animation.SelectedItem.ToString());
                if (_host != null)
                {
                    _host.Scale = (float)scale.Value / 100F;
                    _host.Opacity =
                        (double)opacity.Value / 100D;
                }
                SaveSettings();
                ActivateMode();
            }
        }

        public void ResetSettings()
        {
            SetDefaults();
            if (_host != null)
            {
                _host.Scale = 1F;
                _host.Opacity = 1D;
            }
            SaveSettings();
            ActivateMode();
        }

        public void Dispose()
        {
            AttachRuntime(null);
            _invalidate = null;
            _host = null;
        }

        private void RenderAnalog(
            Graphics graphics,
            Rectangle bounds)
        {
            ClockPalette palette = Palette();
            using (var background = new LinearGradientBrush(
                bounds,
                palette.SurfaceTop,
                palette.SurfaceBottom,
                90F))
            {
                Rectangle face = new Rectangle(
                    bounds.Left + 5,
                    bounds.Top + 5,
                    bounds.Width - 10,
                    bounds.Height - 10);
                graphics.FillEllipse(background, face);
            }

            const float centerX = 150F;
            const float centerY = 140F;
            const float radius = 110F;
            using (var markerPen = new Pen(
                palette.SecondaryText,
                2F))
            {
                for (int hour = 0; hour < 12; hour++)
                {
                    double angle =
                        Math.PI * 2 * hour / 12D -
                        Math.PI / 2;
                    float inner = hour % 3 == 0
                        ? radius - 15
                        : radius - 9;
                    graphics.DrawLine(
                        markerPen,
                        centerX +
                            (float)Math.Cos(angle) * inner,
                        centerY +
                            (float)Math.Sin(angle) * inner,
                        centerX +
                            (float)Math.Cos(angle) * radius,
                        centerY +
                            (float)Math.Sin(angle) * radius);
                }
            }

            if (_showDate)
            {
                using (var dateFont = new Font(
                    "Segoe UI",
                    10F))
                using (var brush = new SolidBrush(
                    palette.SecondaryText))
                    DrawCentered(
                        graphics,
                        _displayTime.ToString(
                            "dddd, MMMM d",
                            CultureInfo.CurrentCulture),
                        dateFont,
                        brush,
                        new RectangleF(12, 260, 276, 25));
            }

            double seconds = _displayTime.Second +
                (_smoothSeconds
                    ? _displayTime.Millisecond / 1000D
                    : 0D);
            double minutes = _displayTime.Minute +
                seconds / 60D;
            double hours = (_displayTime.Hour % 12) +
                minutes / 60D;
            using (var hourPen = new Pen(
                palette.PrimaryText,
                7F))
            using (var minutePen = new Pen(
                palette.PrimaryText,
                4F))
            using (var secondPen = new Pen(
                palette.Accent,
                2F))
            using (var centerBrush = new SolidBrush(
                palette.Accent))
            {
                hourPen.StartCap = LineCap.Round;
                hourPen.EndCap = LineCap.Round;
                minutePen.StartCap = LineCap.Round;
                minutePen.EndCap = LineCap.Round;
                DrawHand(
                    graphics,
                    hourPen,
                    centerX,
                    centerY,
                    58F,
                    hours / 12D);
                DrawHand(
                    graphics,
                    minutePen,
                    centerX,
                    centerY,
                    82F,
                    minutes / 60D);
                if (_showSeconds)
                    DrawHand(
                        graphics,
                        secondPen,
                        centerX,
                        centerY,
                        91F,
                        seconds / 60D);
                graphics.FillEllipse(
                    centerBrush,
                    centerX - 5,
                    centerY - 5,
                    10,
                    10);
            }
            using (var border = new Pen(
                palette.Border,
                2F))
            {
                Rectangle face = new Rectangle(
                    bounds.Left + 5,
                    bounds.Top + 5,
                    bounds.Width - 10,
                    bounds.Height - 10);
                graphics.DrawEllipse(border, face);
            }
        }

        private void OpenDesigner(string widget)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "EmilyDesk.Designer.exe");
                if (!System.IO.File.Exists(path)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--widget " + widget + " --theme \"" +
                        _appearance + "\"",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void RenderDigital(
            Graphics graphics,
            Rectangle bounds)
        {
            ClockPalette palette = Palette();
            using (GraphicsPath path =
                RoundedRectangle(bounds, 16))
            using (var background = new LinearGradientBrush(
                bounds,
                palette.SurfaceTop,
                palette.SurfaceBottom,
                90F))
            using (var border = new Pen(palette.Border))
            using (var accent = new SolidBrush(palette.Accent))
            using (var main = new SolidBrush(
                palette.PrimaryText))
            using (var secondary = new SolidBrush(
                palette.SecondaryText))
            using (var timeFont = new Font(
                "Segoe UI",
                34F,
                FontStyle.Regular))
            using (var dateFont = new Font(
                "Segoe UI",
                11F))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
                graphics.FillRectangle(
                    accent,
                    0,
                    0,
                    6,
                    bounds.Height);
                string format;
                if (_use24Hour)
                    format = _showSeconds
                        ? "HH:mm:ss"
                        : "HH:mm";
                else
                    format = _showSeconds
                        ? (_showAmPm
                            ? "h:mm:ss tt"
                            : "h:mm:ss")
                        : (_showAmPm
                            ? "h:mm tt"
                            : "h:mm");
                DrawCentered(
                    graphics,
                    _displayTime.ToString(
                        format,
                        CultureInfo.CurrentCulture),
                    timeFont,
                    main,
                    new RectangleF(16, 38, 308, 72));
                if (_showDate)
                    DrawCentered(
                        graphics,
                        _displayTime.ToString(
                            "dddd, MMMM d",
                            CultureInfo.CurrentCulture),
                        dateFont,
                        secondary,
                        new RectangleF(20, 124, 300, 30));
                if (_paused)
                    DrawCentered(
                        graphics,
                        "Paused",
                        dateFont,
                        secondary,
                        new RectangleF(20, 160, 300, 24));
            }
        }

        private void SetMode(string mode)
        {
            _mode = mode;
            SaveSettings();
            ActivateMode();
        }

        private void SetDefaults()
        {
            _style = "Clock.Modern";
            _mode = "Analog";
            _showSeconds = true;
            _showDate = true;
            _use24Hour = false;
            _showAmPm = true;
            _smoothSeconds = false;
            _animationLevel = "Minimal";
            _appearance = "Modern";
        }

        private void LoadSettings()
        {
            if (_host == null) return;
            _style = NormalizeStyle(
                _host.GetSetting(
                    "style",
                    _style));
            _mode = NormalizeMode(
                _host.GetSetting("mode", _mode));
            _showSeconds = GetBool(
                "showSeconds",
                _showSeconds);
            _showDate = GetBool(
                "showDate",
                _showDate);
            _use24Hour = GetBool(
                "use24Hour",
                _use24Hour);
            _showAmPm = GetBool(
                "showAmPm",
                _showAmPm);
            _smoothSeconds = GetBool(
                "smoothSeconds",
                _smoothSeconds);
            _animationLevel = NormalizeAnimationLevel(
                _host.GetSetting(
                    "animationLevel",
                    _animationLevel));
            if (_animationLevel != "Enhanced")
                _smoothSeconds = false;
            _appearance = NormalizeAppearance(
                _host.GetSetting(
                    "appearance",
                    _appearance),
                IsSteampunk);
            _style = _appearance == "Steampunk"
                ? "Clock.Steampunk"
                : "Clock.Modern";
        }

        private void SaveSettings()
        {
            if (_host == null) return;
            _host.SetSetting("style", _style);
            _host.SetSetting("mode", _mode);
            _host.SetSetting(
                "showSeconds",
                _showSeconds.ToString());
            _host.SetSetting(
                "showDate",
                _showDate.ToString());
            _host.SetSetting(
                "use24Hour",
                _use24Hour.ToString());
            _host.SetSetting(
                "showAmPm",
                _showAmPm.ToString());
            _host.SetSetting(
                "smoothSeconds",
                _smoothSeconds.ToString());
            _host.SetSetting(
                "animationLevel",
                _animationLevel);
            _host.SetSetting(
                "appearance",
                _appearance);
        }

        private bool GetBool(string key, bool fallback)
        {
            bool value;
            return bool.TryParse(
                _host.GetSetting(
                    key,
                    fallback.ToString()),
                out value)
                ? value
                : fallback;
        }

        private void ActivateMode()
        {
            _displayTime = DateTime.Now;
            if (_host != null)
            {
                _host.SetPreferredSize(PreferredSize());
                _host.SetWindowShape(
                    IsModern || IsVintage || IsArtDeco ||
                        UsesIndustrialClockPresentation
                        ? WidgetWindowShape.AlphaRectangle
                        : _mode == "Analog"
                        ? WidgetWindowShape.Ellipse
                        : WidgetWindowShape.RoundedRectangle);
            }
            RequestInvalidate();
        }

        private Size PreferredSize()
        {
            if (IsArtDeco || UsesIndustrialClockPresentation) return new Size(360, 360);
            return _mode == "Digital"
                ? new Size(340, 200)
                : new Size(300, 300);
        }

        public Rectangle GetSnapBounds(Size renderedSurface)
        {
            SavedDesignerLayout saved = SavedDesignerLayout.Current(
                _appearance, "clock");
            DesignerLayer editable = saved == null || saved.Elements == null
                ? null : saved.Elements.Find(delegate(DesignerLayer item)
                {
                    return item != null && item.Visible &&
                        string.Equals(item.Id, "main-background",
                            StringComparison.OrdinalIgnoreCase);
                });
            Size logical = PreferredSize();
            if (editable != null)
                return Rectangle.Round(new RectangleF(
                    editable.Bounds.X * renderedSurface.Width /
                        logical.Width,
                    editable.Bounds.Y * renderedSurface.Height /
                        logical.Height,
                    editable.Bounds.Width * renderedSurface.Width /
                        logical.Width,
                    editable.Bounds.Height * renderedSurface.Height /
                        logical.Height));
            if (!IsArtDeco && !UsesIndustrialClockPresentation)
                return Rectangle.Empty;
            string path = IsArtDeco ? ArtDecoSkinPath :
                IndustrialPresentationSkinPath;
            return Rectangle.Round(DesignerLayerPainter.AlphaMappedBounds(
                path, new RectangleF(0F, 0F, renderedSurface.Width,
                    renderedSurface.Height)));
        }

        private IClockStyleRenderer ActiveRenderer
        {
            get
            {
                return UsesIndustrialClockPresentation
                    ? IndustrialRenderer
                    : IsArtDeco
                    ? ArtDecoRenderer
                    : IsSteampunk
                    ? SteampunkRenderer
                    : ModernRenderer;
            }
        }

        private void RenderArtDeco(Graphics graphics, Rectangle bounds)
        {
            Image skin = ThemeSkinCache.Get(
                ArtDecoSkinPath);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(skin, new Rectangle(0, 0, 360, 360));
            if (_editableReferenceOnly) return;

            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get("Art Deco");
            if (_mode == "Digital")
            {
                string format = _use24Hour
                    ? (_showSeconds ? "HH:mm:ss" : "HH:mm")
                    : (_showSeconds
                        ? (_showAmPm ? "h:mm:ss tt" : "h:mm:ss")
                        : (_showAmPm ? "h:mm tt" : "h:mm"));
                using (var timeFont = new Font("Georgia", 29F, FontStyle.Bold))
                using (var dateFont = new Font("Georgia", 11F))
                using (var primary = new SolidBrush(theme.PrimaryText))
                using (var secondary = new SolidBrush(theme.SecondaryText))
                {
                    DrawCentered(graphics,
                        _displayTime.ToString(format, CultureInfo.CurrentCulture),
                        timeFont, primary, new RectangleF(61, 113, 238, 70));
                    if (_showDate)
                        DrawCentered(graphics,
                            _displayTime.ToString("dddd, MMMM d", CultureInfo.CurrentCulture),
                            dateFont, secondary, new RectangleF(70, 195, 220, 30));
                }
                return;
            }

            // The face is intentionally above the geometric image centre;
            // these coordinates follow the production skin's black dial.
            const float centerX = 180F;
            const float centerY = 174F;
            using (var numeralFont = new Font("Georgia", 13F, FontStyle.Bold))
            using (var numeralBrush = new SolidBrush(theme.PrimaryText))
            {
                for (int hour = 1; hour <= 12; hour++)
                {
                    double angle = hour * Math.PI / 6D - Math.PI / 2D;
                    float x = centerX + (float)Math.Cos(angle) * 108F - 16F;
                    float y = centerY + (float)Math.Sin(angle) * 108F - 14F;
                    DrawCentered(graphics,
                        hour.ToString(CultureInfo.InvariantCulture),
                        numeralFont, numeralBrush,
                        new RectangleF(x, y, 32, 28));
                }
            }
            double seconds = _displayTime.Second +
                (_smoothSeconds ? _displayTime.Millisecond / 1000D : 0D);
            double minutes = _displayTime.Minute + seconds / 60D;
            double hours = (_displayTime.Hour % 12) + minutes / 60D;
            const float faceRadius = 128F;
            DrawArtDecoHand(graphics, centerX, centerY,
                faceRadius * .38F, 2.4F, 4F, hours / 12D);
            DrawArtDecoHand(graphics, centerX, centerY,
                faceRadius * .60F, 2F, 5F, minutes / 60D);
            if (_showSeconds)
                DrawArtDecoSecondHand(graphics, centerX, centerY,
                    108F, seconds / 60D);
            DrawArtDecoHub(graphics, centerX, centerY);
            if (_showDate)
            {
                using (var dateFont = new Font("Georgia", 9F))
                using (var dateBrush = new SolidBrush(theme.SecondaryText))
                    DrawCentered(graphics,
                        _displayTime.ToString("MMM d", CultureInfo.CurrentCulture),
                        dateFont, dateBrush, new RectangleF(133, 232, 94, 24));
            }
        }

        private void RenderIndustrial(Graphics graphics, Rectangle bounds)
        {
            ReloadDesignerLayoutForCurrentTheme();
            string background = IndustrialClockDesignerLayout.BackgroundImage(
                IndustrialPresentationSkinPath);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            IndustrialClockDesignerElement editableBackground =
                IndustrialClockDesignerLayout.BackgroundElement();
            if (editableBackground != null)
            {
                if (editableBackground.Visible)
                    DesignerLayerPainter.DrawImageAlphaCropped(graphics,
                        editableBackground.ImagePath,
                        editableBackground.Bounds,
                        editableBackground.Opacity);
            }
            else if (!IndustrialClockDesignerLayout.BackgroundDeleted)
            {
                Image skin = ResolveClockImage(background, null);
                if (skin != null)
                    graphics.DrawImage(skin,
                        new Rectangle(0, 0, 360, 360));
            }
            if (_mode == "Digital")
            {
                string format = _use24Hour
                    ? (_showSeconds ? "HH:mm:ss" : "HH:mm")
                    : (_showSeconds ? (_showAmPm ? "h:mm:ss tt" : "h:mm:ss")
                        : (_showAmPm ? "h:mm tt" : "h:mm"));
                using (var timeFont = new Font("Segoe UI Semibold", 30F, FontStyle.Bold))
                using (var dateFont = new Font("Segoe UI", 11F))
                using (var ivory = new SolidBrush(Color.FromArgb(242, 226, 190)))
                using (var grey = new SolidBrush(Color.FromArgb(174, 177, 176)))
                {
                    DrawCentered(graphics, _displayTime.ToString(format,
                        CultureInfo.CurrentCulture), timeFont, ivory,
                        new RectangleF(60, 128, 240, 60));
                    if (_showDate)
                        DrawCentered(graphics, _displayTime.ToString(
                            "dddd, MMMM d", CultureInfo.CurrentCulture),
                            dateFont, grey, new RectangleF(70, 194, 220, 28));
                }
                return;
            }

            double seconds = _displayTime.Second +
                (_smoothSeconds ? _displayTime.Millisecond / 1000D : 0D);
            double minutes = _displayTime.Minute + seconds / 60D;
            double hours = (_displayTime.Hour % 12) + minutes / 60D;
            IndustrialClockDesignerElement hour =
                IndustrialClockDesignerLayout.BoundElement(
                    "Clock: Hour Hand", "clock-hour-hand");
            IndustrialClockDesignerElement minute =
                IndustrialClockDesignerLayout.BoundElement(
                    "Clock: Minute Hand", "clock-minute-hand");
            IndustrialClockDesignerElement second =
                IndustrialClockDesignerLayout.BoundElement(
                    "Clock: Second Hand", "clock-second-hand");
            IndustrialClockDesignerElement centre =
                IndustrialClockDesignerLayout.BoundElement(
                    "Clock: Centre Pivot", "clock-centre-pivot");
            float centreScale = Math.Max(.1F, centre.Scale);
            float cx = centre.X + centre.Width * centreScale / 2F;
            float cy = centre.Y + centre.Height * centreScale / 2F;
            if (UsesWoodlandNaturePresentation && !IndustrialClockDesignerLayout.ReplaceDefaultBackground)
                DrawWoodlandDial(graphics, cx, cy);
            DrawIndustrialDesignerHand(graphics, hour, hours / 12D, false,
                cx, cy);
            DrawIndustrialDesignerHand(graphics, minute, minutes / 60D, false,
                cx, cy);
            if (_showSeconds)
                DrawIndustrialDesignerHand(graphics, second, seconds / 60D,
                    true, cx, cy);
            DrawIndustrialDesignerHub(graphics, centre, cx, cy);
            // Nature/Steampunk stay date-free by default. An explicitly saved
            // visible date layer is rendered with the other saved overlays.
            if (_showDate && !UsesWoodlandNaturePresentation && !IsSteampunk &&
                !IndustrialClockDesignerLayout.HasSavedLayout)
                DrawIndustrialDesignerDate(graphics);
        }

        public void RenderPackagePreview(Graphics graphics, Rectangle bounds)
        {
            string oldMode = _mode;
            bool oldSeconds = _showSeconds, oldDate = _showDate;
            DateTime oldTime = _displayTime;
            try
            {
                _mode = "Analog";
                _showSeconds = true;
                _showDate = false;
                _displayTime = new DateTime(2026, 8, 28, 10, 10, 30);
                Render(graphics, bounds);
            }
            finally
            {
                _mode = oldMode;
                _showSeconds = oldSeconds; _showDate = oldDate;
                _displayTime = oldTime;
            }
        }

        private void DrawIndustrialDesignerHub(Graphics graphics,
            IndustrialClockDesignerElement centre, float cx, float cy)
        {
            if (centre == null || !centre.Visible) return;
            float size = Math.Max(4F, centre.Width *
                Math.Max(.1F, centre.Scale));
            float opacity = Math.Max(0F, Math.Min(1F, centre.Opacity));
            if (DesignerLayerPainter.DrawImage(graphics, centre.ImagePath,
                centre.Bounds, opacity)) return;
            Color outer = IsWoodland
                ? Color.FromArgb(108, 69, 36)
                : IsBotanical ? Color.FromArgb(210, 126, 139)
                : Color.FromArgb(82, 88, 91);
            Color inner = IsWoodland
                ? Color.FromArgb(214, 155, 90)
                : IsBotanical ? Color.FromArgb(245, 240, 226)
                : Color.FromArgb(190, 195, 195);
            Color edgeColor = IsWoodland
                ? Color.FromArgb(67, 45, 27)
                : IsBotanical ? Color.FromArgb(99, 117, 91)
                : Color.FromArgb(42, 45, 47);
            using (var hubOuter = new SolidBrush(Color.FromArgb(
                (int)(255 * opacity), outer)))
            using (var hubInner = new SolidBrush(Color.FromArgb(
                (int)(255 * opacity), inner)))
            using (var hubEdge = new Pen(Color.FromArgb(
                (int)(255 * opacity), edgeColor), Math.Max(1F, size * .075F)))
            {
                graphics.FillEllipse(hubOuter, cx - size / 2F,
                    cy - size / 2F, size, size);
                float innerSize = size * .6F;
                graphics.FillEllipse(hubInner, cx - innerSize / 2F,
                    cy - innerSize / 2F, innerSize, innerSize);
                graphics.DrawEllipse(hubEdge, cx - size / 2F,
                    cy - size / 2F, size, size);
            }
        }

        private void DrawWoodlandDial(Graphics graphics, float centerX, float centerY)
        {
            // All dial geometry is based on the SAME pivot used by the hands.
            // This keeps the numbers and markers concentric even when the
            // Designer pivot is moved slightly.
            using (var minutePen = new Pen(IsBotanical
                ? Color.FromArgb(99, 117, 91)
                : Color.FromArgb(228, 214, 181), IsBotanical ? 1.15F : 1.2F))
            using (var hourPen = new Pen(IsBotanical
                ? Color.FromArgb(210, 126, 139)
                : Color.FromArgb(242, 226, 190), IsBotanical ? 2.05F : 2.4F))
            {
                minutePen.StartCap = LineCap.Round;
                minutePen.EndCap = LineCap.Round;
                hourPen.StartCap = LineCap.Round;
                hourPen.EndCap = LineCap.Round;
                for (int tick = 0; tick < 60; tick++)
                {
                    double angle = tick * Math.PI / 30D - Math.PI / 2D;
                    bool major = tick % 5 == 0;
                    float outer = IsBotanical ? 132F : 116F;
                    float inner = major
                        ? (IsBotanical ? 122F : 105F)
                        : (IsBotanical ? 127F : 111F);
                    float x1 = centerX + (float)Math.Cos(angle) * inner;
                    float y1 = centerY + (float)Math.Sin(angle) * inner;
                    float x2 = centerX + (float)Math.Cos(angle) * outer;
                    float y2 = centerY + (float)Math.Sin(angle) * outer;
                    graphics.DrawLine(major ? hourPen : minutePen, x1, y1, x2, y2);
                }
            }

            using (var numeralFont = new Font("Georgia",
                IsBotanical ? 18F : 20F, FontStyle.Bold))
            using (var numeralBrush = new SolidBrush(IsBotanical
                ? Color.FromArgb(37, 63, 51)
                : Color.FromArgb(244, 232, 202)))
            {
                float numeralRadius = IsBotanical ? 104F : 92F;
                for (int hour = 1; hour <= 12; hour++)
                {
                    double angle = hour * Math.PI / 6D - Math.PI / 2D;
                    float boxWidth = IsBotanical ? 42F : 48F;
                    float boxHeight = IsBotanical ? 32F : 40F;
                    float x = centerX + (float)Math.Cos(angle) * numeralRadius - boxWidth / 2F;
                    float y = centerY + (float)Math.Sin(angle) * numeralRadius - boxHeight / 2F;
                    DrawCentered(graphics, hour.ToString(CultureInfo.InvariantCulture),
                        numeralFont, numeralBrush,
                        new RectangleF(x, y, boxWidth, boxHeight));
                }
            }
        }

        public void RenderIndustrialDesignerReference(
            Graphics graphics, Rectangle bounds, string backgroundPath = null,
            bool replaceDefaultBackground = false)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            string fallback = UsesIndustrialClockPresentation ? IndustrialPresentationSkinPath : IndustrialSkinPath;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            DesignerLayerPainter.DrawImage(graphics, backgroundPath ?? fallback,
                new RectangleF(0, 0, 360, 360), 1F);
            if (UsesWoodlandNaturePresentation && !replaceDefaultBackground)
                DrawWoodlandDial(graphics, 180F, 176.5F);
        }

        public void RenderIndustrialDesignerForeground(
            Graphics graphics, Rectangle bounds)
        {
            using (var hubOuter = new SolidBrush(Color.FromArgb(82, 88, 91)))
            using (var hubInner = new SolidBrush(Color.FromArgb(190, 195, 195)))
            using (var hubEdge = new Pen(Color.FromArgb(42, 45, 47), 1.5F))
            {
                graphics.FillEllipse(hubOuter, 170, 170, 20, 20);
                graphics.FillEllipse(hubInner, 174, 174, 12, 12);
                graphics.DrawEllipse(hubEdge, 170, 170, 20, 20);
            }
        }

        public void RenderIndustrialDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            float previewRotation, PointF sharedPivot, float opacity)
        {
            var element = new IndustrialClockDesignerElement
            {
                Id = elementId, X = bounds.X, Y = bounds.Y,
                Width = bounds.Width, Height = bounds.Height,
                Visible = true, Opacity = opacity, Scale = 1F
            };
            if (string.Equals(elementId, "clock-centre-pivot",
                StringComparison.OrdinalIgnoreCase))
            {
                DrawIndustrialDesignerHub(graphics, element,
                    bounds.X + bounds.Width / 2F,
                    bounds.Y + bounds.Height / 2F);
                return;
            }
            bool second = string.Equals(elementId, "clock-second-hand",
                StringComparison.OrdinalIgnoreCase);
            double turn = previewRotation / 360D;
            DrawIndustrialDesignerHand(graphics, element, turn, second,
                sharedPivot.X, sharedPivot.Y);
        }

        private void DrawIndustrialDesignerHand(Graphics graphics,
            IndustrialClockDesignerElement element, double turn,
            bool second, float cx, float cy)
        {
            if (element == null || !element.Visible) return;
            float scale = Math.Max(.1F, element.Scale);
            float tail = second ? 17F : 13F;
            float width = Math.Max(1F, element.Width * scale);
            float height = Math.Max(tail + 3F, element.Height * scale);
            float length = height - tail;
            float opacity = Math.Max(0F, Math.Min(1F, element.Opacity));
            PointF imagePivot = ClockHandPivot(element,
                new PointF(cx, cy));
            float rotationOffset =
                IndustrialClockDesignerLayout.ClockHandEditVersion > 0
                    ? element.PreviewRotation : 0F;
            double effectiveTurn = turn + rotationOffset / 360D;
            Image customHand = ResolveClockImage(element.ImagePath, null);
            // Builds before 1343 copied the old 54x104 and 34x132 leaf
            // artwork into DesignerAssets. Those absolute copies continue to
            // override new built-in theme assets after an upgrade unless they
            // are recognized here. Preserve every other custom PNG.
            bool legacyNatureLeafOverride =
                UsesWoodlandNaturePresentation &&
                (IsLegacyNatureLeafImage(customHand) ||
                    IsLegacyNatureLeafPath(element.ImagePath));
            if (legacyNatureLeafOverride)
                customHand = null;
            else if (customHand != null)
                customHand = ResolveClockImage(element.ImagePath, null,
                    (int)Math.Ceiling(width * 4F),
                    (int)Math.Ceiling(height * 4F));
            if (customHand != null)
            {
                DrawClockImageHand(graphics, customHand,
                    imagePivot.X, imagePivot.Y,
                    width, height, length, turn,
                    rotationOffset,
                    element.HandPivotX, element.HandPivotY,
                    opacity);
                return;
            }
            if (!second)
            {
                if (IsIndustrial)
                {
                    bool hourHand = string.Equals(element.Id,
                        "clock-hour-hand",
                        StringComparison.OrdinalIgnoreCase);
                    // Normalize the visible Industrial artwork rather than
                    // its differently padded PNG canvases. At 100% this gives
                    // a refined hierarchy of roughly 11px for the hour hand
                    // and 9.4px for the minute hand.
                    DrawWoodlandLeafImageHand(graphics,
                        imagePivot.X, imagePivot.Y,
                        hourHand ? IndustrialHourHandPath
                            : IndustrialMinuteHandPath,
                        hourHand ? Math.Max(13.5F, width * 1.35F)
                            : Math.Max(12.5F, width * 1.5625F),
                        hourHand ? Math.Min(74F, height * .86F)
                            : Math.Min(102F, height * .84F),
                        effectiveTurn, opacity);
                    return;
                }
                if (UsesWoodlandNaturePresentation)
                {
                    bool hourLeaf = string.Equals(element.Id,
                        "clock-hour-hand",
                        StringComparison.OrdinalIgnoreCase);
                    // Old personal Woodland layouts retain the wide canvas
                    // dimensions of the superseded leaf PNGs.  Once those
                    // copied files are migrated away, render the replacement
                    // hands at their standard geometry instead of stretching
                    // them to the legacy transparent canvas.
                    float layoutWidth = legacyNatureLeafOverride
                        ? (hourLeaf ? 11F : 8F) : width;
                    float layoutHeight = legacyNatureLeafOverride
                        ? (hourLeaf ? 86F : 121F) : height;
                    // The Botanical PNGs have very different amounts of
                    // transparent side padding. Normalize their visible
                    // strokes into a deliberate, delicate hierarchy instead
                    // of treating their canvas widths as the artwork widths:
                    // hour ~9.4px, minute ~8.2px at 100% scale.
                    float natureWidth = IsBotanical
                        ? (hourLeaf ? 15.5F / 11F : 33F / 8F) * layoutWidth
                        : (hourLeaf ? 13.5F / 11F : 8.5F / 8F) * layoutWidth;
                    float natureHeight =
                        (hourLeaf ? 72F / 86F : 100F / 121F) * layoutHeight;
                    DrawWoodlandLeafImageHand(graphics,
                        imagePivot.X, imagePivot.Y,
                        hourLeaf ? (IsBotanical ? BotanicalHourHandPath : WoodlandHourHandPath)
                            : (IsBotanical ? BotanicalMinuteHandPath : WoodlandMinuteHandPath),
                        natureWidth, natureHeight,
                        effectiveTurn, opacity);
                    return;
                }
                DrawIndustrialHand(graphics, imagePivot.X,
                    imagePivot.Y, length, width, effectiveTurn, opacity);
                return;
            }
            if (IsIndustrial && second)
            {
                DrawWoodlandLeafImageHand(graphics,
                    imagePivot.X, imagePivot.Y,
                    IndustrialSecondHandPath,
                    // The opaque amber needle occupies only seven pixels of
                    // its 12px canvas. A 6.4px draw width produces a clearly
                    // visible ~3.7px needle while keeping it the finest hand.
                    Math.Max(6.4F, width * 2.14F),
                    Math.Min(111F, height * .82F),
                    effectiveTurn, opacity);
                return;
            }
            if (UsesWoodlandNaturePresentation && second)
            {
                DrawWoodlandLeafImageHand(graphics,
                    imagePivot.X, imagePivot.Y,
                    IsBotanical
                        ? BotanicalSecondHandPath
                        : WoodlandSecondHandPath,
                    // The second hand remains the finest member of the set,
                    // but its ~3.7px visible stroke is no longer hairline-thin.
                    IsBotanical ? Math.Max(5.5F, width * 1.5F)
                        : Math.Max(.9F, width * .65F),
                    // At the default 133px layout height, 130px places the
                    // visible tip on the seconds markers (the image draw has
                    // a 3px covered centre overlap) while remaining inside
                    // the Botanical bezel.
                    IsBotanical ? Math.Min(130F, height)
                        : Math.Min(110F, height * .82F),
                    effectiveTurn, opacity);
                return;
            }
            if (UsesWoodlandNaturePresentation)
            {
                // Keep the seconds hand delicate and clear of the stone bezel.
                tail = 7F;
                length = Math.Min(108F, length * .90F);
                width = Math.Max(.65F, width * .42F);
            }
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(imagePivot.X, imagePivot.Y);
                graphics.RotateTransform((float)(effectiveTurn * 360D));
                using (var pen = new Pen(Color.FromArgb((int)(255 * opacity),
                    224, 157, 39), width))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawLine(pen, 0F, tail, 0F, -length);
                }
                if (UsesWoodlandNaturePresentation && second)
                {
                    float leafY = -length + 10F;
                    PointF[] leaf =
                    {
                        new PointF(0F, leafY - 8F),
                        new PointF(-3.2F, leafY - 2F),
                        new PointF(0F, leafY + 3F),
                        new PointF(3.2F, leafY - 2F)
                    };
                    using (var leafBrush = new SolidBrush(Color.FromArgb(
                        (int)(255 * opacity), 142, 218, 82)))
                    using (var leafEdge = new Pen(Color.FromArgb(
                        (int)(255 * opacity), 72, 133, 47), .7F))
                    {
                        graphics.FillPolygon(leafBrush, leaf);
                        graphics.DrawPolygon(leafEdge, leaf);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        private static Image ResolveClockImage(string path, string fallback)
        {
            return ResolveClockImage(path, fallback, 0, 0);
        }

        private static bool IsLegacyNatureLeafImage(Image image)
        {
            if (image == null) return false;
            return (image.Width == 54 && image.Height == 104) ||
                (image.Width == 34 && image.Height == 132);
        }

        private static bool IsLegacyNatureLeafPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string normalized = path.Replace('/', '\\');
            if (normalized.EndsWith(
                    "\\WoodlandNature\\woodland-custom-hour-hand.png",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(
                    "\\WoodlandNature\\woodland-custom-minute-hand.png",
                    StringComparison.OrdinalIgnoreCase))
                return true;
            if (normalized.IndexOf("\\DesignerAssets\\Clock\\",
                    StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return normalized.EndsWith("\\hour.png",
                       StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("\\min.png",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static Image ResolveClockImage(string path, string fallback,
            int requestedWidth, int requestedHeight)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    // theme:// is an EmilyDesk URI, not a Windows file path.
                    // Replacing its forward slashes first turns it into an
                    // unresolvable value such as theme:\\ember-glow\\..., which
                    // made imported clock faces and PNG hands disappear.
                    if (path.StartsWith("theme://",
                        StringComparison.OrdinalIgnoreCase))
                        return ThemeSkinCache.Get(path);
                    if (!Path.IsPathRooted(path))
                    {
                        string relative = path.Replace('/',
                            Path.DirectorySeparatorChar);
                        return ThemeSkinCache.Get(relative);
                    }
                    if (Path.IsPathRooted(path) && File.Exists(path))
                    {
                        string fullPath = Path.GetFullPath(path);
                        DateTime writeTime = File.GetLastWriteTimeUtc(fullPath);
                        string cacheKey = fullPath + "|" +
                            Math.Max(0, requestedWidth) + "x" +
                            Math.Max(0, requestedHeight);
                        lock (CustomClockImageSync)
                        {
                            Image cached;
                            DateTime cachedTime;
                            if (CustomClockImages.TryGetValue(cacheKey,
                                    out cached) &&
                                CustomClockImageTimes.TryGetValue(cacheKey,
                                    out cachedTime) && cachedTime == writeTime)
                                return cached;
                            using (Image source = Image.FromFile(fullPath))
                            {
                                int width = requestedWidth <= 0
                                    ? source.Width : Math.Max(1,
                                        Math.Min(2048, requestedWidth));
                                int height = requestedHeight <= 0
                                    ? source.Height : Math.Max(1,
                                        Math.Min(2048, requestedHeight));
                                if (requestedWidth <= 0 || requestedHeight <= 0)
                                    cached = new Bitmap(source);
                                else
                                {
                                    var preview = new Bitmap(width, height,
                                        PixelFormat.Format32bppPArgb);
                                    using (Graphics graphics =
                                        Graphics.FromImage(preview))
                                    {
                                        graphics.CompositingMode =
                                            CompositingMode.SourceCopy;
                                        graphics.InterpolationMode =
                                            InterpolationMode.HighQualityBicubic;
                                        graphics.PixelOffsetMode =
                                            PixelOffsetMode.HighQuality;
                                        graphics.DrawImage(source,
                                            new Rectangle(0, 0, width, height));
                                    }
                                    cached = preview;
                                }
                            }
                            Image previous;
                            if (CustomClockImages.TryGetValue(cacheKey,
                                    out previous) && previous != null)
                                previous.Dispose();
                            string pathPrefix = fullPath + "|";
                            foreach (string oldKey in new List<string>(
                                CustomClockImages.Keys))
                            {
                                if (oldKey == cacheKey ||
                                    !oldKey.StartsWith(pathPrefix,
                                        StringComparison.OrdinalIgnoreCase))
                                    continue;
                                Image oldImage = CustomClockImages[oldKey];
                                if (oldImage != null) oldImage.Dispose();
                                CustomClockImages.Remove(oldKey);
                                CustomClockImageTimes.Remove(oldKey);
                            }
                            CustomClockImages[cacheKey] = cached;
                            CustomClockImageTimes[cacheKey] = writeTime;
                            return cached;
                        }
                    }
                }
                catch { }
            }
            return string.IsNullOrWhiteSpace(fallback)
                ? null : ThemeSkinCache.Get(fallback);
        }

        private static void DrawClockImageHand(Graphics graphics, Image image,
            float cx, float cy, float width, float height, float length,
            double turn, float rotationOffset,
            float? pivotFractionX, float? pivotFractionY,
            float opacity)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(cx, cy);
                graphics.RotateTransform((float)(turn * 360D) +
                    rotationOffset);
                float anchorX = pivotFractionX.HasValue
                    ? width * ClampClockPivot(pivotFractionX.Value)
                    : width / 2F;
                float anchorY = pivotFractionY.HasValue
                    ? height * ClampClockPivot(pivotFractionY.Value)
                    : length;
                Rectangle destination = Rectangle.Round(new RectangleF(
                    -anchorX, -anchorY, width, height));
                if (opacity >= .995F)
                    graphics.DrawImage(image, destination);
                else
                {
                    using (var attributes = new ImageAttributes())
                    {
                        var matrix = new ColorMatrix();
                        matrix.Matrix33 = opacity;
                        attributes.SetColorMatrix(matrix);
                        graphics.DrawImage(image, destination,
                            0, 0, image.Width, image.Height,
                            GraphicsUnit.Pixel, attributes);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        private static PointF ClockHandPivot(
            XWidgetReborn.WidgetSdk.DesignerLayer element,
            PointF sharedPivot)
        {
            if (element == null)
                return sharedPivot;
            RectangleF bounds = element.Bounds;
            bool second = string.Equals(element.Binding,
                "Clock: Second Hand", StringComparison.OrdinalIgnoreCase);
            float legacyTail = second ? 17F : 13F;
            float x = element.HandPivotX.HasValue
                ? ClampClockPivot(element.HandPivotX.Value) : .5F;
            float y = element.HandPivotY.HasValue
                ? ClampClockPivot(element.HandPivotY.Value)
                : (bounds.Height - legacyTail) /
                    Math.Max(1F, bounds.Height);
            return new PointF(
                bounds.X + bounds.Width * x,
                bounds.Y + bounds.Height * y);
        }

        private static float ClampClockPivot(float value)
        {
            return Math.Max(0F, Math.Min(1F, value));
        }

        private static void DrawWoodlandLeafImageHand(
            Graphics graphics, float cx, float cy, string assetPath,
            float drawWidth, float drawHeight, double turn, float opacity)
        {
            Image image = ThemeSkinCache.Get(assetPath);
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(cx, cy);
                graphics.RotateTransform((float)(turn * 360D));
                RectangleF destination = new RectangleF(
                    -drawWidth / 2F, -drawHeight + 3F,
                    drawWidth, drawHeight);
                if (opacity >= .995F)
                {
                    graphics.DrawImage(image, destination);
                }
                else
                {
                    using (var attributes = new ImageAttributes())
                    {
                        var matrix = new ColorMatrix();
                        matrix.Matrix33 = opacity;
                        attributes.SetColorMatrix(matrix,
                            ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        graphics.DrawImage(image, Rectangle.Round(destination),
                            0, 0, image.Width, image.Height,
                            GraphicsUnit.Pixel, attributes);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        private void DrawIndustrialDesignerDate(Graphics graphics)
        {
            IndustrialClockDesignerElement element = IndustrialClockDesignerLayout.BoundElement(
                "Clock: Date", "clock-date");
            DesignerLayerPainter.Draw(graphics, element, ResolveClockDesignerText);
        }

        private string ResolveClockDesignerText(DesignerLayer element)
        {
            switch ((element.Binding ?? "").ToLowerInvariant())
            {
                case "clock: date": return _displayTime.ToString("MMM d", CultureInfo.CurrentCulture);
                case "clock: time": return _displayTime.ToString(_use24Hour ? "HH:mm" : "h:mm tt",
                    CultureInfo.CurrentCulture);
                case "clock: seconds": return _displayTime.ToString("ss", CultureInfo.CurrentCulture);
                default: return element.Text;
            }
        }

        private void DrawSavedClockLayers(Graphics graphics, Size size)
        {
            SavedDesignerLayout layout = SavedDesignerLayout.Current(_appearance, "clock");
            if (layout == null || layout.Elements == null) return;
            if (layout.EditableLayerVersion >= 1 && _mode == "Digital") return;
            if (!UsesIndustrialClockPresentation)
                DesignerLayerPainter.DrawImage(graphics, layout.BackgroundImage,
                    new RectangleF(0, 0, size.Width, size.Height), 1F);
            foreach (DesignerLayer layer in layout.Elements)
            {
                if (layer == null) continue;
                if (string.Equals(layer.Id, "main-background",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (!UsesIndustrialClockPresentation)
                        DesignerLayerPainter.DrawImageAlphaCropped(graphics,
                            layer.ImagePath, layer.Bounds, layer.Opacity);
                    continue;
                }
                string binding = layer.Binding ?? "";
                if (UsesIndustrialClockPresentation && layer.Kind == 1 &&
                    (binding == "Clock: Hour Hand" || binding == "Clock: Minute Hand" ||
                     binding == "Clock: Second Hand" || binding == "Clock: Centre Pivot")) continue;
                if (layer.Id == "clock-date" && !_showDate) continue;
                DesignerLayerPainter.Draw(graphics, layer, ResolveClockDesignerText);
            }
        }



        private static void DrawIndustrialHand(
            Graphics graphics, float cx, float cy,
            float length, float width, double turn, float opacity)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(cx, cy);
                graphics.RotateTransform((float)(turn * 360D));
                PointF[] points =
                {
                    new PointF(-width * .48F, 13F),
                    new PointF(-width * .36F, -length + 13F),
                    new PointF(0F, -length),
                    new PointF(width * .36F, -length + 13F),
                    new PointF(width * .48F, 13F)
                };
                using (var fill = new SolidBrush(Color.FromArgb(
                    (int)(255 * opacity), 244, 226, 185)))
                using (var edge = new Pen(Color.FromArgb(
                    (int)(255 * opacity), 129, 105, 62), 1.1F))
                {
                    graphics.FillPolygon(fill, points);
                    graphics.DrawPolygon(edge, points);
                }
            }
            finally { graphics.Restore(state); }
        }

        private void ApplyStyle(string value)
        {
            string normalized =
                NormalizeStyle(value);
            bool changed = _style != normalized;
            _style = normalized;
            if (_style == "Clock.Steampunk")
                _appearance = "Steampunk";
            else if (_appearance == "Steampunk")
                _appearance = "Modern";
            if (changed)
            {
                if (IsSteampunk)
                    _appearance = "Steampunk";
            }
            SaveSettings();
            ActivateMode();
        }

        private void FallBackToModern()
        {
            _style = "Clock.Modern";
            _appearance = "Modern";
            SaveSettings();
        }

        private void RenderModern(
            Graphics graphics,
            Rectangle bounds)
        {
            if (IsModern)
            {
                if (_mode == "Digital")
                    RenderModernDigitalV2(graphics, bounds);
                else
                    RenderModernAnalogV2(graphics, bounds);
                return;
            }
            if (IsVintage)
            {
                RenderVintage(graphics, bounds);
                return;
            }
            if (_mode == "Digital")
                RenderDigital(graphics, bounds);
            else
                RenderAnalog(graphics, bounds);
        }

        private void RenderModernAnalogV2(
            Graphics graphics, Rectangle bounds)
        {
            RectangleF face = new RectangleF(
                bounds.Left + 12F, bounds.Top + 12F,
                bounds.Width - 24F, bounds.Height - 24F);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(face);
                ModernThemePainter.FillGlassCircle(graphics, path, face);
            }

            const float centerX = 150F;
            const float centerY = 140F;
            const float markerRadius = 116F;
            using (var minutePen = new Pen(
                Color.FromArgb(105, ModernThemePainter.SecondaryText), 1F))
            using (var hourPen = new Pen(
                ModernThemePainter.PrimaryText, 2.2F))
            using (var accentPen = new Pen(
                ModernThemePainter.AccentBright, 2.6F))
            {
                for (int tick = 0; tick < 60; tick++)
                {
                    double angle = tick * Math.PI / 30D - Math.PI / 2D;
                    bool hour = tick % 5 == 0;
                    bool cardinal = tick % 15 == 0;
                    float length = cardinal ? 12F : hour ? 8F : 3.5F;
                    Pen pen = cardinal ? accentPen : hour ? hourPen : minutePen;
                    graphics.DrawLine(pen,
                        centerX + (float)Math.Cos(angle) *
                            (markerRadius - length),
                        centerY + (float)Math.Sin(angle) *
                            (markerRadius - length),
                        centerX + (float)Math.Cos(angle) * markerRadius,
                        centerY + (float)Math.Sin(angle) * markerRadius);
                }
            }

            if (_editableReferenceOnly) return;
            string[] cardinals = { "12", "3", "6", "9" };
            using (var font = new Font(
                "Segoe UI Semibold", 10F, FontStyle.Regular))
            using (var brush = new SolidBrush(
                ModernThemePainter.SecondaryText))
            {
                for (int index = 0; index < 4; index++)
                {
                    double angle = index * Math.PI / 2D - Math.PI / 2D;
                    float x = centerX +
                        (float)Math.Cos(angle) * 88F - 16F;
                    float y = centerY +
                        (float)Math.Sin(angle) * 88F - 10F;
                    DrawCentered(graphics, cardinals[index], font, brush,
                        new RectangleF(x, y, 32F, 20F));
                }
            }

            double seconds = _displayTime.Second +
                (_smoothSeconds ? _displayTime.Millisecond / 1000D : 0D);
            double minutes = _displayTime.Minute + seconds / 60D;
            double hours = (_displayTime.Hour % 12) + minutes / 60D;
            DrawModernHand(graphics, centerX, centerY,
                57F, 7F, hours / 12D, ModernThemePainter.PrimaryText);
            DrawModernHand(graphics, centerX, centerY,
                83F, 4.2F, minutes / 60D, ModernThemePainter.PrimaryText);
            if (_showSeconds)
            {
                using (var secondsPen = new Pen(
                    ModernThemePainter.AccentBright, 1.5F))
                {
                    secondsPen.StartCap = LineCap.Round;
                    secondsPen.EndCap = LineCap.Round;
                    DrawHand(graphics, secondsPen, centerX, centerY,
                        96F, seconds / 60D);
                }
            }
            using (var halo = new SolidBrush(
                Color.FromArgb(70, ModernThemePainter.Accent)))
            using (var cap = new SolidBrush(
                ModernThemePainter.AccentBright))
            using (var capEdge = new Pen(
                ModernThemePainter.DeepBorder, 1.2F))
            {
                graphics.FillEllipse(halo,
                    centerX - 10F, centerY - 10F, 20F, 20F);
                graphics.FillEllipse(cap,
                    centerX - 5F, centerY - 5F, 10F, 10F);
                graphics.DrawEllipse(capEdge,
                    centerX - 5F, centerY - 5F, 10F, 10F);
            }

        }

        private static void DrawModernHand(
            Graphics graphics, float centerX, float centerY,
            float length, float halfWidth, double fraction, Color color)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(centerX, centerY);
                graphics.RotateTransform((float)(fraction * 360D));
                PointF[] hand =
                {
                    new PointF(-halfWidth, 9F),
                    new PointF(-halfWidth * .62F, -length + 7F),
                    new PointF(0F, -length),
                    new PointF(halfWidth * .62F, -length + 7F),
                    new PointF(halfWidth, 9F)
                };
                using (var shadow = new SolidBrush(
                    Color.FromArgb(95, 0, 0, 0)))
                using (var fill = new SolidBrush(color))
                {
                    graphics.TranslateTransform(1.5F, 2F);
                    graphics.FillPolygon(shadow, hand);
                    graphics.TranslateTransform(-1.5F, -2F);
                    graphics.FillPolygon(fill, hand);
                }
            }
            finally { graphics.Restore(state); }
        }

        private void RenderModernDigitalV2(
            Graphics graphics, Rectangle bounds)
        {
            RectangleF card = new RectangleF(
                6F, 6F, bounds.Width - 12F, bounds.Height - 12F);
            using (GraphicsPath path =
                ModernThemePainter.RoundedRectangle(card, 18F))
                ModernThemePainter.FillGlassCard(graphics, path, card);
            using (var accent = new SolidBrush(
                ModernThemePainter.Accent))
                graphics.FillRectangle(accent, 14F, 24F, 4F, 145F);
            ModernThemePainter.FillInsetPanel(graphics,
                new RectangleF(29F, 45F, bounds.Width - 52F, 72F), 11F);
            using (var labelFont = new Font(
                "Segoe UI Semibold", 8F, FontStyle.Bold))
            using (var timeFont = new Font(
                "Segoe UI Semibold", 31F, FontStyle.Regular))
            using (var dateFont = new Font(
                "Segoe UI", 10F, FontStyle.Regular))
            using (var accentText = new SolidBrush(
                ModernThemePainter.AccentBright))
            using (var main = new SolidBrush(
                ModernThemePainter.PrimaryText))
            using (var secondary = new SolidBrush(
                ModernThemePainter.SecondaryText))
            {
                graphics.DrawString("EMILYDESK  ·  TIME",
                    labelFont, accentText, 31F, 22F);
                string format = _use24Hour
                    ? (_showSeconds ? "HH:mm:ss" : "HH:mm")
                    : (_showSeconds
                        ? (_showAmPm ? "h:mm:ss tt" : "h:mm:ss")
                        : (_showAmPm ? "h:mm tt" : "h:mm"));
                DrawCentered(graphics,
                    _displayTime.ToString(format,
                        CultureInfo.CurrentCulture),
                    timeFont, main,
                    new RectangleF(34F, 50F,
                        bounds.Width - 62F, 62F));
                if (_showDate)
                    DrawCentered(graphics,
                        _displayTime.ToString("dddd, MMMM d",
                            CultureInfo.CurrentCulture),
                        dateFont, secondary,
                        new RectangleF(31F, 133F,
                            bounds.Width - 58F, 28F));
            }
        }

        private void RenderVintage(
            Graphics graphics, Rectangle bounds)
        {
            if (_mode == "Digital")
                RenderVintageDigital(graphics, bounds);
            else
                RenderVintageAnalog(graphics, bounds);
        }

        private void RenderVintageAnalog(
            Graphics graphics, Rectangle bounds)
        {
            var face = new RectangleF(
                bounds.Left + 12F, bounds.Top + 12F,
                bounds.Width - 24F, bounds.Height - 24F);
            using (var path = new GraphicsPath())
            using (var shadow = new Pen(
                Color.FromArgb(120, 35, 21, 12), 6F))
            using (var outer = new Pen(
                VintageThemePainter.DarkBrass, 2.5F))
            using (var brass = new Pen(
                VintageThemePainter.Brass, 1.6F))
            using (var inner = new Pen(
                Color.FromArgb(150, VintageThemePainter.DarkBrass), 1F))
            {
                path.AddEllipse(face);
                VintageThemePainter.FillPaper(graphics, path, face);
                graphics.DrawEllipse(shadow, face);
                graphics.DrawEllipse(outer, face);
                graphics.DrawEllipse(brass,
                    RectangleF.Inflate(face, -6F, -6F));
                graphics.DrawEllipse(inner,
                    RectangleF.Inflate(face, -11F, -11F));
            }

            const float centerX = 150F;
            const float centerY = 149F;
            const float tickRadius = 113F;
            using (var minutePen = new Pen(
                Color.FromArgb(115, VintageThemePainter.MutedInk), .8F))
            using (var hourPen = new Pen(
                VintageThemePainter.DarkBrass, 1.7F))
            {
                for (int tick = 0; tick < 60; tick++)
                {
                    double angle = tick * Math.PI / 30D - Math.PI / 2D;
                    bool hour = tick % 5 == 0;
                    float innerRadius = tickRadius - (hour ? 10F : 4F);
                    Pen pen = hour ? hourPen : minutePen;
                    graphics.DrawLine(pen,
                        centerX + (float)Math.Cos(angle) * innerRadius,
                        centerY + (float)Math.Sin(angle) * innerRadius,
                        centerX + (float)Math.Cos(angle) * tickRadius,
                        centerY + (float)Math.Sin(angle) * tickRadius);
                }
            }

            if (_editableReferenceOnly) return;
            string[] numerals =
            {
                "XII", "I", "II", "III", "IV", "V",
                "VI", "VII", "VIII", "IX", "X", "XI"
            };
            using (var numeralFont = new Font(
                "Georgia", 11.5F, FontStyle.Bold))
            using (var numeralBrush = new SolidBrush(
                VintageThemePainter.Ink))
            {
                for (int hour = 0; hour < 12; hour++)
                {
                    double angle = hour * Math.PI / 6D - Math.PI / 2D;
                    float x = centerX + (float)Math.Cos(angle) * 91F - 19F;
                    float y = centerY + (float)Math.Sin(angle) * 91F - 11F;
                    DrawCentered(graphics, numerals[hour], numeralFont,
                        numeralBrush, new RectangleF(x, y, 38F, 22F));
                }
            }

            double seconds = _displayTime.Second +
                (_smoothSeconds ? _displayTime.Millisecond / 1000D : 0D);
            double minutes = _displayTime.Minute + seconds / 60D;
            double hours = (_displayTime.Hour % 12) + minutes / 60D;
            DrawVintageHand(graphics, centerX, centerY,
                58F, 6.5F, hours / 12D);
            DrawVintageHand(graphics, centerX, centerY,
                84F, 4.5F, minutes / 60D);
            if (_showSeconds)
            {
                using (var secondPen = new Pen(
                    Color.FromArgb(159, 69, 58), 1.4F))
                {
                    secondPen.StartCap = LineCap.Round;
                    secondPen.EndCap = LineCap.Round;
                    DrawHand(graphics, secondPen, centerX, centerY,
                        96F, seconds / 60D);
                }
            }
            using (var cap = new SolidBrush(VintageThemePainter.Brass))
            using (var capEdge = new Pen(VintageThemePainter.DarkBrass, 1.2F))
            {
                graphics.FillEllipse(cap, centerX - 6F, centerY - 6F, 12F, 12F);
                graphics.DrawEllipse(capEdge, centerX - 6F, centerY - 6F, 12F, 12F);
            }

            if (_showDate)
            {
                using (var dateFont = new Font(
                    "Georgia", 8.5F, FontStyle.Italic))
                using (var dateBrush = new SolidBrush(
                    VintageThemePainter.MutedInk))
                    DrawCentered(graphics,
                        _displayTime.ToString("dddd, MMMM d",
                            CultureInfo.CurrentCulture),
                        dateFont, dateBrush,
                        new RectangleF(36F, 255F, 228F, 20F));
            }
        }

        private static void DrawVintageHand(
            Graphics graphics, float centerX, float centerY,
            float length, float halfWidth, double fraction)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(centerX, centerY);
                graphics.RotateTransform((float)(fraction * 360D));
                PointF[] hand =
                {
                    new PointF(-halfWidth, 10F),
                    new PointF(-halfWidth * .62F, -length + 10F),
                    new PointF(0F, -length),
                    new PointF(halfWidth * .62F, -length + 10F),
                    new PointF(halfWidth, 10F)
                };
                using (var fill = new SolidBrush(VintageThemePainter.Ink))
                using (var edge = new Pen(
                    VintageThemePainter.DarkBrass, .9F))
                {
                    graphics.FillPolygon(fill, hand);
                    graphics.DrawPolygon(edge, hand);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void RenderVintageDigital(
            Graphics graphics, Rectangle bounds)
        {
            RectangleF card = new RectangleF(
                bounds.Left + 5F, bounds.Top + 5F,
                bounds.Width - 10F, bounds.Height - 10F);
            using (GraphicsPath path =
                VintageThemePainter.RoundedRectangle(card, 17F))
            {
                VintageThemePainter.FillPaper(graphics, path, card);
                VintageThemePainter.DrawDoubleBorder(graphics, path,
                    RectangleF.Inflate(card, -8F, -8F), 11F);
            }
            using (var timeFont = new Font(
                "Georgia", 31F, FontStyle.Bold))
            using (var dateFont = new Font(
                "Georgia", 10.5F, FontStyle.Italic))
            using (var main = new SolidBrush(VintageThemePainter.Ink))
            using (var secondary = new SolidBrush(
                VintageThemePainter.MutedInk))
            {
                string format = _use24Hour
                    ? (_showSeconds ? "HH:mm:ss" : "HH:mm")
                    : (_showSeconds
                        ? (_showAmPm ? "h:mm:ss tt" : "h:mm:ss")
                        : (_showAmPm ? "h:mm tt" : "h:mm"));
                DrawCentered(graphics,
                    _displayTime.ToString(format,
                        CultureInfo.CurrentCulture),
                    timeFont, main,
                    new RectangleF(24F, 43F,
                        bounds.Width - 48F, 62F));
                if (_showDate)
                    DrawCentered(graphics,
                        _displayTime.ToString("dddd, MMMM d",
                            CultureInfo.CurrentCulture),
                        dateFont, secondary,
                        new RectangleF(28F, 126F,
                            bounds.Width - 56F, 28F));
            }
        }

        private void RenderSteampunk(
            Graphics graphics,
            Rectangle bounds)
        {
            if (_mode == "Digital")
                RenderSteampunkDigital(
                    graphics,
                    bounds);
            else
                RenderSteampunkAnalog(
                    graphics,
                    bounds);
        }

        private void RenderSteampunkAnalog(
            Graphics graphics,
            Rectangle bounds)
        {
            ClockPalette palette =
                SteampunkPalette();
            Rectangle outer = new Rectangle(
                bounds.Left + 3,
                bounds.Top + 3,
                bounds.Width - 6,
                bounds.Height - 6);
            Rectangle dial = new Rectangle(
                bounds.Left + 17,
                bounds.Top + 17,
                bounds.Width - 34,
                bounds.Height - 34);
            using (var frame =
                new SolidBrush(palette.SurfaceTop))
            using (var dialBrush =
                new SolidBrush(palette.SurfaceBottom))
            using (var framePen =
                new Pen(palette.Border, 3F))
            using (var highlightPen =
                new Pen(Color.FromArgb(155, palette.Accent), 2F))
            {
                graphics.FillEllipse(frame, outer);
                graphics.DrawEllipse(framePen, outer);
                graphics.DrawEllipse(
                    highlightPen,
                    outer.Left + 7,
                    outer.Top + 7,
                    outer.Width - 14,
                    outer.Height - 14);
                graphics.FillEllipse(
                    dialBrush,
                    dial);
                graphics.DrawEllipse(
                    framePen,
                    dial);
            }

            using (var rivet =
                new SolidBrush(palette.Accent))
            using (var shadow =
                new Pen(Color.FromArgb(
                    95,
                    Color.Black),
                    2F))
            {
                for (int index = 0;
                    index < 8;
                    index++)
                {
                    double angle =
                        index * Math.PI / 4D;
                    float x = 150F +
                        (float)Math.Cos(angle) *
                        132F;
                    float y = 150F +
                        (float)Math.Sin(angle) *
                        132F;
                    graphics.FillEllipse(
                        rivet,
                        x - 4,
                        y - 4,
                        8,
                        8);
                    graphics.DrawEllipse(
                        shadow,
                        x - 4,
                        y - 4,
                        8,
                        8);
                }
            }

            DrawSteampunkGear(
                graphics,
                63F,
                63F,
                23F,
                DecorativeGearAngle(false),
                palette);
            DrawSteampunkGear(
                graphics,
                239F,
                70F,
                17F,
                DecorativeGearAngle(true),
                palette);

            string[] numerals =
            {
                "XII", "I", "II", "III",
                "IV", "V", "VI", "VII",
                "VIII", "IX", "X", "XI"
            };
            using (var numeralFont = new Font(
                "Georgia",
                11F,
                FontStyle.Bold))
            using (var numeralBrush =
                new SolidBrush(
                    palette.PrimaryText))
            {
                for (int hour = 0;
                    hour < 12;
                    hour++)
                {
                    if (_showDate &&
                        hour >= 5 &&
                        hour <= 7)
                        continue;
                    double angle =
                        hour * Math.PI / 6D -
                        Math.PI / 2D;
                    float x = 150F +
                        (float)Math.Cos(angle) *
                        100F - 18F;
                    float y = 143F +
                        (float)Math.Sin(angle) *
                        100F - 11F;
                    DrawCentered(
                        graphics,
                        numerals[hour],
                        numeralFont,
                        numeralBrush,
                        new RectangleF(
                            x,
                            y,
                            36,
                            22));
                }
            }

            DrawSteampunkHands(
                graphics,
                palette,
                150F,
                143F);
            if (_showDate)
            {
                Rectangle datePlate =
                    new Rectangle(
                        67,
                        222,
                        166,
                        27);
                using (GraphicsPath platePath =
                    RoundedRectangle(
                        datePlate,
                        8))
                using (var plateBrush =
                    new SolidBrush(
                        Color.FromArgb(
                            255,
                            palette.SurfaceBottom)))
                using (var platePen =
                    new Pen(palette.Border))
                {
                    graphics.FillPath(
                        plateBrush,
                        platePath);
                    graphics.DrawPath(
                        platePen,
                        platePath);
                }
                using (var dateFont = new Font(
                    "Georgia",
                    9F,
                    FontStyle.Italic))
                using (var dateBrush =
                    new SolidBrush(
                        palette.SecondaryText))
                    DrawCentered(
                        graphics,
                        _displayTime.ToString(
                            "MMM d, yyyy",
                            CultureInfo.CurrentCulture),
                        dateFont,
                        dateBrush,
                        new RectangleF(
                            67,
                            223,
                            166,
                            24));
            }
        }

        private void RenderSteampunkDigital(
            Graphics graphics,
            Rectangle bounds)
        {
            ClockPalette palette =
                SteampunkPalette();
            using (GraphicsPath path =
                RoundedRectangle(bounds, 20))
            using (var frame =
                new SolidBrush(palette.SurfaceTop))
            using (var border =
                new Pen(palette.Border, 3F))
            using (var text =
                new SolidBrush(palette.PrimaryText))
            using (var secondary =
                new SolidBrush(
                    palette.SecondaryText))
            using (var timeFont = new Font(
                "Georgia",
                30F,
                FontStyle.Bold))
            using (var dateFont = new Font(
                "Georgia",
                10F,
                FontStyle.Italic))
            {
                graphics.FillPath(frame, path);
                graphics.DrawPath(border, path);
                Rectangle inset = new Rectangle(
                    14,
                    14,
                    bounds.Width - 28,
                    bounds.Height - 28);
                using (GraphicsPath insetPath =
                    RoundedRectangle(inset, 13))
                using (var insetBrush =
                    new SolidBrush(
                        palette.SurfaceBottom))
                {
                    graphics.FillPath(
                        insetBrush,
                        insetPath);
                    graphics.DrawPath(
                        border,
                        insetPath);
                }
                string format = _use24Hour
                    ? (_showSeconds
                        ? "HH:mm:ss"
                        : "HH:mm")
                    : (_showSeconds
                        ? (_showAmPm
                            ? "h:mm:ss tt"
                            : "h:mm:ss")
                        : (_showAmPm
                            ? "h:mm tt"
                            : "h:mm"));
                DrawCentered(
                    graphics,
                    _displayTime.ToString(
                        format,
                        CultureInfo.CurrentCulture),
                    timeFont,
                    text,
                    new RectangleF(
                        24,
                        42,
                        bounds.Width - 48,
                        65));
                if (_showDate)
                    DrawCentered(
                        graphics,
                        _displayTime.ToString(
                            "dddd, MMMM d",
                            CultureInfo.CurrentCulture),
                        dateFont,
                        secondary,
                        new RectangleF(
                            28,
                            124,
                            bounds.Width - 56,
                            28));
            }
        }

        private float DecorativeGearAngle(bool minuteGear)
        {
            if (_animationLevel == "Off")
                return 0F;
            if (minuteGear)
                return (float)(_displayTime.Minute * 6D);
            return (float)(_displayTime.Second * 6D);
        }

        private static void DrawSteampunkGear(
            Graphics graphics,
            float centerX,
            float centerY,
            float radius,
            float angleDegrees,
            ClockPalette palette)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(centerX, centerY);
                graphics.RotateTransform(angleDegrees);
                using (var toothBrush = new SolidBrush(
                    Color.FromArgb(190, palette.Accent)))
                using (var bodyBrush = new SolidBrush(
                    Color.FromArgb(215, palette.SurfaceTop)))
                using (var pen = new Pen(palette.Border, 1.5F))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        graphics.RotateTransform(36F);
                        graphics.FillRectangle(
                            toothBrush,
                            -3F,
                            -radius - 5F,
                            6F,
                            9F);
                    }
                    graphics.FillEllipse(
                        bodyBrush,
                        -radius,
                        -radius,
                        radius * 2F,
                        radius * 2F);
                    graphics.DrawEllipse(
                        pen,
                        -radius,
                        -radius,
                        radius * 2F,
                        radius * 2F);
                    using (var hubBrush = new SolidBrush(
                        palette.SurfaceBottom))
                        graphics.FillEllipse(
                            hubBrush,
                            -radius * .34F,
                            -radius * .34F,
                            radius * .68F,
                            radius * .68F);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawSteampunkHands(
            Graphics graphics,
            ClockPalette palette,
            float centerX,
            float centerY)
        {
            double seconds = _displayTime.Second +
                (_smoothSeconds
                    ? _displayTime.Millisecond /
                        1000D
                    : 0D);
            double minutes =
                _displayTime.Minute +
                seconds / 60D;
            double hours =
                (_displayTime.Hour % 12) +
                minutes / 60D;
            using (var hourPen = new Pen(
                palette.PrimaryText,
                8F))
            using (var minutePen = new Pen(
                palette.PrimaryText,
                5F))
            using (var secondPen = new Pen(
                palette.Accent,
                2F))
            using (var cap = new SolidBrush(
                palette.Accent))
            {
                hourPen.StartCap =
                    LineCap.Round;
                hourPen.EndCap =
                    LineCap.ArrowAnchor;
                minutePen.StartCap =
                    LineCap.Round;
                minutePen.EndCap =
                    LineCap.ArrowAnchor;
                DrawHand(
                    graphics,
                    hourPen,
                    centerX,
                    centerY,
                    56F,
                    hours / 12D);
                DrawHand(
                    graphics,
                    minutePen,
                    centerX,
                    centerY,
                    82F,
                    minutes / 60D);
                if (_showSeconds)
                    DrawHand(
                        graphics,
                        secondPen,
                        centerX,
                        centerY,
                        88F,
                        seconds / 60D);
                graphics.FillEllipse(
                    cap,
                    centerX - 7,
                    centerY - 7,
                    14,
                    14);
            }
        }

        private ClockPalette Palette()
        {
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_appearance);
            return new ClockPalette(
                theme.SurfaceTop,
                theme.SurfaceBottom,
                theme.PrimaryText,
                theme.SecondaryText,
                theme.Accent,
                theme.Border);
        }

        private ClockPalette SteampunkPalette()
        {
            return Palette();
        }

        private void RequestInvalidate()
        {
            if (_host != null) _host.Invalidate();
            if (_invalidate != null) _invalidate();
        }

        private static void DrawHand(
            Graphics graphics,
            Pen pen,
            float centerX,
            float centerY,
            float length,
            double fraction)
        {
            double angle =
                fraction * Math.PI * 2 -
                Math.PI / 2;
            graphics.DrawLine(
                pen,
                centerX,
                centerY,
                centerX +
                    (float)Math.Cos(angle) * length,
                centerY +
                    (float)Math.Sin(angle) * length);
        }

        private static void DrawArtDecoHand(
            Graphics graphics, float centerX, float centerY,
            float length, float halfWidth, float tail, double fraction)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(centerX, centerY);
                graphics.RotateTransform((float)(fraction * 360D));
                using (var path = new GraphicsPath())
                {
                    path.AddPolygon(new[]
                    {
                        new PointF(0F, -length),
                        new PointF(halfWidth * .55F, -length + 6F),
                        new PointF(halfWidth, -11F),
                        new PointF(halfWidth, tail),
                        new PointF(-halfWidth, tail),
                        new PointF(-halfWidth, -11F),
                        new PointF(-halfWidth * .55F, -length + 6F)
                    });
                    using (var visibilityEdge = new Pen(
                        Color.FromArgb(225, 232, 208, 151), 3.15F))
                    using (var blackLine = new Pen(
                        Color.FromArgb(245, 8, 10, 10), 1.15F))
                    {
                        graphics.DrawPath(visibilityEdge, path);
                        graphics.DrawPath(blackLine, path);
                        RectangleF aperture = new RectangleF(-2.7F, -16.7F, 5.4F, 5.4F);
                        graphics.DrawEllipse(visibilityEdge, aperture);
                        graphics.DrawEllipse(blackLine, aperture);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        private static void DrawArtDecoSecondHand(
            Graphics graphics, float centerX, float centerY,
            float length, double fraction)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(centerX, centerY);
                graphics.RotateTransform((float)(fraction * 360D));
                Color teal = Color.FromArgb(45, 184, 174);
                using (var pen = new Pen(teal, 1.7F))
                using (var dark = new Pen(Color.FromArgb(170, 8, 65, 67), 3.2F))
                using (var brush = new SolidBrush(teal))
                {
                    graphics.DrawLine(dark, 0F, 17F, 0F, -length);
                    graphics.DrawLine(pen, 0F, 17F, 0F, -length);
                    graphics.FillPolygon(brush, new[]
                    {
                        new PointF(0F, 23F), new PointF(4F, 15F),
                        new PointF(0F, 8F), new PointF(-4F, 15F)
                    });
                }
            }
            finally { graphics.Restore(state); }
        }

        private static void DrawArtDecoHub(
            Graphics graphics, float centerX, float centerY)
        {
            RectangleF hub = new RectangleF(centerX - 5.5F, centerY - 5.5F, 11F, 11F);
            using (var black = new SolidBrush(Color.FromArgb(250, 7, 9, 9)))
            using (var brass = new Pen(Color.FromArgb(224, 178, 91), 1F))
            {
                graphics.FillEllipse(black, hub);
                graphics.DrawEllipse(brass, hub);
            }
        }

        private static void DrawCentered(
            Graphics graphics,
            string text,
            Font font,
            Brush brush,
            RectangleF bounds)
        {
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
                graphics.DrawString(
                    text,
                    font,
                    brush,
                    bounds,
                    format);
        }

        private static GraphicsPath RoundedRectangle(
            Rectangle rect,
            int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(
                rect.Left,
                rect.Top,
                diameter,
                diameter,
                180,
                90);
            path.AddArc(
                rect.Right - diameter,
                rect.Top,
                diameter,
                diameter,
                270,
                90);
            path.AddArc(
                rect.Right - diameter,
                rect.Bottom - diameter,
                diameter,
                diameter,
                0,
                90);
            path.AddArc(
                rect.Left,
                rect.Bottom - diameter,
                diameter,
                diameter,
                90,
                90);
            path.CloseFigure();
            return path;
        }

        private static ComboBox AddCombo(
            Control parent,
            string label,
            int top,
            string[] items,
            string selected)
        {
            parent.Controls.Add(new Label
            {
                Text = label,
                Location = new Point(18, top + 6),
                Size = new Size(150, 24)
            });
            var combo = new ComboBox
            {
                DropDownStyle =
                    ComboBoxStyle.DropDownList,
                Location = new Point(180, top),
                Size = new Size(232, 28)
            };
            combo.Items.AddRange(items);
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0)
                combo.SelectedIndex = 0;
            parent.Controls.Add(combo);
            return combo;
        }

        private static CheckBox AddCheck(
            Control parent,
            string text,
            int top,
            bool value)
        {
            var check = new CheckBox
            {
                Text = text,
                Checked = value,
                Location = new Point(180, top),
                Size = new Size(232, 26)
            };
            parent.Controls.Add(check);
            return check;
        }

        private static NumericUpDown AddNumber(
            Control parent,
            string label,
            int top,
            int minimum,
            int maximum,
            decimal value)
        {
            parent.Controls.Add(new Label
            {
                Text = label + " (%)",
                Location = new Point(18, top + 5),
                Size = new Size(150, 24)
            });
            var number = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = Math.Max(
                    minimum,
                    Math.Min(maximum, value)),
                Location = new Point(180, top),
                Size = new Size(90, 27)
            };
            parent.Controls.Add(number);
            return number;
        }

        private static string NormalizeAnimationLevel(
            string value)
        {
            if (string.Equals(
                value,
                "Off",
                StringComparison.OrdinalIgnoreCase))
                return "Off";
            if (string.Equals(
                value,
                "Enhanced",
                StringComparison.OrdinalIgnoreCase))
                return "Enhanced";
            return "Minimal";
        }

        private static string NormalizeMode(string mode)
        {
            return string.Equals(
                mode,
                "Digital",
                StringComparison.OrdinalIgnoreCase)
                ? "Digital"
                : "Analog";
        }

        private static string NormalizeStyle(
            string value)
        {
            return string.Equals(
                value,
                "Clock.Steampunk",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    value,
                    "Steampunk",
                    StringComparison.OrdinalIgnoreCase)
                ? "Clock.Steampunk"
                : "Clock.Modern";
        }

        private static string NormalizeAppearance(
            string appearance,
            bool steampunk)
        {
            string normalized = EmilyDeskThemeCatalog.Normalize(appearance);
            return steampunk ? "Steampunk" : normalized;
        }

        private interface IClockStyleRenderer
        {
            void Render(
                NativeClockWidget widget,
                Graphics graphics,
                Rectangle bounds);
        }

        private sealed class ModernClockStyleRenderer :
            IClockStyleRenderer
        {
            public void Render(
                NativeClockWidget widget,
                Graphics graphics,
                Rectangle bounds)
            {
                widget.RenderModern(
                    graphics,
                    bounds);
            }
        }

        private sealed class SteampunkClockStyleRenderer :
            IClockStyleRenderer
        {
            public void Render(
                NativeClockWidget widget,
                Graphics graphics,
                Rectangle bounds)
            {
                widget.RenderSteampunk(
                    graphics,
                    bounds);
            }
        }

        private sealed class ArtDecoClockStyleRenderer :
            IClockStyleRenderer
        {
            public void Render(
                NativeClockWidget widget,
                Graphics graphics,
                Rectangle bounds)
            {
                widget.RenderArtDeco(graphics, bounds);
            }
        }

        private sealed class IndustrialClockStyleRenderer :
            IClockStyleRenderer
        {
            public void Render(NativeClockWidget widget,
                Graphics graphics, Rectangle bounds)
            {
                widget.RenderIndustrial(graphics, bounds);
            }
        }

        private sealed class ClockPalette
        {
            public ClockPalette(
                Color surfaceTop,
                Color surfaceBottom,
                Color primaryText,
                Color secondaryText,
                Color accent,
                Color border)
            {
                SurfaceTop = surfaceTop;
                SurfaceBottom = surfaceBottom;
                PrimaryText = primaryText;
                SecondaryText = secondaryText;
                Accent = accent;
                Border = border;
            }

            public Color SurfaceTop;
            public Color SurfaceBottom;
            public Color PrimaryText;
            public Color SecondaryText;
            public Color Accent;
            public Color Border;
        }
    }
}
