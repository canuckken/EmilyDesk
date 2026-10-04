using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Widgets.Calendar
{
    public sealed partial class NativeCalendarWidget :
        IWidget,
        IRuntimeAwareWidget,
        IOfficialWidget,
        IWidgetPointerInput,
        IWidgetThemeProvider,
        IWidgetStyleProvider,
        IWidgetSnapBoundsProvider
    {
        private const int BaseWidth = 420;
        private const int BaseHeight = 390;
        private const int ArtDecoWidth = 522;
        private const int ArtDecoHeight = 360;
        private const int ArtDecoPreferredWidth = 566;
        private const int ArtDecoPreferredHeight = 390;
        private const string ArtDecoSkinPath =
            @"Assets\Themes\ArtDeco\art-deco-calendar-skin.png";
        private const int IndustrialWidth = 540;
        private const int IndustrialHeight = 360;
        // The Industrial calendar artwork uses a taller transparent source
        // canvas than the Weather artwork.  This surface size makes the
        // visible calendar chrome match the visible Industrial Weather main
        // panel at 100%, while preserving the calendar's native proportions.
        private const int IndustrialPreferredWidth = 548;
        private const int IndustrialPreferredHeight = 382;
        private const string IndustrialSkinPath =
            @"Assets\Themes\Industrial\industrial-calendar-skin.png";
        private const string WoodlandSkinPath =
            @"Assets\Themes\WoodlandNature\woodland-calendar-skin.png";
        private const string BotanicalSkinPath =
            @"Assets\Themes\BotanicalNature\botanical-calendar-skin.png";
        private const string SteampunkSkinPath =
            @"Assets\Themes\Steampunk\steampunk-calendar-skin.png";

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
        private DateTime _now = DateTime.Now;
        private DateTime _displayMonth =
            new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                1);
        private bool _paused;
        private string _style = "Calendar.Modern";
        private string _appearance = "Modern";
        private string _weekStart = "System";
        private string _animationLevel = "Minimal";
        private Size _lastRenderSize =
            new Size(BaseWidth, BaseHeight);
        private CalendarButton _hovered;
        private CalendarButton _pressed;
        private bool _renderingSteampunk;
        private static readonly ICalendarStyleRenderer
            ModernRenderer =
                new ModernCalendarStyleRenderer();
        private static readonly ICalendarStyleRenderer
            SteampunkRenderer =
                new SteampunkCalendarStyleRenderer();
        private static readonly ICalendarStyleRenderer
            ArtDecoRenderer =
                new ArtDecoCalendarStyleRenderer();
        private static readonly ICalendarStyleRenderer
            IndustrialRenderer =
                new IndustrialCalendarStyleRenderer();

        public string Id { get { return "native.calendar"; } }
        public string Name { get { return "Calendar"; } }
        public string Description
        {
            get
            {
                return "Official native monthly calendar for EmilyDesk.";
            }
        }
        public string Version { get { return "1.0.0"; } }
        public Size DefaultSize
        {
            get { return new Size(BaseWidth, BaseHeight); }
        }
        public Point DefaultLocation
        {
            get { return new Point(410, 80); }
        }
        public WidgetUpdateRate UpdateRate
        {
            get { return WidgetUpdateRate.Minute; }
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
                    ? "Calendar.Steampunk"
                    : "Calendar.Modern";
                SaveSettings();
                ApplyPreferredPresentation();
                RequestInvalidate();
            }
        }
        public IEnumerable<WidgetStyleMetadata> Styles
        {
            get
            {
                return new[]
                {
                    new WidgetStyleMetadata(
                        "Calendar.Modern",
                        "Modern",
                        "The stable EmilyDesk Calendar design.",
                        "Calendar",
                        DefaultSize,
                        new Size(210, 195),
                        "preview.png",
                        new[]
                        {
                            "Midnight",
                            "Light",
                            "Ocean"
                        }),
                    new WidgetStyleMetadata(
                        "Calendar.Steampunk",
                        "Steampunk",
                        "A mechanical brass calendar with an aged dial surface.",
                        "Calendar",
                        DefaultSize,
                        new Size(210, 195),
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
            get
            {
                return _style ==
                    "Calendar.Steampunk";
            }
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

        private bool UsesIndustrialCalendarPresentation
        {
            get { return IsImportedTheme || IsIndustrial || IsSteampunk ||
                UsesWoodlandNaturePresentation; }
        }

        private string IndustrialPresentationSkinPath
        {
            get
            {
                string imported = EmilyDeskThemeCatalog.Get(_appearance).Asset("calendar");
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
            ApplyPreferredPresentation();
        }

        public void Start(Action invalidate)
        {
            _invalidate = invalidate;
            _now = DateTime.Now;
            RequestInvalidate();
        }

        public void Tick(DateTime now)
        {
            if (_paused) return;
            bool dateChanged = now.Date != _now.Date;
            _now = now;
            if (dateChanged)
                RequestInvalidate();
        }

        public void Pause()
        {
            _paused = true;
            RequestInvalidate();
        }

        public void Resume()
        {
            _paused = false;
            _now = DateTime.Now;
            RequestInvalidate();
        }

        public void Render(
            Graphics graphics,
            Rectangle bounds)
        {
            // WidgetWindow renders to ClientSize - 1 so the inclusive drawing
            // bounds do not clip the outer edge. Pointer coordinates, however,
            // arrive in the full client coordinate space.
            _lastRenderSize = new Size(
                bounds.Width + 1,
                bounds.Height + 1);
            GraphicsState state = graphics.Save();
            try
            {
                Size designSize = DesignSize();
                graphics.ScaleTransform(
                    (float)bounds.Width / designSize.Width,
                    (float)bounds.Height / designSize.Height);
                graphics.SmoothingMode =
                    SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode =
                    PixelOffsetMode.HighQuality;
                graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint
                        .ClearTypeGridFit;
                Rectangle logicalBounds =
                    new Rectangle(
                        0,
                        0,
                        designSize.Width - 1,
                        designSize.Height - 1);
                try
                {
                    if (RenderEditableCalendar(graphics, logicalBounds))
                    {
                        if (IsBotanical)
                            DrawBotanicalMainFrame(
                                graphics, logicalBounds, 8F, 21F);
                        return;
                    }
                    ActiveRenderer.Render(
                        this,
                        graphics,
                        logicalBounds);
                    if (!UsesIndustrialCalendarPresentation)
                    {
                        SavedDesignerLayout overlay = SavedDesignerLayout.Current(_appearance, "calendar");
                        if (overlay != null)
                        {
                            DesignerLayerPainter.DrawImage(graphics, overlay.BackgroundImage,
                                new RectangleF(0, 0, designSize.Width, designSize.Height), 1F);
                            overlay.Draw(graphics, ResolveCalendarDesignerText);
                        }
                    }
                    if (IsBotanical)
                        DrawBotanicalMainFrame(
                            graphics, logicalBounds, 8F, 21F);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(
                        "Calendar style '{0}' failed: {1}",
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
                    "Previous month",
                    false,
                    delegate
                    {
                        Navigate(
                            CalendarNavigation.Previous);
                    }),
                new WidgetMenuCommand(
                    "Today",
                    false,
                    delegate
                    {
                        Navigate(
                            CalendarNavigation.Today);
                    }),
                new WidgetMenuCommand(
                    "Next month",
                    false,
                    delegate
                    {
                        Navigate(
                            CalendarNavigation.Next);
                    }),
                new WidgetMenuCommand(
                    "Edit in Designer",
                    false,
                    delegate { OpenDesigner(); })
            };
        }

        private void OpenDesigner()
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "EmilyDesk.Designer.exe");
                if (!File.Exists(path)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--widget calendar --theme \"" +
                        _appearance + "\"",
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void ShowSettings()
        {
            using (var form = new Form())
            {
                form.Text = "Calendar Settings";
                form.StartPosition =
                    FormStartPosition.CenterScreen;
                form.FormBorderStyle =
                    FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(420, 333);
                form.Font = new Font("Segoe UI", 9F);

                ComboBox style = AddCombo(
                    form,
                    "Style",
                    22,
                    new[] { "Modern", "Steampunk" },
                    IsSteampunk
                        ? "Steampunk"
                        : "Modern");
                var styleDescription = new Label
                {
                    Text = IsSteampunk
                        ? "Mechanical frame, vintage type, and aged surface."
                        : "Stable EmilyDesk Calendar renderer.",
                    ForeColor = Color.DimGray,
                    Font = new Font("Segoe UI", 8F),
                    Location = new Point(174, 51),
                    Size = new Size(228, 16)
                };
                form.Controls.Add(styleDescription);
                ComboBox appearance = AddCombo(
                    form,
                    "Theme",
                    66,
                    new List<string>(EmilyDeskThemeCatalog.Names).ToArray(),
                    _appearance);
                style.SelectedIndexChanged += delegate
                {
                    bool steampunk =
                        style.SelectedItem.ToString() ==
                        "Steampunk";
                    styleDescription.Text = steampunk
                        ? "Mechanical frame, vintage type, and aged surface."
                        : "Stable EmilyDesk Calendar renderer.";
                    appearance.SelectedItem = steampunk
                        ? "Steampunk"
                        : "Modern";
                };
                ComboBox weekStart = AddCombo(
                    form,
                    "First day of week",
                    110,
                    new[]
                    {
                        "System",
                        "Sunday",
                        "Monday"
                    },
                    _weekStart);
                ComboBox animation = AddCombo(
                    form,
                    "Animation",
                    156,
                    new[] { "Off", "Minimal", "Enhanced" },
                    _animationLevel);
                NumericUpDown scale = AddNumber(
                    form,
                    "Scale",
                    200,
                    50,
                    200,
                    (decimal)Math.Round(
                        (_host == null
                            ? 1F
                            : _host.Scale) * 100));
                NumericUpDown opacity = AddNumber(
                    form,
                    "Opacity",
                    238,
                    20,
                    100,
                    (decimal)Math.Round(
                        (_host == null
                            ? 1D
                            : _host.Opacity) * 100));
                var apply = new Button
                {
                    Text = "Apply",
                    DialogResult = DialogResult.OK,
                    Location = new Point(238, 290),
                    Size = new Size(78, 30)
                };
                var cancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(324, 290),
                    Size = new Size(78, 30)
                };
                form.Controls.Add(apply);
                form.Controls.Add(cancel);
                form.AcceptButton = apply;
                form.CancelButton = cancel;

                if (form.ShowDialog() !=
                    DialogResult.OK)
                    return;
                _appearance = EmilyDeskThemeCatalog.Normalize(
                    appearance.SelectedItem.ToString());
                _style = _appearance == "Steampunk"
                    ? "Calendar.Steampunk"
                    : "Calendar.Modern";
                _weekStart =
                    weekStart.SelectedItem.ToString();
                _animationLevel = NormalizeAnimationLevel(
                    animation.SelectedItem.ToString());
                if (_host != null)
                {
                    _host.Scale =
                        (float)scale.Value / 100F;
                    _host.Opacity =
                        (double)opacity.Value / 100D;
                }
                SaveSettings();
                ApplyPreferredPresentation();
                RequestInvalidate();
            }
        }

        public void ResetSettings()
        {
            _style = "Calendar.Modern";
            _appearance = "Modern";
            _weekStart = "System";
            _animationLevel = "Minimal";
            if (_host != null)
            {
                _host.Scale = 1F;
                _host.Opacity = 1D;
            }
            _displayMonth = new DateTime(
                _now.Year,
                _now.Month,
                1);
            SaveSettings();
            ApplyPreferredPresentation();
            RequestInvalidate();
        }

        public bool PointerDown(
            Point location,
            WidgetPointerButton button)
        {
            if (button != WidgetPointerButton.Left)
                return false;
            CalendarButton target =
                HitTest(ToBase(location));
            if (target == CalendarButton.None)
                return false;
            _pressed = target;
            RequestInvalidate();
            return true;
        }

        public void PointerMove(Point location)
        {
            CalendarButton target =
                HitTest(ToBase(location));
            if (_hovered == target)
                return;
            _hovered = target;
            RequestInvalidate();
        }

        public void PointerUp(
            Point location,
            WidgetPointerButton button)
        {
            CalendarButton target =
                HitTest(ToBase(location));
            CalendarButton pressed = _pressed;
            _pressed = CalendarButton.None;
            if (button == WidgetPointerButton.Left &&
                target == pressed)
                ActivateButton(target);
            RequestInvalidate();
        }

        public void PointerLeave()
        {
            if (_hovered == CalendarButton.None)
                return;
            _hovered = CalendarButton.None;
            RequestInvalidate();
        }

        public void Dispose()
        {
            AttachRuntime(null);
            _invalidate = null;
            _host = null;
        }

        private void DrawCalendar(
            Graphics graphics,
            Rectangle bounds)
        {
            CalendarPalette palette = Palette();
            Rectangle surface = new Rectangle(
                8,
                8,
                bounds.Width - 16,
                bounds.Height - 16);
            using (GraphicsPath path =
                RoundedRectangle(surface, 22))
            using (var border =
                new Pen(palette.Border, 1.5F))
            {
                if (IsModern)
                {
                    ModernThemePainter.FillGlassCard(
                        graphics, path, surface);
                }
                else if (IsVintage)
                {
                    VintageThemePainter.FillPaper(
                        graphics, path, surface);
                    VintageThemePainter.DrawDoubleBorder(
                        graphics, path,
                        RectangleF.Inflate(surface, -8F, -8F), 14F);
                }
                else if (_renderingSteampunk)
                {
                    using (var background =
                        new SolidBrush(palette.SurfaceBottom))
                        graphics.FillPath(background, path);
                }
                else
                {
                    using (var background =
                        new LinearGradientBrush(
                            surface,
                            palette.SurfaceTop,
                            palette.SurfaceBottom,
                            90F))
                        graphics.FillPath(background, path);
                }
                if (!IsVintage && !IsModern)
                    graphics.DrawPath(border, path);
            }
            if (_renderingSteampunk)
                DrawSteampunkFrame(
                    graphics,
                    bounds,
                    palette);
            else if (IsVintage)
                DrawVintageCalendarFrame(graphics, bounds);
            else if (IsModern)
                DrawModernCalendarFrame(graphics, bounds);
            if (_editableReferenceOnly) return;

            using (var monthFont = new Font(
                _renderingSteampunk || IsVintage
                    ? "Georgia"
                    : "Segoe UI",
                17F,
                FontStyle.Bold))
            using (var normalFont = new Font(
                _renderingSteampunk || IsVintage
                    ? "Georgia"
                    : "Segoe UI",
                10F))
            using (var footerTitleFont = new Font(
                _renderingSteampunk || IsVintage
                    ? "Georgia"
                    : "Segoe UI",
                9.5F,
                FontStyle.Bold))
            using (var dayFont = new Font(
                _renderingSteampunk || IsVintage
                    ? "Georgia"
                    : "Segoe UI",
                10.5F,
                FontStyle.Regular))
            using (var primary =
                new SolidBrush(palette.PrimaryText))
            using (var secondary =
                new SolidBrush(palette.SecondaryText))
            {
                DrawCentered(
                    graphics,
                    _displayMonth.ToString(
                        "MMMM yyyy",
                        CultureInfo.CurrentCulture),
                    monthFont,
                    primary,
                    new RectangleF(82, 18, 256, 38));

                DrawButton(
                    graphics,
                    PreviousBounds(),
                    "‹",
                    CalendarButton.Previous,
                    palette,
                    monthFont);
                DrawButton(
                    graphics,
                    NextBounds(),
                    "›",
                    CalendarButton.Next,
                    palette,
                    monthFont);
                DrawButton(
                    graphics,
                    TodayBounds(),
                    "Today",
                    CalendarButton.Today,
                    palette,
                    normalFont);
                DayOfWeek firstDay = FirstDayOfWeek();
                bool viewingCurrentMonth =
                    IsViewingCurrentMonth();
                Rectangle grid = new Rectangle(
                    22,
                    82,
                    376,
                    248);
                float cellWidth = grid.Width / 7F;
                float headerHeight = 28F;
                float cellHeight =
                    (grid.Height - headerHeight) / 6F;
                DateTime first =
                    new DateTime(
                        _displayMonth.Year,
                        _displayMonth.Month,
                        1);
                int offset =
                    ((int)first.DayOfWeek -
                        (int)firstDay + 7) % 7;

                if (IsVintage)
                {
                    using (var gridPen = new Pen(
                        Color.FromArgb(54,
                            VintageThemePainter.DarkBrass), .8F))
                    {
                        for (int column = 0; column <= 7; column++)
                        {
                            float x = grid.Left + column * cellWidth;
                            graphics.DrawLine(gridPen, x,
                                grid.Top + headerHeight, x, grid.Bottom);
                        }
                        for (int row = 0; row <= 6; row++)
                        {
                            float y = grid.Top + headerHeight +
                                row * cellHeight;
                            graphics.DrawLine(gridPen,
                                grid.Left, y, grid.Right, y);
                        }
                    }
                }
                else if (IsModern)
                {
                    using (var rowBrush = new SolidBrush(
                        Color.FromArgb(22, ModernThemePainter.Accent)))
                    using (var gridPen = new Pen(
                        Color.FromArgb(45,
                            ModernThemePainter.Border), .8F))
                    {
                        for (int row = 0; row < 6; row += 2)
                            graphics.FillRectangle(rowBrush,
                                grid.Left,
                                grid.Top + headerHeight + row * cellHeight,
                                grid.Width, cellHeight);
                        for (int column = 0; column <= 7; column++)
                        {
                            float x = grid.Left + column * cellWidth;
                            graphics.DrawLine(gridPen, x,
                                grid.Top + headerHeight, x, grid.Bottom);
                        }
                        for (int row = 0; row <= 6; row++)
                        {
                            float y = grid.Top + headerHeight +
                                row * cellHeight;
                            graphics.DrawLine(gridPen,
                                grid.Left, y, grid.Right, y);
                        }
                    }
                }

                for (int column = 0;
                    column < 7;
                    column++)
                {
                    DayOfWeek day = (DayOfWeek)(
                        ((int)firstDay + column) % 7);
                    Color color = IsWeekend(day)
                        ? palette.Weekend
                        : palette.SecondaryText;
                    using (var brush =
                        new SolidBrush(color))
                        DrawCentered(
                            graphics,
                            CultureInfo.CurrentCulture
                                .DateTimeFormat
                                .GetAbbreviatedDayName(day),
                            normalFont,
                            brush,
                            new RectangleF(
                                grid.Left +
                                    column * cellWidth,
                                grid.Top,
                                cellWidth,
                                headerHeight));
                }

                int days = DateTime.DaysInMonth(
                    first.Year,
                    first.Month);
                for (int dayNumber = 1;
                    dayNumber <= days;
                    dayNumber++)
                {
                    int index = offset +
                        dayNumber - 1;
                    int row = index / 7;
                    int column = index % 7;
                    var day = new DateTime(
                        first.Year,
                        first.Month,
                        dayNumber);
                    RectangleF cell = new RectangleF(
                        grid.Left + column * cellWidth,
                        grid.Top + headerHeight +
                            row * cellHeight,
                        cellWidth,
                        cellHeight);
                    if (_renderingSteampunk)
                    {
                        using (var cellPen =
                            new Pen(
                                Color.FromArgb(
                                    55,
                                    palette.Border),
                                1F))
                            graphics.DrawRectangle(
                                cellPen,
                                cell.X + 3,
                                cell.Y + 2,
                                cell.Width - 6,
                                cell.Height - 4);
                    }
                    bool today =
                        viewingCurrentMonth &&
                        dayNumber == _now.Day;
                    if (today)
                    {
                        RectangleF highlight =
                            new RectangleF(
                                cell.X + 7,
                                cell.Y + 4,
                                cell.Width - 14,
                                cell.Height - 8);
                        using (var accent =
                            new SolidBrush(
                                palette.Accent))
                            graphics.FillEllipse(
                                accent,
                                highlight);
                    }
                    Color textColor = today
                        ? palette.TodayText
                        : IsWeekend(day.DayOfWeek)
                            ? palette.Weekend
                            : palette.PrimaryText;
                    using (var brush =
                        new SolidBrush(textColor))
                        DrawCentered(
                            graphics,
                            dayNumber.ToString(
                                CultureInfo.CurrentCulture),
                            dayFont,
                            brush,
                            cell);
                }

                if (viewingCurrentMonth)
                {
                    DrawCentered(
                        graphics,
                        "Today",
                        footerTitleFont,
                        primary,
                        new RectangleF(
                            22,
                            335,
                            376,
                            19));
                    DrawCentered(
                        graphics,
                        _now.ToString(
                            "dddd, MMMM d, yyyy",
                            CultureInfo.CurrentCulture),
                        normalFont,
                        secondary,
                        new RectangleF(
                            22,
                            353,
                            376,
                            24));
                }
                else
                {
                    DrawCentered(
                        graphics,
                        "Viewing " +
                            _displayMonth.ToString(
                                "MMMM yyyy",
                                CultureInfo.CurrentCulture),
                        footerTitleFont,
                        secondary,
                        new RectangleF(
                            22,
                            340,
                            376,
                            30));
                }

                if (_paused)
                    DrawCentered(
                        graphics,
                        "Paused",
                        normalFont,
                        secondary,
                        new RectangleF(
                            326,
                            61,
                            72,
                            18));
            }
        }

        private static void DrawVintageCalendarFrame(
            Graphics graphics, Rectangle bounds)
        {
            RectangleF header = new RectangleF(
                76F, 14F, bounds.Width - 152F, 46F);
            using (GraphicsPath headerPath =
                VintageThemePainter.RoundedRectangle(header, 11F))
            using (var headerFill = new SolidBrush(
                Color.FromArgb(42, VintageThemePainter.Brass)))
            using (var headerBorder = new Pen(
                Color.FromArgb(135,
                    VintageThemePainter.DarkBrass), 1F))
            using (var rule = new Pen(
                Color.FromArgb(120,
                    VintageThemePainter.DarkBrass), 1F))
            {
                graphics.FillPath(headerFill, headerPath);
                graphics.DrawPath(headerBorder, headerPath);
                graphics.DrawLine(rule, 22F, 76F,
                    bounds.Width - 22F, 76F);
                graphics.DrawLine(rule, 22F, 331F,
                    bounds.Width - 22F, 331F);
            }
            using (var ornament = new SolidBrush(
                VintageThemePainter.Brass))
            {
                graphics.FillEllipse(ornament, 16F, 16F, 5F, 5F);
                graphics.FillEllipse(ornament,
                    bounds.Width - 21F, 16F, 5F, 5F);
                graphics.FillEllipse(ornament,
                    16F, bounds.Height - 21F, 5F, 5F);
                graphics.FillEllipse(ornament,
                    bounds.Width - 21F, bounds.Height - 21F, 5F, 5F);
            }
        }

        private static void DrawModernCalendarFrame(
            Graphics graphics, Rectangle bounds)
        {
            ModernThemePainter.FillInsetPanel(graphics,
                new RectangleF(74F, 13F,
                    bounds.Width - 148F, 48F), 12F);
            ModernThemePainter.FillInsetPanel(graphics,
                new RectangleF(20F, 79F,
                    bounds.Width - 40F, 31F), 8F);
            ModernThemePainter.FillInsetPanel(graphics,
                new RectangleF(20F, 334F,
                    bounds.Width - 40F, 43F), 9F);
            using (var glow = new Pen(
                Color.FromArgb(105,
                    ModernThemePainter.AccentBright), 1.2F))
            {
                graphics.DrawLine(glow, 22F, 76F,
                    bounds.Width - 22F, 76F);
            }
        }

        private void DrawSteampunkFrame(
            Graphics graphics,
            Rectangle bounds,
            CalendarPalette palette)
        {
            using (var innerPen = new Pen(
                palette.Accent,
                2F))
            using (var rivetBrush =
                new SolidBrush(palette.Accent))
            using (var rivetPen =
                new Pen(palette.Border))
            {
                Rectangle inner = new Rectangle(
                    12,
                    12,
                    bounds.Width - 24,
                    bounds.Height - 24);
                using (GraphicsPath path =
                    RoundedRectangle(inner, 15))
                    graphics.DrawPath(
                        innerPen,
                        path);
                Point[] rivets =
                {
                    new Point(19, 19),
                    new Point(bounds.Width - 19, 19),
                    new Point(19, bounds.Height - 19),
                    new Point(
                        bounds.Width - 19,
                        bounds.Height - 19)
                };
                foreach (Point rivet in rivets)
                {
                    graphics.FillEllipse(
                        rivetBrush,
                        rivet.X - 4,
                        rivet.Y - 4,
                        8,
                        8);
                    graphics.DrawEllipse(
                        rivetPen,
                        rivet.X - 4,
                        rivet.Y - 4,
                        8,
                        8);
                }
                float gearAngle = _animationLevel == "Off"
                    ? 0F
                    : (float)((_displayMonth.Year * 12 +
                        _displayMonth.Month) * 12D);
                DrawDecorativeGear(
                    graphics, 42F, 43F, 13F, gearAngle, palette);
                DrawDecorativeGear(
                    graphics, bounds.Width - 42F, 43F, 10F,
                    -gearAngle, palette);
            }
        }

        private void DrawButton(
            Graphics graphics,
            Rectangle bounds,
            string text,
            CalendarButton button,
            CalendarPalette palette,
            Font font)
        {
            Color fill = button == _pressed
                ? palette.ButtonPressed
                : button == _hovered
                    ? palette.ButtonHover
                    : palette.Button;
            using (GraphicsPath path =
                RoundedRectangle(bounds, 9))
            using (var background =
                new SolidBrush(fill))
            using (var border =
                new Pen(palette.Border))
            using (var foreground =
                new SolidBrush(palette.PrimaryText))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
                DrawCentered(
                    graphics,
                    text,
                    font,
                    foreground,
                    bounds);
            }
        }

        private void ActivateButton(
            CalendarButton button)
        {
            if (button == CalendarButton.Previous)
                Navigate(CalendarNavigation.Previous);
            else if (button == CalendarButton.Next)
                Navigate(CalendarNavigation.Next);
            else if (button == CalendarButton.Today)
                Navigate(CalendarNavigation.Today);
        }

        private void Navigate(
            CalendarNavigation navigation)
        {
            if (navigation ==
                CalendarNavigation.Previous)
                _displayMonth =
                    _displayMonth.AddMonths(-1);
            else if (navigation ==
                CalendarNavigation.Next)
                _displayMonth =
                    _displayMonth.AddMonths(1);
            else
            {
                _now = DateTime.Now;
                _displayMonth = new DateTime(
                    _now.Year,
                    _now.Month,
                    1);
            }
            RequestInvalidate();
        }

        private Point ToBase(Point location)
        {
            Size designSize = DesignSize();
            return new Point(
                _lastRenderSize.Width <= 0
                    ? location.X
                    : (int)Math.Round(
                        location.X *
                        (double)designSize.Width /
                        _lastRenderSize.Width),
                _lastRenderSize.Height <= 0
                    ? location.Y
                    : (int)Math.Round(
                        location.Y *
                        (double)designSize.Height /
                        _lastRenderSize.Height));
        }

        private Rectangle PreviousBounds()
        {
            return EditableCalendarBounds("previous-button", IsArtDeco
                ? new Rectangle(52, 72, 38, 30)
                : UsesIndustrialCalendarPresentation
                ? IndustrialDesignerBounds("previous-button",
                    new Rectangle(52, 70, 38, 30))
                : new Rectangle(22, 20, 42, 36));
        }

        private Rectangle NextBounds()
        {
            return EditableCalendarBounds("next-button", IsArtDeco
                ? new Rectangle(432, 72, 38, 30)
                : UsesIndustrialCalendarPresentation
                ? IndustrialDesignerBounds("next-button",
                    new Rectangle(450, 70, 38, 30))
                : new Rectangle(356, 20, 42, 36));
        }

        private Rectangle TodayBounds()
        {
            return EditableCalendarBounds("today-button", IsArtDeco
                ? new Rectangle(390, 284, 80, 23)
                : UsesIndustrialCalendarPresentation
                ? IndustrialDesignerBounds("today-button",
                    new Rectangle(410, 300, 78, 18))
                : new Rectangle(174, 52, 72, 25));
        }

        private Rectangle IndustrialDesignerBounds(
            string id, Rectangle fallback)
        {
            IndustrialCalendarDesignerLayout layout =
                IndustrialCalendarDesignerLayout.Current(
                    DesignerLayoutFiles.FileName(_appearance, "calendar"));
            IndustrialCalendarDesignerElement element =
                layout == null ? null : layout.Find(id);
            if (layout == null) return fallback;
            return element == null || !element.Visible
                ? Rectangle.Empty : Rectangle.Round(element.Bounds);
        }

        private CalendarButton HitTest(
            Point point)
        {
            if (PreviousBounds().Contains(point))
                return CalendarButton.Previous;
            if (NextBounds().Contains(point))
                return CalendarButton.Next;
            if (TodayBounds().Contains(point))
                return CalendarButton.Today;
            return CalendarButton.None;
        }

        private bool IsViewingCurrentMonth()
        {
            return _displayMonth.Year == _now.Year &&
                _displayMonth.Month == _now.Month;
        }

        private DayOfWeek FirstDayOfWeek()
        {
            if (_weekStart == "Sunday")
                return DayOfWeek.Sunday;
            if (_weekStart == "Monday")
                return DayOfWeek.Monday;
            return CultureInfo.CurrentCulture
                .DateTimeFormat.FirstDayOfWeek;
        }

        private static bool IsWeekend(
            DayOfWeek day)
        {
            return day == DayOfWeek.Saturday ||
                day == DayOfWeek.Sunday;
        }

        private void LoadSettings()
        {
            if (_host == null) return;
            _style = NormalizeStyle(
                _host.GetSetting(
                    "style",
                    _style));
            _appearance = NormalizeAppearance(
                _host.GetSetting(
                    "appearance",
                    _appearance),
                IsSteampunk);
            _style = _appearance == "Steampunk"
                ? "Calendar.Steampunk"
                : "Calendar.Modern";
            _weekStart = NormalizeWeekStart(
                _host.GetSetting(
                    "weekStart",
                    _weekStart));
            _animationLevel = NormalizeAnimationLevel(
                _host.GetSetting(
                    "animationLevel",
                    _animationLevel));
        }

        private void SaveSettings()
        {
            if (_host == null) return;
            _host.SetSetting(
                "style",
                _style);
            _host.SetSetting(
                "appearance",
                _appearance);
            _host.SetSetting(
                "weekStart",
                _weekStart);
            _host.SetSetting(
                "animationLevel",
                _animationLevel);
        }

        private ICalendarStyleRenderer ActiveRenderer
        {
            get
            {
                return UsesIndustrialCalendarPresentation
                    ? IndustrialRenderer
                    : IsArtDeco
                    ? ArtDecoRenderer
                    : IsSteampunk
                    ? SteampunkRenderer
                    : ModernRenderer;
            }
        }

        private Size DesignSize()
        {
            return UsesIndustrialCalendarPresentation
                ? new Size(IndustrialWidth, IndustrialHeight)
                : IsArtDeco
                ? new Size(ArtDecoWidth, ArtDecoHeight)
                : new Size(BaseWidth, BaseHeight);
        }

        public Rectangle GetSnapBounds(Size renderedSurface)
        {
            SavedDesignerLayout saved = SavedDesignerLayout.Current(
                _appearance, "calendar");
            DesignerLayer background = saved == null || saved.Elements == null
                ? null : saved.Elements.Find(delegate(DesignerLayer item)
                {
                    return item != null && item.Visible &&
                        string.Equals(item.Id, "main-background",
                            StringComparison.OrdinalIgnoreCase);
                });
            if (background == null) return Rectangle.Empty;
            Size logical = DesignSize();
            return Rectangle.Round(new RectangleF(
                background.Bounds.X * renderedSurface.Width / logical.Width,
                background.Bounds.Y * renderedSurface.Height / logical.Height,
                background.Bounds.Width * renderedSurface.Width /
                    logical.Width,
                background.Bounds.Height * renderedSurface.Height /
                    logical.Height));
        }

        private void ApplyPreferredPresentation()
        {
            if (_host == null) return;
            if (IsArtDeco)
                _host.SetFixedCompositionSurface(
                    new Size(ArtDecoPreferredWidth,
                        ArtDecoPreferredHeight),
                    ArtDecoVisibleBounds());
            else if (UsesIndustrialCalendarPresentation)
                _host.SetFixedCompositionSurface(
                    new Size(IndustrialPreferredWidth,
                        IndustrialPreferredHeight),
                    IndustrialVisibleBounds());
            else
                _host.SetPreferredSize(DesignSize());
            _host.SetWindowShape(IsModern || IsVintage || IsArtDeco ||
                    UsesIndustrialCalendarPresentation
                ? WidgetWindowShape.AlphaRectangle
                : WidgetWindowShape.RoundedRectangle);
        }

        private Rectangle ArtDecoVisibleBounds()
        {
            SavedDesignerLayout saved = SavedDesignerLayout.Current(
                _appearance, "calendar");
            DesignerLayer background = saved == null || saved.Elements == null
                ? null : saved.Elements.Find(delegate(DesignerLayer item)
                {
                    return item != null && item.Visible &&
                        string.Equals(item.Id, "main-background",
                            StringComparison.OrdinalIgnoreCase);
                });
            if (background != null)
                return Rectangle.Round(new RectangleF(
                    background.Bounds.X * ArtDecoPreferredWidth /
                        ArtDecoWidth,
                    background.Bounds.Y * ArtDecoPreferredHeight /
                        ArtDecoHeight,
                    background.Bounds.Width * ArtDecoPreferredWidth /
                        ArtDecoWidth,
                    background.Bounds.Height * ArtDecoPreferredHeight /
                        ArtDecoHeight));
            Image skin = ThemeSkinCache.Get(ArtDecoSkinPath);
            Rectangle alpha = ThemeSkinCache.GetAlphaBounds(
                ArtDecoSkinPath);
            return Rectangle.Round(new RectangleF(
                alpha.X * ArtDecoPreferredWidth /
                    (float)skin.Width,
                alpha.Y * ArtDecoPreferredHeight /
                    (float)skin.Height,
                alpha.Width * ArtDecoPreferredWidth /
                    (float)skin.Width,
                alpha.Height * ArtDecoPreferredHeight /
                    (float)skin.Height));
        }

        private Rectangle IndustrialVisibleBounds()
        {
            IndustrialCalendarDesignerLayout layout =
                IndustrialCalendarDesignerLayout.Current(
                    DesignerLayoutFiles.FileName(_appearance, "calendar"));
            IndustrialCalendarDesignerElement background = layout == null
                ? null : layout.Find("main-background");
            if (background != null && background.Visible &&
                !layout.IsDeleted("main-background"))
                return Rectangle.Round(new RectangleF(
                    background.Bounds.X * IndustrialPreferredWidth /
                        IndustrialWidth,
                    background.Bounds.Y * IndustrialPreferredHeight /
                        IndustrialHeight,
                    background.Bounds.Width * IndustrialPreferredWidth /
                        IndustrialWidth,
                    background.Bounds.Height * IndustrialPreferredHeight /
                        IndustrialHeight));
            Image skin = ThemeSkinCache.Get(IndustrialPresentationSkinPath);
            Rectangle alpha = ThemeSkinCache.GetAlphaBounds(
                IndustrialPresentationSkinPath);
            return Rectangle.Round(new RectangleF(
                alpha.X * IndustrialPreferredWidth /
                    (float)skin.Width,
                alpha.Y * IndustrialPreferredHeight /
                    (float)skin.Height,
                alpha.Width * IndustrialPreferredWidth /
                    (float)skin.Width,
                alpha.Height * IndustrialPreferredHeight /
                    (float)skin.Height));
        }

        private void ApplyStyle(string value)
        {
            string normalized =
                NormalizeStyle(value);
            _style = normalized;
            if (_style == "Calendar.Steampunk")
                _appearance = "Steampunk";
            else if (_appearance == "Steampunk")
                _appearance = "Modern";
            SaveSettings();
            RequestInvalidate();
        }

        private void FallBackToModern()
        {
            _style = "Calendar.Modern";
            _appearance = "Modern";
            SaveSettings();
            _renderingSteampunk = false;
        }

        private void RenderModernStyle(
            Graphics graphics,
            Rectangle bounds)
        {
            _renderingSteampunk = false;
            DrawCalendar(graphics, bounds);
        }

        private void RenderSteampunkStyle(
            Graphics graphics,
            Rectangle bounds)
        {
            _renderingSteampunk = true;
            try
            {
                DrawCalendar(
                    graphics,
                    bounds);
            }
            finally
            {
                _renderingSteampunk = false;
            }
        }

        private void RenderArtDecoStyle(
            Graphics graphics,
            Rectangle bounds)
        {
            Image skin = ThemeSkinCache.Get(
                ArtDecoSkinPath);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(skin,
                new Rectangle(0, 0, ArtDecoWidth, ArtDecoHeight));
            if (_editableReferenceOnly) return;

            CalendarPalette palette = Palette();
            using (var monthFont = new Font("Georgia", 17F, FontStyle.Bold))
            using (var headerFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (var dayFont = new Font("Segoe UI", 10F))
            using (var primary = new SolidBrush(palette.PrimaryText))
            using (var secondary = new SolidBrush(palette.SecondaryText))
            {
                DrawCentered(graphics,
                    _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                    monthFont, primary, new RectangleF(131, 74, 260, 30));
                DrawButton(graphics, PreviousBounds(), "‹",
                    CalendarButton.Previous, palette, monthFont);
                DrawButton(graphics, NextBounds(), "›",
                    CalendarButton.Next, palette, monthFont);
                DrawButton(graphics, TodayBounds(), "Today",
                    CalendarButton.Today, palette, dayFont);

                DayOfWeek firstDay = FirstDayOfWeek();
                RectangleF grid = new RectangleF(47, 111, 428, 171);
                float cellWidth = grid.Width / 7F;
                float headerHeight = 24F;
                float cellHeight = (grid.Height - headerHeight) / 6F;
                DateTime first = new DateTime(
                    _displayMonth.Year, _displayMonth.Month, 1);
                int offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
                using (var line = new Pen(Color.FromArgb(80, palette.Border), 1F))
                {
                    for (int column = 0; column <= 7; column++)
                        graphics.DrawLine(line,
                            grid.Left + column * cellWidth, grid.Top,
                            grid.Left + column * cellWidth, grid.Bottom);
                    for (int row = 0; row <= 6; row++)
                        graphics.DrawLine(line,
                            grid.Left, grid.Top + headerHeight + row * cellHeight,
                            grid.Right, grid.Top + headerHeight + row * cellHeight);
                }
                for (int column = 0; column < 7; column++)
                {
                    DayOfWeek day = (DayOfWeek)(((int)firstDay + column) % 7);
                    using (var brush = new SolidBrush(
                        IsWeekend(day) ? palette.Weekend : palette.SecondaryText))
                        DrawCentered(graphics,
                            CultureInfo.CurrentCulture.DateTimeFormat
                                .GetAbbreviatedDayName(day),
                            headerFont, brush,
                            new RectangleF(grid.Left + column * cellWidth,
                                grid.Top, cellWidth, headerHeight));
                }
                bool current = IsViewingCurrentMonth();
                int days = DateTime.DaysInMonth(first.Year, first.Month);
                for (int number = 1; number <= days; number++)
                {
                    int index = offset + number - 1;
                    int row = index / 7;
                    int column = index % 7;
                    var date = new DateTime(first.Year, first.Month, number);
                    RectangleF cell = new RectangleF(
                        grid.Left + column * cellWidth,
                        grid.Top + headerHeight + row * cellHeight,
                        cellWidth, cellHeight);
                    bool today = current && number == _now.Day;
                    if (today)
                    {
                        using (var accent = new SolidBrush(palette.Accent))
                            graphics.FillEllipse(accent,
                                cell.X + 17, cell.Y + 3,
                                cell.Width - 34, cell.Height - 6);
                    }
                    using (var brush = new SolidBrush(today
                        ? palette.TodayText
                        : IsWeekend(date.DayOfWeek)
                            ? palette.Weekend
                            : palette.PrimaryText))
                        DrawCentered(graphics,
                            number.ToString(CultureInfo.CurrentCulture),
                            dayFont, brush, cell);
                }
                DrawCentered(graphics,
                    current
                        ? _now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)
                        : "Viewing " + _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                    dayFont, secondary, new RectangleF(62, 286, 315, 20));
            }
        }

        private void RenderIndustrialStyle(Graphics graphics, Rectangle bounds)
        {
            IndustrialCalendarDesignerLayout designerLayout =
                IndustrialCalendarDesignerLayout.Current(
                    DesignerLayoutFiles.FileName(_appearance, "calendar"));
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            IndustrialCalendarDesignerElement editableBackground =
                designerLayout == null ? null :
                designerLayout.Find("main-background");
            if (editableBackground != null &&
                !designerLayout.IsDeleted("main-background"))
            {
                if (editableBackground.Visible)
                    DesignerLayerPainter.DrawImageAlphaCropped(graphics,
                        editableBackground.ImagePath,
                        editableBackground.Bounds,
                        editableBackground.Opacity);
            }
            else if (designerLayout == null ||
                !designerLayout.IsDeleted("main-background"))
                DesignerLayerPainter.DrawImage(graphics,
                    designerLayout == null ? IndustrialPresentationSkinPath :
                        designerLayout.BackgroundImage,
                    new RectangleF(0, 0, IndustrialWidth,
                        IndustrialHeight), 1F);

            var palette = IsBotanical
                ? new CalendarPalette(
                    Color.FromArgb(245, 240, 226), Color.FromArgb(217, 221, 197),
                    Color.FromArgb(37, 63, 51), Color.FromArgb(99, 117, 91),
                    Color.FromArgb(210, 126, 139), Color.FromArgb(245, 240, 226),
                    Color.FromArgb(205, 105, 126), Color.FromArgb(139, 151, 116),
                    Color.FromArgb(37, 63, 51), Color.FromArgb(99, 117, 91))
                : new CalendarPalette(
                    Color.FromArgb(38, 39, 39), Color.FromArgb(27, 28, 28),
                    Color.FromArgb(244, 228, 192), Color.FromArgb(157, 161, 160),
                    Color.FromArgb(218, 151, 39), Color.FromArgb(35, 31, 24),
                    Color.FromArgb(224, 157, 39), Color.FromArgb(91, 94, 94),
                    Color.FromArgb(76, 61, 34), Color.FromArgb(91, 94, 94));
            Color footerText = IsBotanical
                ? Color.FromArgb(99, 117, 91)
                : Color.FromArgb(192, 192, 192);
            if (designerLayout != null)
            {
                RenderIndustrialDesignerContent(
                    graphics, designerLayout, palette);
                return;
            }
            using (var monthFont = new Font(
                UsesWoodlandNaturePresentation ? "Georgia" : "Segoe UI Semibold",
                UsesWoodlandNaturePresentation ? 21F : 18F, FontStyle.Bold))
            using (var headerFont = new Font(
                UsesWoodlandNaturePresentation ? "Georgia" : "Segoe UI Semibold",
                UsesWoodlandNaturePresentation ? 10.5F : 9F, FontStyle.Bold))
            using (var dayFont = new Font(
                UsesWoodlandNaturePresentation ? "Georgia" : "Segoe UI",
                UsesWoodlandNaturePresentation ? 11.5F : 10F))
            using (var primary = new SolidBrush(palette.PrimaryText))
            using (var secondary = new SolidBrush(palette.SecondaryText))
            using (var footer = new SolidBrush(footerText))
            {
                DrawCentered(graphics,
                    _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                    monthFont, primary, new RectangleF(135, 61, 270, 31));
                DrawButton(graphics, PreviousBounds(), "‹",
                    CalendarButton.Previous, palette, monthFont);
                DrawButton(graphics, NextBounds(), "›",
                    CalendarButton.Next, palette, monthFont);
                // Keep the full hit target, but do not draw the oversized pill
                // over the narrow lower portion of the Industrial frame.
                DrawCentered(graphics, "Today", dayFont, footer,
                    TodayBounds());

                DayOfWeek firstDay = FirstDayOfWeek();
                RectangleF grid = new RectangleF(50, 102, 440, 181);
                float cellWidth = grid.Width / 7F;
                float headerHeight = 25F;
                float cellHeight = (grid.Height - headerHeight) / 6F;
                DateTime first = new DateTime(
                    _displayMonth.Year, _displayMonth.Month, 1);
                int offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
                using (var line = new Pen(Color.FromArgb(75, 132, 134, 133), 1F))
                {
                    for (int column = 0; column <= 7; column++)
                        graphics.DrawLine(line, grid.Left + column * cellWidth,
                            grid.Top, grid.Left + column * cellWidth, grid.Bottom);
                    for (int row = 0; row <= 6; row++)
                        graphics.DrawLine(line, grid.Left,
                            grid.Top + headerHeight + row * cellHeight,
                            grid.Right, grid.Top + headerHeight + row * cellHeight);
                }
                for (int column = 0; column < 7; column++)
                {
                    DayOfWeek day = (DayOfWeek)(((int)firstDay + column) % 7);
                    using (var brush = new SolidBrush(IsWeekend(day)
                        ? palette.Weekend : palette.SecondaryText))
                        DrawCentered(graphics,
                            CultureInfo.CurrentCulture.DateTimeFormat
                                .GetAbbreviatedDayName(day), headerFont, brush,
                            new RectangleF(grid.Left + column * cellWidth,
                                grid.Top, cellWidth, headerHeight));
                }
                for (int index = 0; index < 42; index++)
                {
                    DateTime date = first.AddDays(index - offset);
                    int row = index / 7;
                    int column = index % 7;
                    RectangleF cell = new RectangleF(
                        grid.Left + column * cellWidth,
                        grid.Top + headerHeight + row * cellHeight,
                        cellWidth, cellHeight);
                    bool inMonth = date.Month == _displayMonth.Month;
                    bool today = date.Date == _now.Date;
                    if (today)
                    {
                        using (var accent = new SolidBrush(palette.Accent))
                            graphics.FillEllipse(accent, cell.X + 17, cell.Y + 3,
                                cell.Width - 34, cell.Height - 6);
                    }
                    Color color = today ? palette.TodayText
                        : !inMonth ? Color.FromArgb(105, 108, 108)
                        : IsWeekend(date.DayOfWeek) ? palette.Weekend
                        : palette.PrimaryText;
                    using (var brush = new SolidBrush(color))
                        DrawCentered(graphics,
                            date.Day.ToString(CultureInfo.CurrentCulture),
                            dayFont, brush, cell);
                }
                DrawCentered(graphics, IsViewingCurrentMonth()
                    ? _now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)
                    : "Viewing " + _displayMonth.ToString("MMMM yyyy",
                    CultureInfo.CurrentCulture), dayFont, footer,
                    new RectangleF(50, 300, 275, 18));
            }
        }

        public void RenderIndustrialDesignerReference(
            Graphics graphics, Rectangle bounds, string backgroundPath = null)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.ScaleTransform(
                    bounds.Width / (float)IndustrialWidth,
                    bounds.Height / (float)IndustrialHeight);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                DesignerLayerPainter.DrawImage(graphics, backgroundPath ??
                    (UsesIndustrialCalendarPresentation ? IndustrialPresentationSkinPath : IndustrialSkinPath),
                    new RectangleF(0, 0, IndustrialWidth, IndustrialHeight), 1F);
                DrawIndustrialCalendarGrid(graphics);

                var palette = new CalendarPalette(
                    Color.FromArgb(38, 39, 39), Color.FromArgb(27, 28, 28),
                    Color.FromArgb(244, 228, 192), Color.FromArgb(157, 161, 160),
                    Color.FromArgb(218, 151, 39), Color.FromArgb(35, 31, 24),
                    Color.FromArgb(224, 157, 39), Color.FromArgb(91, 94, 94),
                    Color.FromArgb(76, 61, 34), Color.FromArgb(91, 94, 94));
                DrawIndustrialButtonBackground(graphics,
                    new Rectangle(52, 70, 38, 30),
                    CalendarButton.Previous, palette);
                DrawIndustrialButtonBackground(graphics,
                    new Rectangle(450, 70, 38, 30),
                    CalendarButton.Next, palette);
                if (IsBotanical)
                    DrawBotanicalMainFrame(graphics,
                        new Rectangle(0, 0,
                            IndustrialWidth - 1,
                            IndustrialHeight - 1), 8F, 21F);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void RenderIndustrialDesignerContent(
            Graphics graphics, IndustrialCalendarDesignerLayout layout, CalendarPalette palette)
        {
            DrawIndustrialCalendarGrid(graphics, layout);
            Rectangle previous = IndustrialDesignerBounds("previous-button", Rectangle.Empty);
            Rectangle next = IndustrialDesignerBounds("next-button", Rectangle.Empty);
            if (!previous.IsEmpty) DrawIndustrialButtonBackground(graphics, previous,
                CalendarButton.Previous, palette);
            if (!next.IsEmpty) DrawIndustrialButtonBackground(graphics, next,
                CalendarButton.Next, palette);
            foreach (IndustrialCalendarDesignerElement element in layout.Elements)
            {
                if (element == null || !element.Visible ||
                    element.Surface != 0 ||
                    layout.IsDeleted(element.Id)) continue;
                if (string.Equals(element.Id, "main-background",
                    StringComparison.OrdinalIgnoreCase)) continue;
                Color? dynamicColor = null;
                int index;
                if (element.Kind == 0 && element.Binding == "Calendar: Date" &&
                    TryCalendarIndex(element.Id, "date-", 42, out index))
                {
                    DateTime date = CalendarCellDate(index);
                    if (date.Date == _now.Date)
                    {
                        RectangleF cell = element.Bounds;
                        using (var accent = new SolidBrush(palette.Accent))
                            graphics.FillEllipse(accent, cell.X + Math.Max(2F, cell.Width * .27F),
                                cell.Y + 3F, Math.Max(8F, cell.Width * .46F),
                                Math.Max(8F, cell.Height - 6F));
                    }
                    // Keep automatic today/weekend/off-month colours only for
                    // uncustomised date ink. A chosen colour is never discarded.
                    if (element.ColorArgb == palette.PrimaryText.ToArgb())
                        dynamicColor = date.Date == _now.Date ? palette.TodayText :
                            date.Month != _displayMonth.Month ? Color.FromArgb(105, 108, 108) :
                            IsWeekend(date.DayOfWeek) ? palette.Weekend : palette.PrimaryText;
                }
                else if (element.Kind == 0 && element.Binding == "Calendar: Weekday" &&
                    TryCalendarIndex(element.Id, "weekday-", 7, out index))
                {
                    Color original = index == 0 || index == 6 ? palette.Weekend : palette.SecondaryText;
                    if (element.ColorArgb == original.ToArgb())
                    {
                        DayOfWeek day = (DayOfWeek)(((int)FirstDayOfWeek() + index) % 7);
                        dynamicColor = IsWeekend(day) ? palette.Weekend : palette.SecondaryText;
                    }
                }
                DesignerLayerPainter.Draw(graphics, element, ResolveCalendarDesignerText, dynamicColor);
            }
        }

        private static bool TryCalendarIndex(string id, string prefix, int count, out int index)
        {
            index = 0;
            return id != null && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id.Substring(prefix.Length), out index) && index >= 0 && index < count;
        }

        private DateTime CalendarCellDate(int index)
        {
            DateTime first = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
            int offset = ((int)first.DayOfWeek - (int)FirstDayOfWeek() + 7) % 7;
            return first.AddDays(index - offset);
        }

        private string ResolveCalendarDesignerText(DesignerLayer element)
        {
            string binding = (element.Binding ?? "").ToLowerInvariant();
            int index;
            switch (binding)
            {
                case "calendar: month and year":
                    return _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                case "calendar: previous month": return "\u2039";
                case "calendar: next month": return "\u203a";
                case "calendar: today": return "Today";
                case "calendar: full date":
                    return IsViewingCurrentMonth() ? _now.ToString("dddd, MMMM d, yyyy",
                        CultureInfo.CurrentCulture) : "Viewing " +
                        _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                case "calendar: weekday":
                    TryCalendarIndex(element.Id, "weekday-", 7, out index);
                    return CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(
                        (DayOfWeek)(((int)FirstDayOfWeek() + index) % 7));
                case "calendar: date":
                    return (TryCalendarIndex(element.Id, "date-", 42, out index)
                        ? CalendarCellDate(index) : _now).Day.ToString(CultureInfo.CurrentCulture);
                default: return element.Text;
            }
        }

        private static void DrawIndustrialCalendarGrid(Graphics graphics)
        {
            RectangleF grid = new RectangleF(50, 102, 440, 181);
            DrawIndustrialCalendarGrid(graphics, grid, 127F);
        }

        private static void DrawIndustrialCalendarGrid(Graphics graphics,
            IndustrialCalendarDesignerLayout layout)
        {
            RectangleF grid = RectangleF.Empty;
            float dateTop = float.MaxValue;
            if (layout != null && layout.Elements != null)
            {
                foreach (IndustrialCalendarDesignerElement element in layout.Elements)
                {
                    if (element == null || !element.Visible ||
                        element.Surface != 0 || element.Kind != 0) continue;
                    bool weekday = element.Id != null && element.Id.StartsWith(
                        "weekday-", StringComparison.OrdinalIgnoreCase);
                    bool date = element.Id != null && element.Id.StartsWith(
                        "date-", StringComparison.OrdinalIgnoreCase);
                    if (!weekday && !date) continue;
                    grid = grid.IsEmpty ? element.Bounds : RectangleF.Union(
                        grid, element.Bounds);
                    if (date && element.Bounds.Top < dateTop)
                        dateTop = element.Bounds.Top;
                }
            }
            if (grid.IsEmpty || dateTop == float.MaxValue)
            {
                DrawIndustrialCalendarGrid(graphics);
                return;
            }
            DrawIndustrialCalendarGrid(graphics, grid, dateTop);
        }

        private static void DrawIndustrialCalendarGrid(Graphics graphics,
            RectangleF grid, float dateTop)
        {
            float cellWidth = grid.Width / 7F;
            float headerHeight = Math.Max(1F, dateTop - grid.Top);
            float cellHeight = (grid.Height - headerHeight) / 6F;
            using (var line = new Pen(
                Color.FromArgb(75, 132, 134, 133), 1F))
            {
                for (int column = 0; column <= 7; column++)
                    graphics.DrawLine(line,
                        grid.Left + column * cellWidth, grid.Top,
                        grid.Left + column * cellWidth, grid.Bottom);
                for (int row = 0; row <= 6; row++)
                    graphics.DrawLine(line, grid.Left,
                        grid.Top + headerHeight + row * cellHeight,
                        grid.Right,
                        grid.Top + headerHeight + row * cellHeight);
            }
        }

        private void DrawIndustrialButtonBackground(
            Graphics graphics, Rectangle bounds, CalendarButton button,
            CalendarPalette palette)
        {
            Color fill = button == _pressed
                ? palette.ButtonPressed
                : button == _hovered
                    ? palette.ButtonHover : palette.Button;
            using (GraphicsPath path = RoundedRectangle(bounds, 9))
            using (var background = new SolidBrush(fill))
            using (var border = new Pen(palette.Border))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
            }
        }



        private void RequestInvalidate()
        {
            if (_invalidate != null)
                _invalidate();
            else if (_host != null)
                _host.Invalidate();
        }

        private CalendarPalette Palette()
        {
            if (IsModern)
                return new CalendarPalette(
                    ModernThemePainter.SurfaceTop,
                    ModernThemePainter.SurfaceBottom,
                    ModernThemePainter.PrimaryText,
                    ModernThemePainter.SecondaryText,
                    ModernThemePainter.Accent,
                    Color.White,
                    Color.FromArgb(242, 112, 143),
                    ModernThemePainter.Border,
                    Color.FromArgb(48, 62, 82),
                    Color.FromArgb(28, 39, 55));
            if (IsVintage)
                return new CalendarPalette(
                    VintageThemePainter.PaperLight,
                    VintageThemePainter.PaperDark,
                    VintageThemePainter.Ink,
                    VintageThemePainter.MutedInk,
                    VintageThemePainter.Brass,
                    Color.FromArgb(250, 239, 211),
                    VintageThemePainter.Weekend,
                    VintageThemePainter.DarkBrass,
                    Color.FromArgb(78, VintageThemePainter.Brass),
                    Color.FromArgb(110,
                        VintageThemePainter.DarkBrass));
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_appearance);
            return new CalendarPalette(
                theme.SurfaceTop,
                theme.SurfaceBottom,
                theme.PrimaryText,
                theme.SecondaryText,
                theme.Accent,
                EmilyDeskThemeCatalog.BestTextOn(theme.Accent),
                theme.Weekend,
                Blend(theme.SurfaceTop, theme.Border, .45F),
                Blend(theme.SurfaceTop, theme.Accent, .25F),
                Blend(theme.SurfaceBottom, theme.Border, .5F));
        }

        private CalendarPalette SteampunkPalette()
        {
            return Palette();
        }

        private static Color Blend(Color first, Color second, float amount)
        {
            float value = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                (int)(first.R + (second.R - first.R) * value),
                (int)(first.G + (second.G - first.G) * value),
                (int)(first.B + (second.B - first.B) * value));
        }

        private static void DrawDecorativeGear(
            Graphics graphics,
            float centerX,
            float centerY,
            float radius,
            float angleDegrees,
            CalendarPalette palette)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(centerX, centerY);
                graphics.RotateTransform(angleDegrees);
                using (var teeth = new SolidBrush(
                    Color.FromArgb(185, palette.Accent)))
                using (var body = new SolidBrush(
                    Color.FromArgb(220, palette.SurfaceTop)))
                using (var hub = new SolidBrush(
                    palette.SurfaceBottom))
                using (var pen = new Pen(palette.Border, 1F))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        graphics.RotateTransform(45F);
                        graphics.FillRectangle(
                            teeth, -2F, -radius - 4F, 4F, 7F);
                    }
                    graphics.FillEllipse(
                        body, -radius, -radius, radius * 2F, radius * 2F);
                    graphics.DrawEllipse(
                        pen, -radius, -radius, radius * 2F, radius * 2F);
                    graphics.FillEllipse(
                        hub, -radius * .35F, -radius * .35F,
                        radius * .7F, radius * .7F);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static string NormalizeAnimationLevel(
            string value)
        {
            if (string.Equals(
                value, "Off", StringComparison.OrdinalIgnoreCase))
                return "Off";
            if (string.Equals(
                value, "Enhanced", StringComparison.OrdinalIgnoreCase))
                return "Enhanced";
            return "Minimal";
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
                Location = new Point(18, top + 5),
                Size = new Size(150, 24)
            });
            var combo = new ComboBox
            {
                DropDownStyle =
                    ComboBoxStyle.DropDownList,
                Location = new Point(174, top),
                Size = new Size(228, 28)
            };
            combo.Items.AddRange(items);
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0)
                combo.SelectedIndex = 0;
            parent.Controls.Add(combo);
            return combo;
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
                Location = new Point(174, top),
                Size = new Size(90, 27)
            };
            parent.Controls.Add(number);
            return number;
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
            Rectangle bounds,
            int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
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
            return path;
        }

        private static void DrawBotanicalMainFrame(
            Graphics graphics,
            Rectangle bounds,
            float thickness,
            float radius)
        {
            RectangleF outer = RectangleF.Inflate(bounds,
                -thickness / 2F, -thickness / 2F);
            using (GraphicsPath outerPath = RoundedRectangle(
                Rectangle.Round(outer), (int)Math.Round(radius)))
            using (var sage = new Pen(
                Color.FromArgb(174, 180, 139), thickness))
                graphics.DrawPath(sage, outerPath);

            RectangleF darkLine = RectangleF.Inflate(
                outer, -thickness / 2F - 1F,
                -thickness / 2F - 1F);
            using (GraphicsPath darkPath = RoundedRectangle(
                Rectangle.Round(darkLine),
                Math.Max(4, (int)Math.Round(radius - thickness))))
            using (var darkSage = new Pen(
                Color.FromArgb(99, 117, 91), 2F))
                graphics.DrawPath(darkSage, darkPath);

            RectangleF highlight = RectangleF.Inflate(
                darkLine, -3F, -3F);
            using (GraphicsPath highlightPath = RoundedRectangle(
                Rectangle.Round(highlight),
                Math.Max(3, (int)Math.Round(radius - thickness - 3F))))
            using (var ivory = new Pen(
                Color.FromArgb(245, 243, 228), 1.5F))
                graphics.DrawPath(ivory, highlightPath);
        }

        private static string NormalizeStyle(
            string value)
        {
            return string.Equals(
                value,
                "Calendar.Steampunk",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    value,
                    "Steampunk",
                    StringComparison.OrdinalIgnoreCase)
                ? "Calendar.Steampunk"
                : "Calendar.Modern";
        }

        private static string NormalizeAppearance(
            string value,
            bool steampunk)
        {
            string normalized = EmilyDeskThemeCatalog.Normalize(value);
            return steampunk ? "Steampunk" : normalized;
        }

        private interface ICalendarStyleRenderer
        {
            void Render(
                NativeCalendarWidget widget,
                Graphics graphics,
                Rectangle bounds);
        }

        private sealed class ModernCalendarStyleRenderer :
            ICalendarStyleRenderer
        {
            public void Render(
                NativeCalendarWidget widget,
                Graphics graphics,
                Rectangle bounds)
            {
                widget.RenderModernStyle(
                    graphics,
                    bounds);
            }
        }

        private sealed class SteampunkCalendarStyleRenderer :
            ICalendarStyleRenderer
        {
            public void Render(
                NativeCalendarWidget widget,
                Graphics graphics,
                Rectangle bounds)
            {
                widget.RenderSteampunkStyle(
                    graphics,
                    bounds);
            }
        }

        private sealed class ArtDecoCalendarStyleRenderer :
            ICalendarStyleRenderer
        {
            public void Render(
                NativeCalendarWidget widget,
                Graphics graphics,
                Rectangle bounds)
            {
                widget.RenderArtDecoStyle(graphics, bounds);
            }
        }

        private sealed class IndustrialCalendarStyleRenderer :
            ICalendarStyleRenderer
        {
            public void Render(NativeCalendarWidget widget,
                Graphics graphics, Rectangle bounds)
            {
                widget.RenderIndustrialStyle(graphics, bounds);
            }
        }

        private static string NormalizeWeekStart(
            string value)
        {
            if (string.Equals(
                value,
                "Sunday",
                StringComparison.OrdinalIgnoreCase))
                return "Sunday";
            if (string.Equals(
                value,
                "Monday",
                StringComparison.OrdinalIgnoreCase))
                return "Monday";
            return "System";
        }

        private enum CalendarButton
        {
            None,
            Previous,
            Today,
            Next
        }

        private enum CalendarNavigation
        {
            Previous,
            Today,
            Next
        }

        private sealed class CalendarPalette
        {
            public CalendarPalette(
                Color surfaceTop,
                Color surfaceBottom,
                Color primaryText,
                Color secondaryText,
                Color accent,
                Color todayText,
                Color weekend,
                Color border,
                Color buttonHover,
                Color buttonPressed)
            {
                SurfaceTop = surfaceTop;
                SurfaceBottom = surfaceBottom;
                PrimaryText = primaryText;
                SecondaryText = secondaryText;
                Accent = accent;
                TodayText = todayText;
                Weekend = weekend;
                Border = border;
                Button = Color.FromArgb(
                    32,
                    primaryText);
                ButtonHover = buttonHover;
                ButtonPressed = buttonPressed;
            }

            public Color SurfaceTop;
            public Color SurfaceBottom;
            public Color PrimaryText;
            public Color SecondaryText;
            public Color Accent;
            public Color TodayText;
            public Color Weekend;
            public Color Border;
            public Color Button;
            public Color ButtonHover;
            public Color ButtonPressed;
        }
    }
}
