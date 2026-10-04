using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XWidgetReborn.Runtime.Core;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Runtime
{
    internal static class ArtDecoWeatherLaunchVerification
    {
        public static void Run(IRuntimeLoggingService logging)
        {
            RunTheme(logging, "Art Deco");
        }

        public static void RunTheme(IRuntimeLoggingService logging, string theme)
        {
            if (!string.Equals(theme, "Art Deco", StringComparison.Ordinal) &&
                !string.Equals(theme, "Industrial", StringComparison.Ordinal))
                throw new ArgumentException("Unsupported Weather verification theme.", "theme");
            foreach (float scale in new[] { .5F, .75F, 1F, 1.25F, 1.5F, 1.75F, 2F })
                VerifyScale(logging, scale, theme);
            VerifySmoothAnimation(logging, theme);
        }

        private static void VerifySmoothAnimation(
            IRuntimeLoggingService logging, string theme)
        {
            IWidget widget = (IWidget)Assembly.LoadFrom(ResolveWeatherAssembly())
                .CreateInstance("XWidgetReborn.Widgets.Weather.NativeWeatherWidget");
            WidgetWindow window = null;
            try
            {
                window = new WidgetWindow(widget,
                    new VerificationSettings(1F, "Smooth", theme), logging);
                window.Text = VerificationTitle(theme);
                window.Show();
                Application.DoEvents();
                AssertSelectedTheme(widget, theme);
                int presentations = window.LayeredPresentationCount;
                int resizes = window.AnchoredCompositeResizeCount;
                int frames = ArtDecoAnimationFrames(widget);
                Rectangle fixedBounds = window.Bounds;
                ToggleForecast(widget, window);
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(700);
                while (DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(5);
                }
                Application.DoEvents();
                int presentationDelta = window.LayeredPresentationCount - presentations;
                int resizeDelta = window.AnchoredCompositeResizeCount - resizes;
                int frameDelta = ArtDecoAnimationFrames(widget) - frames;
                if (window.Bounds != fixedBounds || resizeDelta != 0 ||
                    presentationDelta < 3 || presentationDelta > frameDelta + 1)
                    throw new InvalidOperationException(
                        "Art Deco Weather SlidePanel changed the parent window while opening; " +
                        "resizes=" + resizeDelta + "; presentations=" +
                        presentationDelta + ".");
                presentations = window.LayeredPresentationCount;
                resizes = window.AnchoredCompositeResizeCount;
                frames = ArtDecoAnimationFrames(widget);
                ToggleForecast(widget, window);
                deadline = DateTime.UtcNow.AddMilliseconds(700);
                while (DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(5);
                }
                Application.DoEvents();
                presentationDelta = window.LayeredPresentationCount - presentations;
                resizeDelta = window.AnchoredCompositeResizeCount - resizes;
                frameDelta = ArtDecoAnimationFrames(widget) - frames;
                if (window.Bounds != fixedBounds || resizeDelta != 0 ||
                    presentationDelta < 3 || presentationDelta > frameDelta + 1)
                    throw new InvalidOperationException(
                        "Art Deco Weather SlidePanel changed the parent window while closing; " +
                        "resizes=" + resizeDelta + "; presentations=" + presentationDelta + ".");
                logging.Information(
                    "PASS: " + theme + " Weather smooth open and close animations used " +
                    "one layered presentation per anchored resize frame.");
            }
            finally
            {
                if (window != null) window.Dispose();
                widget.Dispose();
            }
        }

        private static void VerifyScale(
            IRuntimeLoggingService logging, float scale, string theme)
        {
            string assemblyPath = ResolveWeatherAssembly();
            IWidget widget = (IWidget)Assembly.LoadFrom(assemblyPath).CreateInstance(
                "XWidgetReborn.Widgets.Weather.NativeWeatherWidget");
            if (widget == null)
                throw new InvalidOperationException("Native Weather could not be created.");

            WidgetWindow window = null;
            var settings = new VerificationSettings(scale, "Instant", theme);
            try
            {
                window = new WidgetWindow(widget, settings, logging);
                window.Text = VerificationTitle(theme);
                window.Show();
                Application.DoEvents();
                AssertSelectedTheme(widget, theme);
                if (string.Equals(theme, "Industrial", StringComparison.Ordinal))
                    VerifyIndustrialGeometry(widget, window, scale, logging);
                if (!window.Visible || !window.IsHandleCreated ||
                    window.ClientSize.Width < 2 || window.ClientSize.Height < 2)
                    throw new InvalidOperationException(
                        "Native Weather did not create a visible layered surface.");
                using (Graphics graphics = window.CreateGraphics())
                    if (graphics == null)
                        throw new InvalidOperationException(
                        "Native Weather surface did not expose a graphics context.");

                int collapsedWidth = window.Width;
                int expectedCollapsedWidth = (int)Math.Round(1081F * scale);
                int expectedHeight = (int)Math.Round(494F * scale);
                if (scale <= 1.25F &&
                    (collapsedWidth != expectedCollapsedWidth ||
                    window.Height != expectedHeight))
                    throw new InvalidOperationException(
                        theme + " Weather collapsed size is incorrect at " +
                        scale.ToString("0.0") + "x scale.");
                Point collapsedMove = new Point(window.Left + 23, window.Top + 17);
                window.Location = collapsedMove;
                Application.DoEvents();
                if (window.Location != collapsedMove)
                    throw new InvalidOperationException(
                        "Collapsed Art Deco Weather did not move as one surface.");
                Rectangle stationaryBounds = window.Bounds;
                ToggleForecast(widget, window);
                Application.DoEvents();
                if (window.Bounds != stationaryBounds)
                    throw new InvalidOperationException(
                        theme + " Weather resized or moved its parent while expanding.");

                Point moved = new Point(window.Left + 37, window.Top + 29);
                window.Location = moved;
                Application.DoEvents();
                if (window.Location != moved)
                    throw new InvalidOperationException(
                        "Expanded Art Deco Weather did not move as one surface.");

                Rectangle expandedMovedBounds = window.Bounds;
                ToggleForecast(widget, window);
                Application.DoEvents();
                if (window.Bounds != expandedMovedBounds)
                    throw new InvalidOperationException(
                        "Art Deco Weather resized or moved its parent while collapsing.");

                ToggleForecast(widget, window);
                Application.DoEvents();
                Point expectedSurfacePosition = window.Location;
                Point expectedRestartPosition = ExpectedPersistedPosition(window);
                window.Close();
                Application.DoEvents();
                if (settings.LastPosition != expectedRestartPosition)
                    throw new InvalidOperationException(
                        "Expanded Art Deco Weather persisted the panel edge instead of " +
                        "the main widget position.");
                window.Dispose();
                window = new WidgetWindow(widget, settings, logging);
                window.Show();
                Application.DoEvents();
                if (window.Width != collapsedWidth ||
                    window.Location != expectedSurfacePosition)
                    throw new InvalidOperationException(
                        theme + " Weather did not restart collapsed at the main widget position.");
                logging.Information(
                    "PASS: " + theme + " native.weather created a visible surface and " +
                    "completed fixed-surface left expand/move/collapse/restart at " +
                    scale.ToString("0.0") + "x; " +
                    "ClientSize=" + window.ClientSize.Width + "x" +
                    window.ClientSize.Height + ".");
            }
            finally
            {
                if (window != null) window.Dispose();
                widget.Dispose();
            }
        }

        private static void AssertSelectedTheme(IWidget widget, string theme)
        {
            var provider = widget as IWidgetThemeProvider;
            if (provider == null || !string.Equals(
                provider.Theme, theme, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Production WidgetWindow selected " +
                    (provider == null ? "<no theme>" : provider.Theme) +
                    " instead of " + theme + ".");
        }

        private static string VerificationTitle(string theme)
        {
            return string.Equals(theme, "Industrial", StringComparison.Ordinal)
                ? "Industrial Weather verification"
                : "Art Deco Weather regression verification";
        }

        private static RectangleF ActiveParentBounds(IWidget widget, string theme)
        {
            string fieldName = string.Equals(theme, "Industrial", StringComparison.Ordinal)
                ? "_industrialSlidePanel" : "_artDecoSlidePanel";
            FieldInfo field = widget.GetType().GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return ((SlidePanel)field.GetValue(widget)).ParentBounds;
        }

        private static Point ExpectedPersistedPosition(WidgetWindow window)
        {
            Type type = typeof(WidgetWindow);
            Rectangle parent = (Rectangle)type.GetField(
                "_compositionParentBounds",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            float runtimeScale = (float)type.GetField(
                "_scale", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            float dpiScale = (float)type.GetField(
                "_dpiScale", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            return new Point(
                window.Left + (int)Math.Round(parent.X * runtimeScale * dpiScale),
                window.Top + (int)Math.Round(parent.Y * runtimeScale * dpiScale));
        }

        private static void VerifyIndustrialGeometry(
            IWidget widget, WidgetWindow window, float scale,
            IRuntimeLoggingService logging)
        {
            ApplyIndustrialVisualFixture(widget);
            using (var layoutProbe = new Bitmap(
                window.ClientSize.Width, window.ClientSize.Height))
            using (Graphics probeGraphics = Graphics.FromImage(layoutProbe))
                widget.Render(probeGraphics, new Rectangle(
                    Point.Empty, window.ClientSize));
            FieldInfo field = widget.GetType().GetField(
                "_industrialSlidePanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var slide = (SlidePanel)field.GetValue(widget);
            MethodInfo method = widget.GetType().GetMethod(
                "IndustrialMainVisibleBounds",
                BindingFlags.NonPublic | BindingFlags.Static);
            RectangleF main = (RectangleF)method.Invoke(
                null, new object[] { slide.ParentBounds });
            float sx = window.ClientSize.Width / 1441F;
            float sy = window.ClientSize.Height / 659F;
            SizeF mainPixels = new SizeF(main.Width * sx, main.Height * sy);
            SizeF panelPixels = new SizeF(
                slide.PanelBounds.Width * sx, slide.PanelBounds.Height * sy);
            if (Math.Abs(panelPixels.Width - mainPixels.Width * .80F) > 1F ||
                Math.Abs(panelPixels.Height - mainPixels.Height * .80F) > 1F)
                throw new InvalidOperationException(
                    "Industrial visible SlidePanel ratio is not 0.80 at " + scale + "x.");
            string typographyPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "industrial-weather-typography-measurements.txt");
            if (Math.Abs(scale - .75F) < .001F)
            {
                string typography = VerifyIndustrialTypography(
                    widget, scale, sx, sy);
                File.WriteAllText(typographyPath, typography + "\r\n");
            }
            else if (Math.Abs(scale - 1F) < .001F)
            {
                string typography = VerifyIndustrialTypography(
                    widget, scale, sx, sy);
                File.AppendAllText(typographyPath, typography + "\r\n");
            }
            if (Math.Abs(scale - 1F) < .001F)
            {
                string measurement = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "MainVisible={0:0.0}x{1:0.0}px\r\nPanelVisible={2:0.0}x{3:0.0}px\r\n" +
                    "Ratios={4:0.0000}x{5:0.0000}\r\nPanelDestination={6:0.0}x{7:0.0}px",
                    mainPixels.Width, mainPixels.Height,
                    panelPixels.Width, panelPixels.Height,
                    panelPixels.Width / mainPixels.Width,
                    panelPixels.Height / mainPixels.Height,
                    slide.PanelBounds.Width * sx,
                    slide.PanelBounds.Height * sy);
                File.WriteAllText(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "industrial-weather-measurements.txt"), measurement);
                slide.SetProgress(1F);
                try
                {
                    using (var preview = new Bitmap(
                        window.ClientSize.Width, window.ClientSize.Height))
                    using (Graphics graphics = Graphics.FromImage(preview))
                    {
                        graphics.Clear(Color.Transparent);
                        widget.Render(graphics, new Rectangle(
                            Point.Empty, window.ClientSize));
                        preview.Save(Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "industrial-weather-runtime-preview.png"));
                    }
                }
                finally { slide.SetProgress(0F); }
                logging.Information(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "MEASURE: Industrial 100% main visible={0:0.0}x{1:0.0}px; " +
                    "panel visible={2:0.0}x{3:0.0}px; ratios={4:0.0000}x{5:0.0000}; " +
                    "destination={6:0.0}x{7:0.0}px.",
                    mainPixels.Width, mainPixels.Height,
                    panelPixels.Width, panelPixels.Height,
                    panelPixels.Width / mainPixels.Width,
                    panelPixels.Height / mainPixels.Height,
                    slide.PanelBounds.Width * sx,
                    slide.PanelBounds.Height * sy));
            }
        }

        private static void ApplyIndustrialVisualFixture(IWidget widget)
        {
            Type type = widget.GetType();
            MethodInfo pause = type.GetMethod("Pause", BindingFlags.Public |
                BindingFlags.Instance);
            if (pause != null) pause.Invoke(widget, null);
            SetPrivateField(widget, "_locationName", "Port Williams, Nova Scotia, Canada");
            SetPrivateField(widget, "_condition", "Cloudy");
            SetPrivateField(widget, "_icon", 6);
            SetPrivateField(widget, "_temperature", 21D);
            SetPrivateField(widget, "_feelsLike", 25D);
            SetPrivateField(widget, "_hasFeelsLike", true);
            SetPrivateField(widget, "_humidity", 94);
            SetPrivateField(widget, "_hasHumidity", true);
            SetPrivateField(widget, "_windKmh", 4D);
            SetPrivateField(widget, "_hasWind", true);
            SetPrivateField(widget, "_loading", false);
            SetPrivateField(widget, "_error", null);
            SetPrivateField(widget, "_lastSuccessLocal", new DateTime(
                2026, 8, 9, 1, 13, 0));
            var forecasts = (System.Collections.IList)type.GetField(
                "_forecastDays", BindingFlags.NonPublic |
                BindingFlags.Instance).GetValue(widget);
            forecasts.Clear();
            Type forecastType = type.GetNestedType("ForecastDay",
                BindingFlags.NonPublic);
            for (int index = 0; index < 4; index++)
            {
                object forecast = Activator.CreateInstance(forecastType, true);
                forecastType.GetField("Date").SetValue(forecast,
                    new DateTime(2026, 8, 9).AddDays(index));
                forecastType.GetField("Icon").SetValue(forecast,
                    new[] { 3, 4, 12, 6, 4 }[index]);
                forecastType.GetField("Condition").SetValue(forecast, "Cloudy");
                forecastType.GetField("HighC").SetValue(forecast, 20D + index);
                forecastType.GetField("LowC").SetValue(forecast, 18D - index);
                forecasts.Add(forecast);
            }
        }

        private static void SetPrivateField(
            object instance, string fieldName, object value)
        {
            instance.GetType().GetField(fieldName, BindingFlags.NonPublic |
                BindingFlags.Instance).SetValue(instance, value);
        }

        private static string VerifyIndustrialTypography(
            IWidget widget, float scale, float sx, float sy)
        {
            FieldInfo layoutField = widget.GetType().GetField(
                "_lastIndustrialTypographyLayout",
                BindingFlags.NonPublic | BindingFlags.Instance);
            object layout = layoutField.GetValue(widget);
            if (layout == null)
                throw new InvalidOperationException(
                    "Industrial typography layout was not captured.");
            Type type = layout.GetType();
            RectangleF temperature = (RectangleF)type.GetField(
                "TemperatureBounds").GetValue(layout);
            RectangleF condition = (RectangleF)type.GetField(
                "ConditionBounds").GetValue(layout);
            var labels = (RectangleF[])type.GetField(
                "MetricLabelBounds").GetValue(layout);
            var values = (RectangleF[])type.GetField(
                "MetricValueBounds").GetValue(layout);
            var forecastDays = (RectangleF[])type.GetField(
                "ForecastDayBounds").GetValue(layout);
            var forecastIcons = (RectangleF[])type.GetField(
                "ForecastIconBounds").GetValue(layout);
            var forecastTemperatures = (RectangleF[])type.GetField(
                "ForecastTemperatureBounds").GetValue(layout);
            RectangleF metricsRegion = (RectangleF)type.GetField(
                "MetricsRegion").GetValue(layout);
            RectangleF forecastRegion = (RectangleF)type.GetField(
                "ForecastRegion").GetValue(layout);
            RectangleF footerRegion = (RectangleF)type.GetField(
                "FooterRegion").GetValue(layout);
            RectangleF footerBounds = (RectangleF)type.GetField(
                "FooterBounds").GetValue(layout);
            float divider = (float)type.GetField(
                "ForecastDividerY").GetValue(layout);
            RectangleF temperaturePixels = ScaleBounds(temperature, sx, sy);
            RectangleF conditionPixels = ScaleBounds(condition, sx, sy);
            RectangleF labelPixels = ScaleBounds(labels[0], sx, sy);
            RectangleF valuePixels = ScaleBounds(values[0], sx, sy);
            float dividerPixels = divider * sy;
            float temperatureGap = conditionPixels.Top - temperaturePixels.Bottom;
            float conditionGap = labelPixels.Top - conditionPixels.Bottom;
            float labelValueGap = valuePixels.Top - labelPixels.Bottom;
            float dividerGap = dividerPixels - valuePixels.Bottom;
            if (temperatureGap < 4.5F - .1F || conditionGap < 7F - .1F ||
                labelValueGap < 4F - .1F || dividerGap < 3.5F - .1F)
                throw new InvalidOperationException(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Industrial measured typography gaps failed at {0:0.00}x: " +
                    "temperature={1:0.0}, condition={2:0.0}, label/value={3:0.0}, divider={4:0.0}.",
                    scale, temperatureGap, conditionGap,
                    labelValueGap, dividerGap));
            for (int index = 0; index < 3; index++)
                if (!ContainsWithTolerance(metricsRegion, labels[index]) ||
                    !ContainsWithTolerance(metricsRegion, values[index]))
                    throw new InvalidOperationException(
                        "Industrial metric typography escaped its fixed region.");
            for (int index = 0; index < 4; index++)
            {
                if (!ContainsWithTolerance(forecastRegion, forecastDays[index]) ||
                    !ContainsWithTolerance(forecastRegion, forecastIcons[index]) ||
                    !ContainsWithTolerance(forecastRegion,
                        forecastTemperatures[index]))
                    throw new InvalidOperationException(
                        "Industrial forecast content escaped its fixed region.");
                float dayCenter = forecastDays[index].Left +
                    forecastDays[index].Width / 2F;
                float iconCenter = forecastIcons[index].Left +
                    forecastIcons[index].Width / 2F;
                float temperatureCenter = forecastTemperatures[index].Left +
                    forecastTemperatures[index].Width / 2F;
                if (Math.Abs(dayCenter - iconCenter) > .1F ||
                    Math.Abs(dayCenter - temperatureCenter) > .1F)
                    throw new InvalidOperationException(
                        "Industrial forecast column centers are inconsistent.");
            }
            if (!ContainsWithTolerance(footerRegion, footerBounds))
                throw new InvalidOperationException(
                    "Industrial Updated text escaped its fixed footer region.");
            float pointToPixel = 96F / 72F * sy;
            float temperatureFont = (float)type.GetField(
                "TemperatureFontPoints").GetValue(layout) * pointToPixel;
            float conditionFont = (float)type.GetField(
                "ConditionFontPoints").GetValue(layout) * pointToPixel;
            float labelFont = (float)type.GetField(
                "MetricLabelFontPoints").GetValue(layout) * pointToPixel;
            float valueFont = (float)type.GetField(
                "MetricValueFontPoints").GetValue(layout) * pointToPixel;
            float dayFont = (float)type.GetField(
                "DayFontPoints").GetValue(layout) * pointToPixel;
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Typography {0:0}%: fonts temp={1:0.0}px condition={2:0.0}px " +
                "labels={3:0.0}px values={4:0.0}px weekdays={5:0.0}px; " +
                "tempBounds={6}; conditionBounds={7}; labelBounds={8}; valueBounds={9}; " +
                "dividerY={10:0.0}; gaps temp/condition={11:0.0}px, " +
                "condition/label={12:0.0}px, label/value={13:0.0}px, value/divider={14:0.0}px",
                scale * 100F, temperatureFont, conditionFont,
                labelFont, valueFont, dayFont,
                FormatBounds(temperaturePixels), FormatBounds(conditionPixels),
                FormatBounds(labelPixels), FormatBounds(valuePixels),
                dividerPixels, temperatureGap, conditionGap,
                labelValueGap, dividerGap);
        }

        private static bool ContainsWithTolerance(
            RectangleF outer, RectangleF inner)
        {
            const float tolerance = .1F;
            return inner.Left >= outer.Left - tolerance &&
                inner.Top >= outer.Top - tolerance &&
                inner.Right <= outer.Right + tolerance &&
                inner.Bottom <= outer.Bottom + tolerance;
        }

        private static RectangleF ScaleBounds(
            RectangleF bounds, float sx, float sy)
        {
            return new RectangleF(
                bounds.X * sx, bounds.Y * sy,
                bounds.Width * sx, bounds.Height * sy);
        }

        private static string FormatBounds(RectangleF bounds)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "({0:0.0},{1:0.0},{2:0.0},{3:0.0})",
                bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        private static void ToggleForecast(IWidget widget, WidgetWindow window)
        {
            var hotspots = (IWidgetHotspotProvider)widget;
            var input = (IWidgetPointerInput)widget;
            WidgetHotspot toggle = null;
            foreach (WidgetHotspot hotspot in hotspots.GetHotspots(window.ClientSize))
                if (string.Equals(hotspot.Id, "forecast-toggle",
                    StringComparison.OrdinalIgnoreCase))
                {
                    toggle = hotspot;
                    break;
                }
            if (toggle == null)
                throw new InvalidOperationException("Weather toggle hotspot is missing.");
            Point point = new Point(
                toggle.Bounds.Left + toggle.Bounds.Width / 2,
                toggle.Bounds.Top + toggle.Bounds.Height / 2);
            if (!input.PointerDown(point, WidgetPointerButton.Left))
                throw new InvalidOperationException("Weather toggle did not capture pointer-down.");
            input.PointerUp(point, WidgetPointerButton.Left);
        }

        private static int ArtDecoAnimationFrames(IWidget widget)
        {
            FieldInfo field = widget.GetType().GetField(
                "_artDecoAnimationFrameCount",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (int)field.GetValue(widget);
        }

        private static string ResolveWeatherAssembly()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string installed = Path.Combine(baseDirectory, "Widgets", "Weather",
                "XWidgetReborn.Widgets.Weather.dll");
            if (File.Exists(installed)) return installed;
            string source = Path.GetFullPath(Path.Combine(baseDirectory,
                @"..\..\..\XWidgetReborn.Widgets.Weather\bin\Release\XWidgetReborn.Widgets.Weather.dll"));
            if (File.Exists(source)) return source;
            throw new FileNotFoundException("Native Weather assembly was not found.", installed);
        }

        private sealed class VerificationSettings : IRuntimeSettingsService
        {
            private readonly float _scale;
            private Point _position = new Point(40, 40);
            private readonly string _animation;
            private readonly string _theme;
            public VerificationSettings(float scale)
                : this(scale, "Instant", "Art Deco") { }
            public VerificationSettings(
                float scale, string animation, string theme)
            {
                _scale = scale;
                _animation = animation;
                _theme = theme;
            }
            public Point LastPosition { get { return _position; } }
            public void ApplyMigrations() { }
            public bool IsWidgetEnabled(string widgetId, bool fallback) { return true; }
            public void SetWidgetEnabled(string widgetId, bool enabled) { }
            public Point GetWidgetPosition(string widgetId, Point fallback) { return _position; }
            public void SetWidgetPosition(string widgetId, Point position) { _position = position; }
            public bool IsWidgetTopMost(string widgetId, bool fallback) { return false; }
            public void SetWidgetTopMost(string widgetId, bool topMost) { }
            public string GetWidgetSetting(string widgetId, string key, string fallback)
            {
                if (string.Equals(key, "appearance", StringComparison.OrdinalIgnoreCase))
                    return _theme;
                if (string.Equals(key, "weather.panelAnimation", StringComparison.OrdinalIgnoreCase))
                    return _animation;
                if (string.Equals(key, "scale", StringComparison.OrdinalIgnoreCase))
                    return _scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return fallback;
            }
            public void SetWidgetSetting(string widgetId, string key, string value) { }
        }
    }
}
