using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using XWidgetReborn.WidgetSdk;

namespace EmilyDesk.Widgets.Calculator
{
    public sealed class CalculatorWidget :
        IWidget,
        IOfficialWidget,
        IWidgetPointerInput,
        IWidgetDesignerProvider,
        IWidgetDesignerBackgroundLayerProvider,
        IWidgetThemeProvider,
        IRuntimeAwareWidget
    {
#if EMBER_GLOW_WIDGET
        private const int BaseWidth = 420;
        private const int BaseHeight = 350;
        private const int DisplayX = 34;
        private const int DisplayWidth = 342;
#elif ART_DECO_WIDGET
        private const int BaseWidth = 300;
        private const int BaseHeight = 420;
        private const int DisplayX = 46;
        private const int DisplayWidth = 208;
#else
        private const int BaseWidth = 300;
        private const int BaseHeight = 390;
        private const int DisplayX = 46;
        private const int DisplayWidth = 208;
#endif
#if ART_DECO_WIDGET
        private const string WidgetId = "utility.art-deco-calculator";
        private const string WidgetName = "Art Deco Calculator";
        private const string WidgetVersion = "1.0.4";
        private const string ThemeName = "Art Deco";
        private const string TitleText = "ART DECO";
        private const string SkinFileName = "art-deco-calculator-skin.png";
        private const int PreferredWidth = 270;
        private const int PreferredHeight = 378;
        private const int TitlePlateY = 48;
        private const int TitleTextY = 54;
        private const int SubtitleTextY = 70;
        private const int DisplayY = 84;
        private const int DisplayHeight = 52;
        private const int DisplayTextY = 88;
        private const int DisplayTextHeight = 40;
        private const float DisplayRivetY = 92F;
#elif EMBER_GLOW_WIDGET
        private const string WidgetId = "utility.ember-glow-calculator";
        private const string WidgetName = "Ember Glow Calculator";
        private const string WidgetVersion = "1.0.12";
        private const string ThemeName = "Ember Glow";
        private const string TitleText = "EMBER GLOW";
        private const string SkinFileName = "ember-glow-calculator-skin.png";
        private const string LcdFileName = "ember-glow-calculator-lcd.png";
        private const int PreferredWidth = BaseWidth;
        private const int PreferredHeight = BaseHeight;
        private const int TitlePlateY = 0;
        private const int TitleTextY = 43;
        private const int SubtitleTextY = 62;
        private const int DisplayY = 56;
        private const int DisplayHeight = 42;
        private const int DisplayTextY = 59;
        private const int DisplayTextHeight = 34;
        private const float DisplayRivetY = 96F;
#elif INDUSTRIAL_WIDGET
        private const string WidgetId = "utility.industrial-calculator";
        private const string WidgetName = "Industrial Calculator";
        private const string WidgetVersion = "1.0.2";
        private const string ThemeName = "Industrial";
        private const string TitleText = "INDUSTRIAL";
        private const string SkinFileName = "industrial-calculator-skin.png";
        private const int PreferredWidth = 270;
        private const int PreferredHeight = 351;
        private const int TitlePlateY = 34;
        private const int TitleTextY = 34;
        private const int SubtitleTextY = 52;
        private const int DisplayY = 76;
        private const int DisplayHeight = 60;
        private const int DisplayTextY = 84;
        private const int DisplayTextHeight = 44;
        private const float DisplayRivetY = 84F;
#elif WOODLAND_WIDGET
        private const string WidgetId = "utility.woodland-calculator";
        private const string WidgetName = "Woodland Nature Calculator";
        private const string WidgetVersion = "1.0.1";
        private const string ThemeName = "Woodland Nature";
        private const string TitleText = "WOODLAND";
        private const string SkinFileName = "woodland-calculator-skin.png";
        private const int PreferredWidth = 270;
        private const int PreferredHeight = 351;
        private const int TitlePlateY = 34;
        private const int TitleTextY = 34;
        private const int SubtitleTextY = 52;
        private const int DisplayY = 76;
        private const int DisplayHeight = 60;
        private const int DisplayTextY = 84;
        private const int DisplayTextHeight = 44;
        private const float DisplayRivetY = 84F;
#elif BOTANICAL_WIDGET
        private const string WidgetId = "utility.botanical-calculator";
        private const string WidgetName = "Botanical Nature Calculator";
        private const string WidgetVersion = "1.0.0";
        private const string ThemeName = "Botanical Nature";
        private const string TitleText = "BOTANICAL";
        private const string SkinFileName = "botanical-calculator-skin.png";
        private const int PreferredWidth = 270;
        private const int PreferredHeight = 351;
        private const int TitlePlateY = 34;
        private const int TitleTextY = 34;
        private const int SubtitleTextY = 52;
        private const int DisplayY = 76;
        private const int DisplayHeight = 60;
        private const int DisplayTextY = 84;
        private const int DisplayTextHeight = 44;
        private const float DisplayRivetY = 84F;
#else
        private const string WidgetId = "utility.calculator";
        private const string WidgetName = "Steampunk Calculator";
        private const string WidgetVersion = "1.1.4";
        private const string ThemeName = "Steampunk";
        private const string TitleText = "STEAMPUNK";
        private const string SkinFileName = "steampunk-calculator-skin.png";
        private const int PreferredWidth = BaseWidth;
        private const int PreferredHeight = BaseHeight;
        private const int TitlePlateY = 34;
        private const int TitleTextY = 34;
        private const int SubtitleTextY = 52;
        private const int DisplayY = 76;
        private const int DisplayHeight = 60;
        private const int DisplayTextY = 84;
        private const int DisplayTextHeight = 44;
        private const float DisplayRivetY = 84F;
#endif
        private static readonly string[,] Buttons =
        {
            { "C", "+/-", "%", "/" },
            { "7", "8", "9", "*" },
            { "4", "5", "6", "-" },
            { "1", "2", "3", "+" },
            { "0", ".", "=", "=" }
        };

        private Action _invalidate;
        private IWidgetHostContext _host;
        private IRuntimeContext _runtime;
        private Size _lastRenderSize = new Size(BaseWidth, BaseHeight);
        private string _display = "0";
        private decimal _stored;
        private string _operation = string.Empty;
        private bool _replaceDisplay = true;
        private string _pressed = string.Empty;
        private bool _paused;
        private Image _calculatorSkin;
        private bool _calculatorSkinAttempted;

        public string Id { get { return WidgetId; } }
        public string Name { get { return WidgetName; } }
        public string Description
        {
#if ART_DECO_WIDGET
            get { return "A black-lacquer, gold, and teal Art Deco desktop calculator imported separately from EmilyDesk."; }
#elif EMBER_GLOW_WIDGET
            get { return "A dark copper and amber Ember Glow desktop calculator imported separately from EmilyDesk."; }
#elif INDUSTRIAL_WIDGET
            get { return "A brushed-steel and charcoal desktop calculator imported separately from EmilyDesk."; }
#elif WOODLAND_WIDGET
            get { return "A dark-green, hardwood, and antique-brass desktop calculator imported separately from EmilyDesk."; }
#elif BOTANICAL_WIDGET
            get { return "An ivory, sage, and dusty-rose botanical desktop calculator imported separately from EmilyDesk."; }
#else
            get { return "A brass, copper, and mahogany desktop calculator imported separately from EmilyDesk."; }
#endif
        }
        public string Version { get { return WidgetVersion; } }
        public Size DefaultSize
        { get { return new Size(PreferredWidth, PreferredHeight); } }
        public Point DefaultLocation { get { return new Point(80, 110); } }
        public WidgetUpdateRate UpdateRate { get { return WidgetUpdateRate.Second; } }
        public IEnumerable<string> Themes
        {
            get { return OptionalWidgetThemeCatalog.AvailableThemes(WidgetId); }
        }
        public string Theme
        {
            get { return ThemeName; }
            set
            {
                string target = OptionalWidgetThemeCatalog.TargetWidgetId(
                    WidgetId, value);
                if (_runtime == null || string.IsNullOrWhiteSpace(target) ||
                    string.Equals(target, WidgetId,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                Point position = _runtime.Settings.GetPosition(DefaultLocation);
                _runtime.Events.Publish("widget.switch-theme",
                    WidgetId + "\t" + target + "\t" +
                    position.X.ToString(CultureInfo.InvariantCulture) + "\t" +
                    position.Y.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void AttachHost(IWidgetHostContext host)
        {
            _host = host;
            if (_host == null) return;
            _host.SetPreferredSize(DefaultSize);
            _host.SetWindowShape(WidgetWindowShape.AlphaRectangle);
        }

        public void AttachRuntime(IRuntimeContext runtime)
        {
            if (_runtime != null)
                _runtime.Events.Published -= DesignerSaved;
            _runtime = runtime;
            if (_runtime != null)
                _runtime.Events.Published += DesignerSaved;
        }

        private void DesignerSaved(object sender, WidgetRuntimeEventArgs e)
        {
            if (e != null && e.Topic == "designer.saved")
                RequestInvalidate();
        }

        public void Start(Action invalidate)
        {
            _invalidate = invalidate;
            RequestInvalidate();
        }

        public void Tick(DateTime now) { }
        public void Pause() { _paused = true; }
        public void Resume() { _paused = false; RequestInvalidate(); }

        public void Render(Graphics graphics, Rectangle bounds)
        {
            _lastRenderSize = bounds.Size;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            GraphicsState state = graphics.Save();
            float scale = Math.Min(
                bounds.Width / (float)BaseWidth,
                bounds.Height / (float)BaseHeight);
            graphics.TranslateTransform(
                bounds.Left + (bounds.Width - BaseWidth * scale) / 2F,
                bounds.Top + (bounds.Height - BaseHeight * scale) / 2F);
            graphics.ScaleTransform(scale, scale);
            try
            {
                SavedDesignerLayout layout = SavedDesignerLayout.Current(
                    DesignerTheme, DesignerKind);
#if EMBER_GLOW_WIDGET
                if (layout != null && layout.CanvasWidth == 300 &&
                    layout.CanvasHeight == 420)
                    layout = MigrateLegacyEmberGlowLayout(layout);
#endif
                if (layout == null)
                    DrawCalculator(graphics, true);
                else
                    DrawSavedLayout(graphics, layout);
                if (_paused)
                    using (var paused = new SolidBrush(
                        Color.FromArgb(155, 20, 12, 9)))
                        graphics.FillRectangle(paused, 1, 1,
                            BaseWidth - 2, BaseHeight - 2);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawCalculator(Graphics graphics, bool drawText)
        {
            DrawCalculatorBackground(graphics, null);
#if EMBER_GLOW_WIDGET
            DrawLcd(graphics, LcdFileName,
                new Rectangle(DisplayX, DisplayY,
                    DisplayWidth, DisplayHeight), 1F);
#endif
            DrawCalculatorChrome(graphics, drawText);
        }

#if EMBER_GLOW_WIDGET
        private void DrawLcd(Graphics graphics, string imagePath,
            Rectangle bounds, float opacity)
        {
            if (DesignerLayerPainter.DrawImage(graphics,
                ResolvePackageAsset(imagePath), bounds, opacity)) return;
            using (GraphicsPath display = RoundedRectangle(bounds, 14))
            using (var brush = new SolidBrush(Color.FromArgb(20, 11, 8)))
                graphics.FillPath(brush, display);
        }
#endif

        private void DrawCalculatorBackground(
            Graphics graphics, string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath) &&
                DesignerLayerPainter.DrawImage(graphics,
                    ResolvePackageAsset(imagePath),
                    new RectangleF(0, 0, BaseWidth, BaseHeight), 1F))
                return;
            Image skin = CalculatorSkin;
            if (skin != null)
                graphics.DrawImage(skin, new Rectangle(0, 0, BaseWidth, BaseHeight));
            else
                DrawFallbackShell(graphics);
        }

        private void DrawCalculatorChrome(
            Graphics graphics, bool drawText)
        {
#if !ART_DECO_WIDGET && !EMBER_GLOW_WIDGET
            using (GraphicsPath titlePlate = RoundedRectangle(
                new Rectangle(70, TitlePlateY, 160, 34), 8))
            using (var titleBrush = new LinearGradientBrush(
                new Rectangle(70, TitlePlateY, 160, 34),
#if ART_DECO_WIDGET
                Color.FromArgb(255, 235, 164),
                Color.FromArgb(143, 91, 25), 90F))
            using (var titleBorder = new Pen(Color.FromArgb(244, 198, 92), 1.5F))
#elif INDUSTRIAL_WIDGET
                Color.FromArgb(226, 229, 229),
                Color.FromArgb(91, 95, 97), 90F))
            using (var titleBorder = new Pen(Color.FromArgb(36, 39, 41), 1.5F))
#elif WOODLAND_WIDGET
                Color.FromArgb(142, 92, 51),
                Color.FromArgb(55, 31, 19), 90F))
            using (var titleBorder = new Pen(Color.FromArgb(193, 151, 83), 1.5F))
#elif BOTANICAL_WIDGET
                Color.FromArgb(249, 231, 226),
                Color.FromArgb(217, 146, 142), 90F))
            using (var titleBorder = new Pen(Color.FromArgb(102, 116, 85), 1.5F))
#else
                Color.FromArgb(250, 205, 111),
                Color.FromArgb(143, 79, 30), 90F))
            using (var titleBorder = new Pen(Color.FromArgb(245, 198, 103), 1.5F))
