using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Globalization;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Widgets.Weather
{
    public sealed partial class NativeWeatherWidget
    {
        private IndustrialDesignerLayout NatureEditableWeatherLayout()
        {
            return IsBotanical ? BotanicalDesignerLayout.Current() : WoodlandDesignerLayout.Current();
        }

        private IndustrialDesignerLayout EditableCompactWeatherLayout()
        {
            if (!IsModern && !IsVintage) return null;
            IndustrialDesignerLayout layout = IndustrialDesignerLayout.Current(
                DesignerLayoutFiles.FileName(_appearance, "weather"));
            return layout != null && layout.EditableLayerVersion >= 1 ? layout : null;
        }

        public void RenderEditableDesignerReference(Graphics graphics, Rectangle bounds, bool details)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 360F, bounds.Height / 210F);
                RectangleF card = details ? new RectangleF(-18, 5, 292, 200) : new RectangleF(5, 5, 350, 200);
                using (var path = Rounded(card, 24))
                {
                    if (IsModern)
                    {
                        ModernThemePainter.FillGlassCard(graphics, path, card);
                        if (!details) DrawModernWeatherFrame(graphics);
                        else for (int row = 0; row < 4; row++)
                            ModernThemePainter.FillInsetPanel(graphics, new RectangleF(14, 49 + row * 29, 250, 24), 6F);
                    }
                    else
                    {
                        VintageThemePainter.FillPaper(graphics, path, card);
                        VintageThemePainter.DrawDoubleBorder(graphics, path, RectangleF.Inflate(card, -8, -8), 16);
                        if (!details) DrawVintageWeatherFrame(graphics);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        public void RenderEditableDesignerSymbol(Graphics graphics, string id,
            RectangleF bounds, int colorArgb, float opacity, float fontSize)
        {
            if (opacity <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
            if (id == "panel-button")
                DrawToggleButton(graphics, bounds, Color.FromArgb(colorArgb), opacity, fontSize);
            else if ((id ?? "").StartsWith("forecast-", StringComparison.OrdinalIgnoreCase))
                DrawThemeMiniWeatherSymbol(graphics, bounds, 3);
            else DrawThemeWeatherSymbol(graphics, bounds, 3);
        }

        public void RenderEditableDesignerBackground(Graphics graphics,
            RectangleF bounds, bool details)
        {
            RenderEditableDesignerBackground(graphics, bounds, details,
                string.Empty, 1F, false);
        }

        // Designer previews always operate on the editable painted-frame
        // rectangle. Keep the established five-argument API for Designer and
        // verification callers; runtime layout migration is selected by the
        // explicit overload below.
        public void RenderEditableDesignerBackground(Graphics graphics,
            RectangleF bounds, bool details, string imagePath, float opacity)
        {
            RenderEditableDesignerBackground(graphics, bounds, details,
                imagePath, opacity, true);
        }

        public void RenderEditableDesignerBackground(Graphics graphics,
            RectangleF bounds, bool details, string imagePath, float opacity,
            bool cropTransparentCanvas)
        {
            if (graphics == null || bounds.Width <= 0F || bounds.Height <= 0F)
                return;
            opacity = Math.Max(0F, Math.Min(1F, opacity));
            if (opacity <= 0F) return;
            if (TryDrawEditableDesignerBackgroundImage(graphics, bounds,
                details, imagePath, opacity,
                cropTransparentCanvas)) return;

            if (opacity < .999F)
            {
                int width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
                int height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
                using (var layer = new Bitmap(width, height,
                    PixelFormat.Format32bppPArgb))
                {
                    using (Graphics layerGraphics = Graphics.FromImage(layer))
                    {
                        layerGraphics.SmoothingMode =
                            SmoothingMode.HighQuality;
                        DrawEditableDesignerVectorBackground(layerGraphics,
                            new RectangleF(0F, 0F, width, height), details);
                    }
                    DrawEditableDesignerBackgroundImage(graphics, layer,
                        bounds, new Rectangle(0, 0, width, height), opacity,
                        false);
                }
                return;
            }

            DrawEditableDesignerVectorBackground(graphics, bounds, details);
        }

        private void DrawEditableDesignerVectorBackground(Graphics graphics,
            RectangleF bounds, bool details)
        {
            float radius = Math.Max(4F,
                Math.Min(24F, Math.Min(bounds.Width, bounds.Height) / 5F));
            using (GraphicsPath path = Rounded(bounds, radius))
            {
                if (IsModern)
                {
                    ModernThemePainter.FillGlassCard(graphics, path, bounds);
                    if (details)
                        for (int row = 0; row < 4; row++)
                        {
                            float rowHeight = bounds.Height * .115F;
                            float rowTop = bounds.Top + bounds.Height *
                                (.22F + row * .14F);
                            ModernThemePainter.FillInsetPanel(graphics,
                                new RectangleF(
                                    bounds.Left + bounds.Width * .07F,
                                    rowTop, bounds.Width * .86F,
                                    rowHeight), Math.Max(2F, radius / 4F));
                        }
                    return;
                }
                if (IsVintage)
                {
                    VintageThemePainter.FillPaper(graphics, path, bounds);
                    VintageThemePainter.DrawDoubleBorder(graphics, path,
                        RectangleF.Inflate(bounds, -8F, -8F), 16F);
                }
            }
        }

        private bool TryDrawEditableDesignerBackgroundImage(
            Graphics graphics, RectangleF bounds, bool details,
            string imagePath, float opacity, bool cropTransparentCanvas)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return false;
            // Some existing installations may already carry the new version
            // marker while retaining the old full-canvas rectangle. Never
            // stretch the painted portion of that legacy Weather background.
            if (cropTransparentCanvas && !details &&
                IsLegacyFullWeatherCanvas(bounds))
                cropTransparentCanvas = false;
            bool industrialDetails = details &&
                UsesIndustrialWeatherPresentation && SameDesignerAssetPath(
                    imagePath, IndustrialPresentationPanelSkinPath);
            bool mirroredArtDecoDetails = details && IsArtDeco &&
                SameDesignerAssetPath(imagePath,
                    @"Assets\Themes\ArtDeco\art-deco-forecast-panel-skin.png");
            bool bundledThemeAsset = !Path.IsPathRooted(imagePath) &&
                imagePath.Replace('/', '\\').TrimStart('.', '\\').StartsWith(
                    @"Assets\Themes\", StringComparison.OrdinalIgnoreCase);

            if (bundledThemeAsset)
            {
                try
                {
                    Image image = ThemeSkinCache.Get(imagePath);
                    Rectangle source = cropTransparentCanvas ||
                        industrialDetails
                        ? ThemeSkinCache.GetAlphaBounds(imagePath)
                        : new Rectangle(0, 0, image.Width, image.Height);
                    if (source.Width <= 0 || source.Height <= 0)
                        source = new Rectangle(0, 0, image.Width,
                            image.Height);
                    DrawEditableDesignerBackgroundImage(graphics, image,
                        bounds, source, opacity, mirroredArtDecoDetails);
                    return true;
                }
                catch { return false; }
            }

            string resolved = ResolveDesignerAssetPath(imagePath);
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                return false;
            try
            {
                Image image = ThemeSkinCache.Get(resolved);
                Rectangle source = cropTransparentCanvas
                    ? ThemeSkinCache.GetAlphaBounds(resolved)
                    : new Rectangle(0, 0, image.Width, image.Height);
                if (source.Width <= 0 || source.Height <= 0)
                    source = new Rectangle(0, 0, image.Width,
                        image.Height);
                DrawEditableDesignerBackgroundImage(graphics, image,
                    bounds, source, opacity, false);
                return true;
            }
            catch { return false; }
        }

        private bool IsLegacyFullWeatherCanvas(RectangleF bounds)
        {
            float width = IsArtDeco ? 500F :
                UsesIndustrialWeatherPresentation ? 750F : 360F;
            float height = IsArtDeco ? 346F :
                UsesIndustrialWeatherPresentation ? 500F : 210F;
            return Math.Abs(bounds.X) < .1F &&
                Math.Abs(bounds.Y) < .1F &&
                Math.Abs(bounds.Width - width) < .1F &&
                Math.Abs(bounds.Height - height) < .1F;
        }

        private static void DrawEditableDesignerBackgroundImage(
            Graphics graphics, Image image, RectangleF bounds,
            Rectangle source, float opacity, bool mirrorHorizontally)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality =
                    CompositingQuality.HighQuality;
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix
                    {
                        Matrix33 = opacity
                    });
                    Rectangle destination = Rectangle.Round(bounds);
                    if (mirrorHorizontally)
                    {
                        var points = new[]
                        {
                            new Point(destination.Right, destination.Top),
                            new Point(destination.Left, destination.Top),
                            new Point(destination.Right, destination.Bottom)
                        };
                        graphics.DrawImage(image, points, source,
                            GraphicsUnit.Pixel, attributes);
                    }
                    else
                    {
                        graphics.DrawImage(image, destination, source.X,
                            source.Y, source.Width, source.Height,
                            GraphicsUnit.Pixel, attributes);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        private static bool SameDesignerAssetPath(string first,
            string second)
        {
            if (string.IsNullOrWhiteSpace(first) ||
                string.IsNullOrWhiteSpace(second)) return false;
            return string.Equals(first.Replace('/', '\\').TrimStart('.', '\\'),
                second.Replace('/', '\\').TrimStart('.', '\\'),
                StringComparison.OrdinalIgnoreCase);
        }

        private void DrawEditableWeatherLayers(Graphics graphics, WeatherSnapshot snapshot,
            IndustrialDesignerLayout layout, int surface)
        {
            string pack = layout.WeatherIconPack;
            bool compact = IsModern || IsVintage;
            if (!compact && string.IsNullOrWhiteSpace(pack))
                pack = DefaultWeatherIconPack(IsBotanical ? "BotanicalNature" : "WoodlandNature");
            foreach (IndustrialDesignerElement item in layout.Elements)
            {
                if (item == null || !item.Visible || item.Surface != surface || layout.IsDeleted(item.Id) ||
                    item.Width <= 0 || item.Height <= 0 || item.Opacity <= 0) continue;
                if (IsDesignerBackground(item, surface)) continue;
                if (item.Kind == 0)
                {
                    DrawIndustrialDesignerText(graphics, item, ResolveEditableWeatherText(item, snapshot));
                    continue;
                }
                if (item.Kind == 2)
                {
                    DrawIndustrialDivider(graphics, layout, item.Id, Pens.Transparent, RectangleF.Empty, false);
                    continue;
                }
                if (DrawDesignerPng(graphics, item)) continue;
                if (item.Id == "panel-button" || item.Binding == "Control: Weather Details Button")
                {
                    DrawToggleButton(graphics, item.Bounds, Color.FromArgb(item.ColorArgb), item.Opacity,
                        item.FontSize * Math.Max(.05F, item.Scale));
                    continue;
                }
                int detailIndex;
                if ((item.Id ?? "").StartsWith("details-icon-", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(item.Id.Substring(13), out detailIndex) && detailIndex >= 0 && detailIndex < 8)
                {
                    string[] labels = { "Feels Like", "Humidity", "Wind", "Pressure",
                        "Dew Point", "Visibility", "UV Index", "Sunrise / Sunset" };
                    DrawWoodlandWeatherDetailIcon(graphics, labels[detailIndex], item.Bounds);
                    continue;
                }
                if (compact && string.IsNullOrWhiteSpace(pack))
                {
                    if (item.Binding == "Weather: Current Icon")
                        DrawThemeWeatherSymbol(graphics, item.Bounds, snapshot.Icon);
                    else if (item.Binding == "Weather: Forecast Icon")
                    {
                        int index = UnboundedForecastIndex(item);
                        if (snapshot.ForecastDays != null && index >= 0 &&
                            index < snapshot.ForecastDays.Count)
                            DrawThemeMiniWeatherSymbol(graphics, item.Bounds, snapshot.ForecastDays[index].Icon);
                    }
                }
                else DrawIndustrialDesignerWeatherImage(graphics, item, snapshot, pack);
            }
        }

        private static bool IsDesignerBackground(
            IndustrialDesignerElement element, int surface)
        {
            return element != null && string.Equals(element.Id,
                surface == 0 ? "main-background" : "details-background",
                StringComparison.OrdinalIgnoreCase);
        }

        private static IndustrialDesignerElement DesignerBackground(
            IndustrialDesignerLayout layout, int surface)
        {
            if (layout == null || layout.Elements == null) return null;
            string id = surface == 0
                ? "main-background" : "details-background";
            return layout.Elements.Find(delegate(
                IndustrialDesignerElement element)
            {
                return element != null && !layout.IsDeleted(id) &&
                    string.Equals(element.Id, id,
                        StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool SuppressDefaultDesignerBackground(
            IndustrialDesignerLayout layout, int surface)
        {
            if (layout == null) return false;
            string id = surface == 0
                ? "main-background" : "details-background";
            return layout.IsDeleted(id) || DesignerBackground(layout, surface) != null;
        }

        private string ResolveEditableWeatherText(IndustrialDesignerElement item, WeatherSnapshot snapshot)
        {
            string binding = (item.Binding ?? "").ToLowerInvariant();
            string label = null, value = null;
            if (binding == "weather: feels like")
            {
                label = "Feels Like";
                value = snapshot.HasFeelsLike ? Math.Round(snapshot.FeelsLike) + Degree() : item.Text;
            }
            else if (binding == "weather: humidity")
            {
                label = "Humidity";
                value = snapshot.HasHumidity ? snapshot.Humidity + "%" : item.Text;
            }
            else if (binding == "weather: wind")
            {
                label = "Wind";
                value = snapshot.HasWind ? (snapshot.WindDirection ?? "") + " " + Math.Round(snapshot.Wind) +
                    (_metric ? " km/h" : " mph") : item.Text;
            }
            if (label != null)
            {
                // Keep the original three compact nature-theme metrics as
                // their intentional two-line label/value blocks. A text layer
                // added in Designer must retain the one-line sentence shown
                // on the canvas instead of receiving a runtime-only newline.
                bool originalMainMetric = item.Surface == 0 &&
                    (string.Equals(item.Id, "feels-like",
                        StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(item.Id, "humidity",
                        StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(item.Id, "wind",
                        StringComparison.OrdinalIgnoreCase));
                if (UsesWoodlandNaturePresentation && originalMainMetric)
                    return label + Environment.NewLine + value;
                if (item.Surface == 0)
                    return label + " " + value;
                return value;
            }
            if (binding == "weather: updated time")
            {
                if (!string.IsNullOrEmpty(snapshot.Error)) return "Saved weather · refresh unavailable";
                if (snapshot.Updated != DateTime.MinValue)
                    return "Updated " + snapshot.Updated.ToString("h:mm tt", CultureInfo.CurrentCulture);
                return snapshot.Loading ? "Updating…" : "Waiting for weather service";
            }
            return ResolveIndustrialDesignerBinding(item, snapshot);
        }
    }
}