#endif
            {
                titleBrush.InterpolationColors = new ColorBlend
                {
#if ART_DECO_WIDGET
                    Colors = new[]
                    {
                        Color.FromArgb(111, 68, 18),
                        Color.FromArgb(255, 238, 174),
                        Color.FromArgb(205, 145, 52),
                        Color.FromArgb(96, 56, 16)
                    },
#elif INDUSTRIAL_WIDGET
                    Colors = new[]
                    {
                        Color.FromArgb(74, 78, 80),
                        Color.FromArgb(232, 234, 233),
                        Color.FromArgb(142, 146, 147),
                        Color.FromArgb(57, 61, 63)
                    },
#elif WOODLAND_WIDGET
                    Colors = new[]
                    {
                        Color.FromArgb(48, 29, 18),
                        Color.FromArgb(155, 105, 58),
                        Color.FromArgb(92, 52, 29),
                        Color.FromArgb(39, 24, 16)
                    },
#elif BOTANICAL_WIDGET
                    Colors = new[]
                    {
                        Color.FromArgb(233, 188, 183),
                        Color.FromArgb(255, 244, 240),
                        Color.FromArgb(223, 164, 160),
                        Color.FromArgb(207, 132, 130)
                    },
#else
                    Colors = new[]
                    {
                        Color.FromArgb(116, 59, 23),
                        Color.FromArgb(255, 226, 145),
                        Color.FromArgb(209, 137, 54),
                        Color.FromArgb(111, 53, 23)
                    },
#endif
                    Positions = new[] { 0F, .28F, .62F, 1F }
                };
                graphics.FillPath(titleBrush, titlePlate);
                graphics.DrawPath(titleBorder, titlePlate);
                using (var shine = new Pen(
#if ART_DECO_WIDGET
                    Color.FromArgb(185, 255, 250, 214), 1F))
#elif INDUSTRIAL_WIDGET
                    Color.FromArgb(170, 255, 255, 255), 1F))
#elif WOODLAND_WIDGET
                    Color.FromArgb(135, 255, 231, 180), 1F))
#elif BOTANICAL_WIDGET
                    Color.FromArgb(185, 255, 255, 255), 1F))
#else
                    Color.FromArgb(170, 255, 246, 202), 1F))
#endif
                    graphics.DrawLine(shine, 82,
                        TitlePlateY + 6, 218, TitlePlateY + 6);
            }
#endif
#if !EMBER_GLOW_WIDGET
            if (drawText)
            using (var titleFont = new Font("Georgia", 12F, FontStyle.Bold))
                DrawText(graphics, TitleText, titleFont,
                    TitleInk,
                    new Rectangle(76, TitleTextY, 148, 21),
                    ContentAlignment.MiddleCenter);
            if (drawText)
            using (var subtitleFont = new Font("Segoe UI", 6.5F,
                FontStyle.Bold))
                DrawText(graphics, "CALCULATOR", subtitleFont,
                    TitleInk,
                    new Rectangle(76, SubtitleTextY, 148, 12),
                    ContentAlignment.MiddleCenter);
#endif

#if !EMBER_GLOW_WIDGET
            using (GraphicsPath display = RoundedRectangle(
                new Rectangle(DisplayX, DisplayY, DisplayWidth, DisplayHeight), 10))
            using (var displayBrush = new LinearGradientBrush(
                new Rectangle(DisplayX, DisplayY, DisplayWidth, DisplayHeight),
#if INDUSTRIAL_WIDGET
                Color.FromArgb(37, 40, 41),
                Color.FromArgb(9, 11, 12), 90F))
            using (var displayGlow = new Pen(Color.FromArgb(55, 226, 157, 43), 5F))
            using (var displayBorder = new Pen(Color.FromArgb(128, 133, 135), 2F))
#elif WOODLAND_WIDGET
                Color.FromArgb(27, 47, 36),
                Color.FromArgb(8, 20, 15), 90F))
            using (var displayGlow = new Pen(Color.FromArgb(48, 220, 171, 72), 5F))
            using (var displayBorder = new Pen(Color.FromArgb(151, 125, 69), 2F))
#elif BOTANICAL_WIDGET
                Color.FromArgb(255, 253, 248),
                Color.FromArgb(240, 235, 224), 90F))
            using (var displayGlow = new Pen(Color.FromArgb(42, 217, 146, 142), 5F))
            using (var displayBorder = new Pen(Color.FromArgb(102, 116, 85), 2F))
#else
                Color.FromArgb(42, 27, 18),
                Color.FromArgb(16, 12, 10), 90F))
            using (var displayGlow = new Pen(Color.FromArgb(70, 255, 155, 43), 6F))
            using (var displayBorder = new Pen(Color.FromArgb(225, 159, 53), 2F))
#endif
            {
                graphics.DrawPath(displayGlow, display);
                graphics.FillPath(displayBrush, display);
                graphics.DrawPath(displayBorder, display);
            }
#endif
#if !EMBER_GLOW_WIDGET
            DrawRivet(graphics, 54F, DisplayRivetY);
            DrawRivet(graphics, 246F, DisplayRivetY);
#endif
            if (drawText)
            using (var displayFont = new Font("Consolas", 22F, FontStyle.Bold))
                DrawText(graphics, DisplayForDrawing(), displayFont,
                    DisplayInk,
                    new Rectangle(DisplayX + 12, DisplayTextY,
                        DisplayWidth - 24,
                        DisplayTextHeight),
                    ContentAlignment.MiddleRight);

            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 4; column++)
                {
                    if (row == 4 && column == 3) continue;
                    string label = Buttons[row, column];
                    Rectangle button = ButtonBounds(row, column);
                    bool accent = label == "/" || label == "*" ||
                        label == "-" || label == "+" || label == "=";
                    Color top = accent
#if EMBER_GLOW_WIDGET
                        ? Color.FromArgb(54, 35, 28)
                        : Color.FromArgb(39, 25, 20);
                    Color bottom = accent
                        ? Color.FromArgb(26, 16, 13)
                        : Color.FromArgb(20, 12, 10);
#elif INDUSTRIAL_WIDGET
                        ? Color.FromArgb(111, 78, 34)
                        : Color.FromArgb(76, 80, 82);
                    Color bottom = accent
                        ? Color.FromArgb(48, 37, 24)
                        : Color.FromArgb(27, 30, 31);
#elif WOODLAND_WIDGET
                        ? Color.FromArgb(123, 88, 38)
                        : Color.FromArgb(57, 76, 56);
                    Color bottom = accent
                        ? Color.FromArgb(51, 43, 24)
                        : Color.FromArgb(18, 34, 25);
#elif BOTANICAL_WIDGET
                        ? Color.FromArgb(220, 161, 157)
                        : Color.FromArgb(247, 231, 226);
                    Color bottom = accent
                        ? Color.FromArgb(184, 104, 105)
                        : Color.FromArgb(232, 213, 207);
#else
                        ? Color.FromArgb(157, 84, 33)
                        : Color.FromArgb(72, 49, 37);
                    Color bottom = accent
                        ? Color.FromArgb(76, 37, 21)
                        : Color.FromArgb(30, 20, 17);
#endif
                    if (_pressed == label)
                    {
#if EMBER_GLOW_WIDGET
                        top = Color.FromArgb(36, 24, 20);
                        bottom = Color.FromArgb(19, 12, 10);
#elif INDUSTRIAL_WIDGET
                        top = Color.FromArgb(61, 64, 65);
                        bottom = Color.FromArgb(17, 19, 20);
#elif WOODLAND_WIDGET
                        top = Color.FromArgb(47, 64, 46);
                        bottom = Color.FromArgb(11, 24, 17);
#elif BOTANICAL_WIDGET
                        top = Color.FromArgb(203, 146, 142);
                        bottom = Color.FromArgb(169, 96, 98);
#else
                        top = Color.FromArgb(112, 57, 28);
                        bottom = Color.FromArgb(45, 25, 18);
#endif
                    }
                    using (GraphicsPath path = RoundedRectangle(button, 8))
                    using (var brush = new LinearGradientBrush(button, top, bottom, 90F))
                    using (var pen = new Pen(accent
#if EMBER_GLOW_WIDGET
                        ? Color.FromArgb(97, 79, 65)
                        : Color.FromArgb(75, 61, 52), 1.0F))
#elif INDUSTRIAL_WIDGET
                        ? Color.FromArgb(218, 154, 55)
                        : Color.FromArgb(139, 144, 146), 1.4F))
#elif WOODLAND_WIDGET
                        ? Color.FromArgb(218, 171, 70)
                        : Color.FromArgb(132, 111, 66), 1.4F))
#elif BOTANICAL_WIDGET
                        ? Color.FromArgb(169, 95, 97)
                        : Color.FromArgb(102, 116, 85), 1.4F))
#else
                        ? Color.FromArgb(235, 169, 67)
                        : Color.FromArgb(163, 111, 54), 1.4F))
#endif
                    {
                        graphics.FillPath(brush, path);
                        graphics.DrawPath(pen, path);
                    }
#if !EMBER_GLOW_WIDGET
                    DrawRivet(graphics, button.Left + 7F,
                        button.Top + 7F, 2.2F);
#endif
                    if (drawText)
                    using (var font = new Font("Georgia", 13F,
                        accent ? FontStyle.Bold : FontStyle.Regular))
                        DrawText(graphics, label == "*" ? "×" :
                            label == "/" ? "÷" : label,
                            font, ButtonInk, button,
                            ContentAlignment.MiddleCenter);
                }
        }

        public bool PointerDown(Point location, WidgetPointerButton button)
        {
            if (button != WidgetPointerButton.Left || _paused) return false;
            string hit = HitTest(ToBase(location));
            if (hit.Length == 0) return false;
            _pressed = hit;
            RequestInvalidate();
            return true;
        }

        public void PointerMove(Point location) { }

        public void PointerUp(Point location, WidgetPointerButton button)
        {
            string hit = HitTest(ToBase(location));
            string pressed = _pressed;
            _pressed = string.Empty;
            if (button == WidgetPointerButton.Left && hit == pressed)
                Press(pressed);
            RequestInvalidate();
        }

        public void PointerLeave()
        {
            if (_pressed.Length == 0) return;
            _pressed = string.Empty;
            RequestInvalidate();
        }

        public void ShowSettings()
        {
            System.Windows.Forms.MessageBox.Show(
                "Use the buttons on the calculator. Drag it from the title area above the display.",
                WidgetName,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }

        public void ResetSettings()
        {
            _display = "0";
            _stored = 0M;
            _operation = string.Empty;
            _replaceDisplay = true;
            RequestInvalidate();
        }

        public IEnumerable<WidgetMenuCommand> GetMenuCommands()
        {
            return new[]
            {
                new WidgetMenuCommand(
                    "Edit in Designer", false, OpenDesigner)
            };
        }

        public string DesignerKind { get { return "calculator"; } }
        public string DesignerTheme { get { return ThemeName; } }

        public SavedDesignerLayout CreateDesignerLayout()
        {
            SavedDesignerLayout layout = NewDesignerLayout(
                WidgetName, BaseWidth, BaseHeight);
            AddBackground(layout);
#if EMBER_GLOW_WIDGET
            layout.Elements.Add(new DesignerLayer
            {
                Id = "lcd-window", Name = "LCD Screen",
                Binding = "Calculator: LCD Window",
                Kind = 1, Surface = 0,
                X = DisplayX, Y = DisplayY,
                Width = DisplayWidth, Height = DisplayHeight,
                Visible = true, Opacity = 1F, Scale = 1F,
                ImagePath = LcdFileName
            });
#endif
#if !EMBER_GLOW_WIDGET
            AddText(layout, "title", "Title", TitleText, "None",
                76, TitleTextY, 148, 21, "Georgia", 12F, true,
                TitleInk, 1);
            AddText(layout, "subtitle", "Subtitle", "CALCULATOR", "None",
                76, SubtitleTextY, 148, 12, "Segoe UI", 6.5F, true,
                TitleInk, 1);
#endif
            AddText(layout, "display", "Display", "0", "Calculator: Display",
                DisplayX + 12, DisplayTextY, DisplayWidth - 24,
                DisplayTextHeight,
                "Consolas", 22F, true,
                DisplayInk, 2);
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 4; column++)
                {
                    if (row == 4 && column == 3) continue;
                    string value = Buttons[row, column];
                    Rectangle button = ButtonBounds(row, column);
                    AddText(layout, "button-" + row + "-" + column,
                        "Button " + value,
                        value == "*" ? "×" : value == "/" ? "÷" : value,
                        "Calculator: Button " + value,
                        button.X, button.Y, button.Width, button.Height,
                        "Georgia", 13F,
                        value == "/" || value == "*" || value == "-" ||
                            value == "+" || value == "=",
                        ButtonInk, 1);
                }
            return layout;
        }

        public void RenderDesignerBackground(Graphics graphics,
            Rectangle bounds)
        {
            RenderDesignerBackgroundLayer(
                graphics, bounds, SkinFileName, 1F);
        }

        public void RenderDesignerBackgroundLayer(
            Graphics graphics,
            Rectangle bounds,
            string imagePath,
            float opacity)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (var surface = new Bitmap(BaseWidth, BaseHeight,
                PixelFormat.Format32bppArgb))
            using (Graphics layer = Graphics.FromImage(surface))
            {
                layer.SmoothingMode = SmoothingMode.AntiAlias;
                layer.CompositingQuality = CompositingQuality.HighQuality;
                layer.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                layer.PixelOffsetMode = PixelOffsetMode.HighQuality;
                layer.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                DrawCalculatorBackground(layer, imagePath);
                DrawCalculatorChrome(layer, false);
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix
                    {
                        Matrix33 = Math.Max(0F, Math.Min(1F, opacity))
                    });
                    graphics.DrawImage(surface, bounds, 0, 0,
                        surface.Width, surface.Height,
                        GraphicsUnit.Pixel, attributes);
                }
            }
        }

        private void DrawSavedLayout(
            Graphics graphics, SavedDesignerLayout layout)
        {
            DesignerLayer background = null;
            if (layout != null && layout.Elements != null)
                background = layout.Elements.Find(delegate(DesignerLayer item)
                {
                    return IsBackgroundLayer(item);
                });
            if (background == null)
            {
                DrawCalculatorBackground(graphics, null);
                DrawCalculatorChrome(graphics, false);
            }
            else if (background.Visible)
                RenderDesignerBackgroundLayer(graphics,
                    Rectangle.Round(background.Bounds),
                    background.ImagePath, background.Opacity);
            if (layout == null || layout.Elements == null) return;
#if EMBER_GLOW_WIDGET
            if (!layout.Elements.Exists(delegate(DesignerLayer item)
                { return item != null && string.Equals(item.Id,
                    "lcd-window", StringComparison.OrdinalIgnoreCase); }))
                DrawLcd(graphics, LcdFileName,
                    new Rectangle(DisplayX, DisplayY,
                        DisplayWidth, DisplayHeight), 1F);
#endif
            foreach (DesignerLayer layer in layout.Elements)
                if (!IsBackgroundLayer(layer)
#if EMBER_GLOW_WIDGET
                    && !string.Equals(layer.Id, "title",
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(layer.Id, "subtitle",
                        StringComparison.OrdinalIgnoreCase)
#endif
                    )
                {
#if EMBER_GLOW_WIDGET
                    if (string.Equals(layer.Id, "lcd-window",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (layer.Visible)
                            DrawLcd(graphics, layer.ImagePath,
                                Rectangle.Round(layer.Bounds),
                                layer.Opacity);
                        continue;
                    }
#endif
                    DesignerLayerPainter.Draw(
                        graphics, layer, ResolveDesignerText);
                }
        }

#if EMBER_GLOW_WIDGET
        private SavedDesignerLayout MigrateLegacyEmberGlowLayout(
            SavedDesignerLayout oldLayout)
        {
            SavedDesignerLayout current = CreateDesignerLayout();
            if (oldLayout.Elements == null) return current;
            foreach (DesignerLayer updated in current.Elements)
            {
                if (updated.Kind != 0) continue;
                DesignerLayer previous = oldLayout.Elements.Find(
                    delegate(DesignerLayer item)
                    {
                        return item != null && string.Equals(item.Id,
                            updated.Id, StringComparison.OrdinalIgnoreCase);
                    });
                if (previous == null) continue;
                updated.FontName = previous.FontName;
                updated.FontFile = previous.FontFile;
                updated.FontSize = previous.FontSize;
                updated.Bold = previous.Bold;
                updated.Italic = previous.Italic;
                updated.ColorArgb = previous.ColorArgb;
            }
            return current;
        }
#endif

        private static bool IsBackgroundLayer(DesignerLayer layer)
        {
            return layer != null && string.Equals(layer.Id,
                "main-background", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolvePackageAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (Path.IsPathRooted(path)) return path;
            string folder = Path.GetDirectoryName(
                GetType().Assembly.Location) ?? string.Empty;
            string candidate = Path.Combine(folder,
                path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(candidate) ? candidate : path;
        }

        public string ResolveDesignerText(DesignerLayer layer)
        {
            if (layer == null) return string.Empty;
            if (string.Equals(layer.Binding, "Calculator: Display",
                StringComparison.OrdinalIgnoreCase))
                return DisplayForDrawing();
            if (!string.IsNullOrEmpty(layer.Binding) &&
                layer.Binding.StartsWith("Calculator: Button ",
                    StringComparison.OrdinalIgnoreCase))
            {
                string value = layer.Binding.Substring(
                    "Calculator: Button ".Length);
                return value == "*" ? "×" : value == "/" ? "÷" : value;
            }
            return layer.Text;
        }

        private void OpenDesigner()
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "EmilyDesk.Designer.exe");
                if (!File.Exists(path)) throw new FileNotFoundException(
                    "EmilyDesk Designer is not installed.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--optional-widget-assembly \"" +
                        GetType().Assembly.Location +
                        "\" --type \"" +
                        GetType().FullName + "\"",
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
            catch (Exception error)
            {
                System.Windows.Forms.MessageBox.Show(error.Message,
                    "EmilyDesk Designer",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private static SavedDesignerLayout NewDesignerLayout(
            string name, int width, int height)
        {
            return new SavedDesignerLayout
            {
                Name = name,
                CanvasWidth = width,
                CanvasHeight = height,
                EditableLayerVersion = 1,
                BackgroundLayerVersion = 1,
                Elements = new List<DesignerLayer>(),
                DeletedElementIds = new List<string>()
            };
        }

        private static void AddBackground(SavedDesignerLayout layout)
        {
            layout.Elements.Add(new DesignerLayer
            {
                Id = "main-background",
                Name = "Widget Background",
                Binding = "Layout: Main Background",
                Kind = 1,
                Surface = 0,
                X = 0F,
                Y = 0F,
                Width = BaseWidth,
                Height = BaseHeight,
                Visible = true,
                Opacity = 1F,
                Scale = 1F,
                ImagePath = SkinFileName
            });
        }

        private static void AddText(SavedDesignerLayout layout,
            string id, string name, string text, string binding,
            float x, float y, float width, float height,
            string font, float size, bool bold, Color color, int alignment)
        {
            layout.Elements.Add(new DesignerLayer
            {
                Id = id, Name = name, Text = text, Binding = binding,
                Kind = 0, Surface = 0, X = x, Y = y,
                Width = width, Height = height, Visible = true,
                Opacity = 1F, Scale = 1F, FontName = font,
                FontSize = size, Bold = bold,
                ColorArgb = color.ToArgb(), Alignment = alignment,
                WordWrap = false, Trimming = 3
            });
        }

        private void Press(string key)
        {
            if (key == "C")
            {
                ResetSettings();
                return;
            }
            if (key == "+/-")
            {
                decimal value = CurrentValue();
                _display = Format(-value);
                return;
            }
            if (key == "%")
            {
                _display = Format(CurrentValue() / 100M);
                _replaceDisplay = true;
                return;
            }
            if (key == "=" || key == "+" || key == "-" ||
                key == "*" || key == "/")
            {
                if (_operation.Length > 0 && !_replaceDisplay)
                    Calculate();
                if (key == "=")
                    _operation = string.Empty;
                else
                {
                    _stored = CurrentValue();
                    _operation = key;
                }
                _replaceDisplay = true;
                return;
            }
            if (key == ".")
            {
                if (_replaceDisplay)
                {
                    _display = "0.";
                    _replaceDisplay = false;
                }
                else if (_display.IndexOf('.') < 0)
                    _display += ".";
                return;
            }
            if (char.IsDigit(key[0]))
            {
                if (_replaceDisplay || _display == "0")
                    _display = key;
                else if (_display.Length < 14)
                    _display += key;
                _replaceDisplay = false;
            }
        }

        private void Calculate()
        {
            decimal right = CurrentValue();
            try
            {
                decimal value = _operation == "+" ? _stored + right :
                    _operation == "-" ? _stored - right :
                    _operation == "*" ? _stored * right :
                    _operation == "/" && right != 0M ? _stored / right :
                    0M;
                if (_operation == "/" && right == 0M)
                    _display = "Cannot divide";
                else
                    _display = Format(value);
            }
            catch
            {
                _display = "Error";
            }
        }

        private decimal CurrentValue()
        {
            decimal value;
            return decimal.TryParse(
                _display,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value) ? value : 0M;
        }

        private static string Format(decimal value)
        {
            return value.ToString("0.############", CultureInfo.InvariantCulture);
        }

        private string DisplayForDrawing()
        {
            return _display.Length <= 16
                ? _display
                : _display.Substring(0, 16);
        }

        private static Rectangle ButtonBounds(int row, int column)
        {
#if EMBER_GLOW_WIDGET
            int x = 34 + column * 88;
            int y = 104 + row * 37;
            int width = 78;
            if (row == 4 && column == 2) width = 166;
            return new Rectangle(x, y, width, 33);
#else
            int x = 46 + column * 53;
            int y = 145 + row * 42;
            int width = 46;
            if (row == 4 && column == 2) width = 99;
            return new Rectangle(x, y, width, 34);
#endif
        }

        private Image CalculatorSkin
        {
            get
            {
                if (_calculatorSkinAttempted) return _calculatorSkin;
                _calculatorSkinAttempted = true;
                try
                {
                    string assemblyFolder = Path.GetDirectoryName(
                        GetType().Assembly.Location);
                    string skinPath = Path.Combine(
                        assemblyFolder ?? string.Empty,
                        SkinFileName);
                    if (File.Exists(skinPath))
                        using (Image source = Image.FromFile(skinPath))
                            _calculatorSkin = new Bitmap(source);
                }
                catch
                {
                    _calculatorSkin = null;
                }
                return _calculatorSkin;
            }
        }


        private static void DrawFallbackShell(Graphics graphics)
        {
            using (GraphicsPath shell = RoundedRectangle(
                new Rectangle(2, 2, BaseWidth - 4, BaseHeight - 4), 20))
            using (var shellBrush = new LinearGradientBrush(
                new Rectangle(0, 0, BaseWidth, BaseHeight),
#if EMBER_GLOW_WIDGET
                Color.FromArgb(79, 40, 22),
                Color.FromArgb(18, 9, 7),
                90F))
            using (var outerBorder = new Pen(Color.FromArgb(244, 124, 24), 3F))
            using (var innerBorder = new Pen(Color.FromArgb(104, 42, 13), 2F))
#elif INDUSTRIAL_WIDGET
                Color.FromArgb(109, 113, 115),
                Color.FromArgb(25, 28, 29),
                90F))
            using (var outerBorder = new Pen(Color.FromArgb(202, 205, 205), 3F))
            using (var innerBorder = new Pen(Color.FromArgb(45, 48, 50), 2F))
#elif WOODLAND_WIDGET
                Color.FromArgb(55, 83, 60),
                Color.FromArgb(12, 27, 20),
                90F))
            using (var outerBorder = new Pen(Color.FromArgb(144, 104, 58), 3F))
            using (var innerBorder = new Pen(Color.FromArgb(42, 63, 44), 2F))
#elif BOTANICAL_WIDGET
                Color.FromArgb(255, 250, 240),
                Color.FromArgb(225, 218, 199),
                90F))
            using (var outerBorder = new Pen(Color.FromArgb(152, 167, 128), 3F))
            using (var innerBorder = new Pen(Color.FromArgb(102, 116, 85), 2F))
#else
                Color.FromArgb(74, 40, 25),
                Color.FromArgb(22, 13, 11),
                90F))
            using (var outerBorder = new Pen(Color.FromArgb(218, 165, 70), 3F))
            using (var innerBorder = new Pen(Color.FromArgb(101, 49, 27), 2F))
#endif
            {
                graphics.FillPath(shellBrush, shell);
                graphics.DrawPath(outerBorder, shell);
                using (GraphicsPath inner = RoundedRectangle(
                    new Rectangle(8, 8, BaseWidth - 16, BaseHeight - 16), 15))
                    graphics.DrawPath(innerBorder, inner);
            }
        }

        private static void DrawRivet(
            Graphics graphics,
            float centerX,
            float centerY,
            float radius = 3F)
        {
            using (var brush = new SolidBrush(RivetFace))
            using (var pen = new Pen(RivetEdge, 1F))
            {
                graphics.FillEllipse(brush, centerX - radius,
                    centerY - radius, radius * 2F, radius * 2F);
                graphics.DrawEllipse(pen, centerX - radius,
                    centerY - radius, radius * 2F, radius * 2F);
                graphics.DrawLine(pen, centerX - radius * 0.45F,
                    centerY, centerX + radius * 0.45F, centerY);
            }
        }

        private static Color TitleInk
        {
            get
            {
#if ART_DECO_WIDGET
                return Color.FromArgb(236, 218, 177);
#elif EMBER_GLOW_WIDGET
                return Color.FromArgb(246, 223, 189);
#elif INDUSTRIAL_WIDGET
                return Color.FromArgb(31, 34, 35);
#elif WOODLAND_WIDGET
                return Color.FromArgb(245, 226, 184);
#elif BOTANICAL_WIDGET
                return Color.FromArgb(66, 82, 59);
#else
                return Color.FromArgb(73, 38, 18);
#endif
            }
        }

        private static Color DisplayInk
        {
            get
            {
#if EMBER_GLOW_WIDGET
                return Color.FromArgb(255, 155, 40);
#elif INDUSTRIAL_WIDGET
                return Color.FromArgb(245, 177, 63);
#elif WOODLAND_WIDGET
                return Color.FromArgb(235, 188, 82);
#elif BOTANICAL_WIDGET
                return Color.FromArgb(66, 82, 59);
#else
                return Color.FromArgb(255, 205, 112);
#endif
            }
        }

        private static Color ButtonInk
        {
            get
            {
#if EMBER_GLOW_WIDGET
                return Color.FromArgb(246, 223, 189);
#elif INDUSTRIAL_WIDGET
                return Color.FromArgb(239, 232, 211);
#elif WOODLAND_WIDGET
                return Color.FromArgb(245, 238, 210);
#elif BOTANICAL_WIDGET
                return Color.FromArgb(66, 82, 59);
#else
                return Color.FromArgb(255, 230, 178);
#endif
            }
        }

        private static Color RivetFace
        {
            get
            {
#if INDUSTRIAL_WIDGET
                return Color.FromArgb(170, 174, 175);
#elif WOODLAND_WIDGET
                return Color.FromArgb(157, 121, 68);
#elif BOTANICAL_WIDGET
                return Color.FromArgb(152, 167, 128);
#else
                return Color.FromArgb(211, 151, 65);
#endif
            }
        }

        private static Color RivetEdge
        {
            get
            {
#if INDUSTRIAL_WIDGET
                return Color.FromArgb(35, 38, 39);
#elif WOODLAND_WIDGET
                return Color.FromArgb(42, 28, 17);
#elif BOTANICAL_WIDGET
                return Color.FromArgb(66, 82, 59);
#else
                return Color.FromArgb(61, 31, 18);
#endif
            }
        }

        private static string HitTest(Point point)
        {
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 4; column++)
                {
                    if (row == 4 && column == 3) continue;
                    if (ButtonBounds(row, column).Contains(point))
                        return Buttons[row, column];
                }
            return string.Empty;
        }

        private Point ToBase(Point location)
        {
            return new Point(
                _lastRenderSize.Width <= 0 ? location.X :
                    (int)Math.Round(location.X * BaseWidth /
                        (double)_lastRenderSize.Width),
                _lastRenderSize.Height <= 0 ? location.Y :
                    (int)Math.Round(location.Y * BaseHeight /
                        (double)_lastRenderSize.Height));
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawText(
            Graphics graphics,
            string text,
            Font font,
            Color color,
            Rectangle bounds,
            ContentAlignment alignment)
        {
            using (var format = new StringFormat())
            using (var brush = new SolidBrush(color))
            {
                format.LineAlignment = StringAlignment.Center;
                format.Alignment = alignment == ContentAlignment.MiddleRight
                    ? StringAlignment.Far
                    : alignment == ContentAlignment.MiddleCenter
                        ? StringAlignment.Center
                        : StringAlignment.Near;
                format.Trimming = StringTrimming.EllipsisCharacter;
                graphics.DrawString(text, font, brush, bounds, format);
            }
        }

        private void RequestInvalidate()
        {
            if (_invalidate != null) _invalidate();
            if (_host != null) _host.Invalidate();
        }

        public void Dispose()
        {
            if (_runtime != null)
                _runtime.Events.Published -= DesignerSaved;
            _runtime = null;
            if (_calculatorSkin != null)
            {
                _calculatorSkin.Dispose();
                _calculatorSkin = null;
            }
            _invalidate = null;
            _host = null;
        }
    }
}
