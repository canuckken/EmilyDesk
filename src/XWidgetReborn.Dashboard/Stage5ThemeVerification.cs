using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal static class Stage5ThemeVerification
    {
        private static readonly string[] Expected =
        {
            "Modern", "Vintage", "Steampunk", "Industrial",
            "Art Deco", "Botanical Nature", "Woodland Nature"
        };
        private static readonly string[] GalleryThemes =
            Expected.Concat(new[] { "Ember Glow" }).ToArray();

        private const int GalleryPreviewWidth = 640;
        private const int GalleryPreviewHeight = 360;

        public static void GenerateGalleryPreviews(string destinationDirectory)
        {
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ArgumentException(
                    "A Gallery preview destination is required.",
                    "destinationDirectory");

            string destination = Path.GetFullPath(destinationDirectory);
            string root = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\..\.."));
            string temporary = destination + ".generating-" +
                Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(temporary);
            Require(EmilyDeskThemeCatalog.Get("Ember Glow").IsImported,
                "Ember Glow theme assets are required for Gallery previews.");

            var widgets = new[]
            {
                Load(root, "Clock", "XWidgetReborn.Widgets.Clock.NativeClockWidget"),
                Load(root, "Calendar", "XWidgetReborn.Widgets.Calendar.NativeCalendarWidget"),
                Load(root, "Weather", "XWidgetReborn.Widgets.Weather.NativeWeatherWidget"),
                Load(root, "RecycleBin", "XWidgetReborn.Widgets.RecycleBin.NativeRecycleBinWidget")
            };
            try
            {
                foreach (IWidget widget in widgets)
                {
                    var host = new TestHost();
                    if (widget.Name == "Weather")
                        host.SetSetting("weather.panelAnimation", "Instant");
                    ((IOfficialWidget)widget).AttachHost(host);
                    if (widget.Name == "Weather")
                        SeedWeatherPreview(widget);
                    else if (widget.Name == "Recycle Bin")
                        SeedRecycleBinPreview(widget);
                    else
                        widget.Tick(DateTime.Now);

                    IWidgetThemeProvider provider =
                        (IWidgetThemeProvider)widget;
                    foreach (string themeName in GalleryThemes)
                    {
                        provider.Theme = themeName;
                        Size size = host.PreferredSize;
                        if (size.Width < 1 || size.Height < 1)
                            size = widget.DefaultSize;
                        using (var rendered = new Bitmap(
                            size.Width,
                            size.Height,
                            PixelFormat.Format32bppArgb))
                        using (Graphics graphics = Graphics.FromImage(rendered))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            graphics.InterpolationMode =
                                InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.TextRenderingHint =
                                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                            widget.Render(
                                graphics,
                                new Rectangle(Point.Empty, size));
                            VerifyExpectedThemeSkinLoaded(
                                widget.Name, themeName);

                            string fileName = ThemeFileName(themeName) + "-" +
                                ThemeFileName(widget.Name) + ".png";
                            using (Bitmap preview = ComposeGalleryPreview(
                                rendered,
                                EmilyDeskThemeCatalog.Get(themeName)))
                                preview.Save(
                                    Path.Combine(temporary, fileName),
                                    ImageFormat.Png);
                        }
                    }
                }

                Directory.CreateDirectory(destination);
                string[] generated = Directory.GetFiles(
                    temporary,
                    "*.png",
                    SearchOption.TopDirectoryOnly);
                Require(generated.Length == GalleryThemes.Length * widgets.Length,
                    "Gallery preview rendering did not create all 32 images.");
                foreach (string file in generated)
                {
                    ValidateGalleryPreview(file);
                    PublishPreview(file, Path.Combine(
                        destination,
                        Path.GetFileName(file)));
                }

                string[] published = Directory.GetFiles(
                    destination,
                    "*.png",
                    SearchOption.TopDirectoryOnly);
                Require(published.Length == GalleryThemes.Length * widgets.Length,
                    "Gallery preview generation did not publish all 32 images.");
                Require(!File.ReadAllBytes(Path.Combine(destination,
                        "steampunk-weather.png")).SequenceEqual(
                            File.ReadAllBytes(Path.Combine(destination,
                                "industrial-weather.png"))),
                    "Steampunk and Industrial Weather previews used the same cached skin.");
            }
            finally
            {
                foreach (IWidget widget in widgets)
                    widget.Dispose();
                try
                {
                    if (Directory.Exists(temporary))
                        Directory.Delete(temporary, true);
                }
                catch { }
            }
        }

        private static Bitmap ComposeGalleryPreview(
            Bitmap rendered,
            EmilyDeskTheme theme)
        {
            Rectangle content = FindVisibleBounds(rendered);
            var preview = new Bitmap(
                GalleryPreviewWidth,
                GalleryPreviewHeight,
                PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(preview))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var background = new LinearGradientBrush(
                    new Rectangle(
                        0,
                        0,
                        GalleryPreviewWidth,
                        GalleryPreviewHeight),
                    theme.SurfaceTop,
                    theme.SurfaceBottom,
                    LinearGradientMode.Vertical))
                    graphics.FillRectangle(
                        background,
                        0,
                        0,
                        GalleryPreviewWidth,
                        GalleryPreviewHeight);

                const int margin = 18;
                float availableWidth = GalleryPreviewWidth - margin * 2F;
                float availableHeight = GalleryPreviewHeight - margin * 2F;
                float scale = Math.Min(
                    availableWidth / content.Width,
                    availableHeight / content.Height);
                float width = content.Width * scale;
                float height = content.Height * scale;
                var target = new RectangleF(
                    (GalleryPreviewWidth - width) / 2F,
                    (GalleryPreviewHeight - height) / 2F,
                    width,
                    height);
                graphics.DrawImage(
                    rendered,
                    target,
                    content,
                    GraphicsUnit.Pixel);
            }
            return preview;
        }

        private static void VerifyExpectedThemeSkinLoaded(
            string widgetName, string themeName)
        {
            string folder = string.Empty;
            string prefix = string.Empty;
            if (widgetName == "Recycle Bin" && themeName == "Modern")
            { folder = "Modern"; prefix = "modern"; }
            else if (widgetName == "Recycle Bin" && themeName == "Vintage")
            { folder = "Vintage"; prefix = "vintage"; }
            else if (themeName == "Industrial")
            { folder = "Industrial"; prefix = "industrial"; }
            else if (themeName == "Art Deco")
            { folder = "ArtDeco"; prefix = "art-deco"; }
            else if (themeName == "Steampunk")
            { folder = "Steampunk"; prefix = "steampunk"; }
            else if (themeName == "Botanical Nature")
            { folder = "BotanicalNature"; prefix = "botanical"; }
            else if (themeName == "Woodland Nature")
            { folder = "WoodlandNature"; prefix = "woodland"; }
            if (string.IsNullOrEmpty(folder)) return;
            string fileName = widgetName == "Recycle Bin"
                ? prefix + "-recycle-bin-empty.png"
                : prefix + "-" + widgetName.ToLowerInvariant() + "-skin.png";
            // Art Deco Weather intentionally uses the calendar skin as its
            // larger main-panel frame. The separate weather skin is retained
            // as an asset, but it is not the runtime background for this card.
            if (themeName == "Art Deco" && widgetName == "Weather")
                fileName = "art-deco-calendar-skin.png";
            string relative = Path.Combine(
                "Assets", "Themes", folder, fileName);
            Require(ThemeSkinCache.LoadCount(relative) >= 1,
                themeName + " " + widgetName +
                " Gallery preview did not load its matching theme skin.");
        }

        private static Rectangle FindVisibleBounds(Bitmap bitmap)
        {
            Rectangle bounds = new Rectangle(
                0,
                0,
                bitmap.Width,
                bitmap.Height);
            BitmapData data = bitmap.LockBits(
                bounds,
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                var pixels = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                int left = bitmap.Width;
                int top = bitmap.Height;
                int right = -1;
                int bottom = -1;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int row = data.Stride >= 0
                        ? y * stride
                        : (bitmap.Height - 1 - y) * stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        if (pixels[row + x * 4 + 3] == 0)
                            continue;
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
                }
                return right >= left && bottom >= top
                    ? Rectangle.FromLTRB(
                        left,
                        top,
                        right + 1,
                        bottom + 1)
                    : bounds;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void SeedWeatherPreview(IWidget widget)
        {
            SetField(widget, "_locationName", "Halifax");
            SetField(widget, "_condition", "Partly Cloudy");
            SetField(widget, "_icon", 3);
            SetField(widget, "_temperature", 18D);
            SetField(widget, "_feelsLike", 18D);
            SetField(widget, "_hasFeelsLike", true);
            SetField(widget, "_humidity", 68);
            SetField(widget, "_hasHumidity", true);
            SetField(widget, "_windKmh", 16D);
            SetField(widget, "_hasWind", true);
            SetField(widget, "_windDirection", "NW");
            SetField(widget, "_pressureHpa", 1013D);
            SetField(widget, "_hasPressure", true);
            SetField(widget, "_dewPointC", 12D);
            SetField(widget, "_hasDewPoint", true);
            SetField(widget, "_visibilityKm", 14D);
            SetField(widget, "_hasVisibility", true);
            SetField(widget, "_uvIndex", 3D);
            SetField(widget, "_hasUvIndex", true);
            SetField(widget, "_uvText", "Moderate");
            SetField(widget, "_sunrise", "6:30 AM");
            SetField(widget, "_sunset", "8:15 PM");
            SetField(widget, "_high", 20D);
            SetField(widget, "_low", 12D);
            SetField(widget, "_lastSuccessLocal", DateTime.Now);
            SetField(widget, "_loading", false);
            SetField(widget, "_error", null);

            Type widgetType = widget.GetType();
            Type dayType = widgetType.GetNestedType(
                "ForecastDay",
                BindingFlags.NonPublic);
            FieldInfo daysField = widgetType.GetField(
                "_forecastDays",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Require(dayType != null && daysField != null,
                "Weather preview forecast fields were not found.");
            IList days = (IList)daysField.GetValue(widget);
            days.Clear();
            int[] icons = { 3, 1, 7, 12 };
            string[] conditions =
                { "Partly Cloudy", "Sunny", "Cloudy", "Light Rain" };
            double[] highs = { 20D, 21D, 19D, 18D };
            double[] lows = { 12D, 13D, 11D, 10D };
            for (int index = 0; index < icons.Length; index++)
            {
                object day = Activator.CreateInstance(dayType, true);
                SetPublicField(day, "Date", DateTime.Today.AddDays(index));
                SetPublicField(day, "Icon", icons[index]);
                SetPublicField(day, "Condition", conditions[index]);
                SetPublicField(day, "HighC", highs[index]);
                SetPublicField(day, "LowC", lows[index]);
                days.Add(day);
            }
        }

        private static void SeedRecycleBinPreview(IWidget widget)
        {
            SetField(widget, "_isFull", false);
            SetField(widget, "_itemCount", 0L);
            SetField(widget, "_totalBytes", 0L);
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Require(field != null, "Preview field was not found: " + name);
            field.SetValue(target, value);
        }

        private static void SetPublicField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Require(field != null, "Preview field was not found: " + name);
            field.SetValue(target, value);
        }

        private static string ThemeFileName(string themeName)
        {
            return themeName.ToLowerInvariant().Replace(" ", "-");
        }

        private static void ValidateGalleryPreview(string path)
        {
            using (var image = new Bitmap(path))
            {
                Require(
                    image.Width == GalleryPreviewWidth &&
                    image.Height == GalleryPreviewHeight,
                    Path.GetFileName(path) +
                    " does not use the 640 x 360 Gallery canvas.");
                using (var decoded = new Bitmap(
                    image.Width,
                    image.Height,
                    PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(decoded))
                    graphics.DrawImageUnscaled(image, 0, 0);
            }
        }

        private static void PublishPreview(
            string source,
            string destination)
        {
            string incoming = destination + ".new";
            if (File.Exists(incoming))
                File.Delete(incoming);
            File.Copy(source, incoming, true);
            ValidateGalleryPreview(incoming);
            if (File.Exists(destination))
            {
                try
                {
                    File.Replace(incoming, destination, null);
                    return;
                }
                catch (PlatformNotSupportedException) { }
                catch (IOException) { }
                File.Delete(destination);
            }
            File.Move(incoming, destination);
        }

        public static void Run()
        {
            VerifySlidePanelComponent();
            VerifyDesignerPreviewPriority();
            string[] names = EmilyDeskThemeCatalog.BuiltInNames.ToArray();
            Require(names.SequenceEqual(Expected),
                "Theme catalog does not contain the seven approved families in order.");
            string root = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\..\.."));
            foreach (string name in names)
            {
                EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(name);
                Require(EmilyDeskThemeCatalog.ContrastRatio(
                    theme.PrimaryText, theme.SurfaceBottom) >= 4.5D,
                    name + " primary text contrast is below 4.5:1.");
                Require(EmilyDeskThemeCatalog.ContrastRatio(
                    theme.PrimaryText, theme.SurfaceTop) >= 4.5D,
                    name + " top-surface text contrast is below 4.5:1.");
                Require(EmilyDeskThemeCatalog.ContrastRatio(
                    EmilyDeskThemeCatalog.BestTextOn(theme.Accent),
                    theme.Accent) >= 4.5D,
                    name + " accent control contrast is below 4.5:1.");
                Require(!string.IsNullOrWhiteSpace(theme.WallpaperFileName),
                    name + " has no recommended wallpaper mapping.");
                string recycleBinFolder = name.Replace(" ", string.Empty);
                string emptyBinPath = Path.Combine(root, "Assets",
                    "RecycleBins", recycleBinFolder, "empty.ico");
                string fullBinPath = Path.Combine(root, "Assets",
                    "RecycleBins", recycleBinFolder, "full.ico");
                Require(File.Exists(emptyBinPath),
                    name + " has no empty Recycle Bin icon.");
                Require(File.Exists(fullBinPath),
                    name + " has no full Recycle Bin icon.");
                using (var emptyBinIcon = new Icon(emptyBinPath, 128, 128))
                using (var fullBinIcon = new Icon(fullBinPath, 128, 128))
                {
                    Require(emptyBinIcon.Width >= 128 &&
                        emptyBinIcon.Height >= 128,
                        name + " empty Recycle Bin icon is too small.");
                    Require(fullBinIcon.Width >= 128 &&
                        fullBinIcon.Height >= 128,
                        name + " full Recycle Bin icon is too small.");
                }
            }

            string wallpaperBefore = WallpaperManager.CurrentPath();
            foreach (string name in names)
            {
                string path = WallpaperManager.RecommendedPath(name);
                if (File.Exists(path)) WallpaperManager.ValidateImage(path);
            }
            Require(string.Equals(
                wallpaperBefore, WallpaperManager.CurrentPath(),
                StringComparison.OrdinalIgnoreCase),
                "Wallpaper discovery or preview changed Windows automatically.");

            VerifyWeatherIconPack(root, "ArtDeco");
            VerifyWeatherIconPack(root, "BotanicalNature");
            VerifyClockAndNatureArtwork(root);
            var widgets = new[]
            {
                Load(root, "Clock", "XWidgetReborn.Widgets.Clock.NativeClockWidget"),
                Load(root, "Calendar", "XWidgetReborn.Widgets.Calendar.NativeCalendarWidget"),
                Load(root, "Weather", "XWidgetReborn.Widgets.Weather.NativeWeatherWidget"),
                Load(root, "RecycleBin", "XWidgetReborn.Widgets.RecycleBin.NativeRecycleBinWidget")
            };
            var timer = Stopwatch.StartNew();
            try
            {
                foreach (IWidget widget in widgets)
                    VerifyWidget(widget, names);
            }
            finally
            {
                foreach (IWidget widget in widgets) widget.Dispose();
            }
            // Rendering all four widgets across every theme can take longer
            // on systems where image decoding, antivirus scanning, or disk
            // activity is busy. Keep a generous safety limit so Stage 5 still
            // catches a genuine rendering hang without rejecting a valid build
            // because of normal machine-to-machine timing differences.
            Require(timer.Elapsed < TimeSpan.FromSeconds(90),
                "Static theme rendering exceeded the 90-second safety limit.");
            RequireRecycleBinSkinLoaded("Modern", "modern");
            RequireRecycleBinSkinLoaded("Vintage", "vintage");
            foreach (string file in new[]
            {
                "art-deco-clock-skin.png",
                "art-deco-calendar-skin.png"
            })
                Require(ThemeSkinCache.LoadCount(
                    @"Assets\Themes\ArtDeco\" + file) >= 1,
                    file + " was not loaded by the shared cache.");
            foreach (string file in new[]
            {
                "industrial-clock-skin.png",
                "industrial-hour-hand.png",
                "industrial-minute-hand.png",
                "industrial-second-hand.png",
                "industrial-weather-skin.png",
                "industrial-calendar-skin.png"
            })
                Require(ThemeSkinCache.LoadCount(
                    @"Assets\Themes\Industrial\" + file) >= 1,
                    file + " was not loaded by the shared cache.");
            foreach (string file in new[]
            {
                "steampunk-clock-skin.png",
                "steampunk-weather-skin.png",
                "steampunk-calendar-skin.png"
            })
                Require(ThemeSkinCache.LoadCount(
                    @"Assets\Themes\Steampunk\" + file) >= 1,
                    file + " was not loaded by the shared cache.");
            foreach (string file in new[]
            {
                "woodland-clock-skin.png",
                "woodland-hour-hand.png",
                "woodland-minute-hand.png",
                "woodland-second-hand.png",
                "woodland-weather-skin.png",
                "woodland-calendar-skin.png"
            })
                Require(ThemeSkinCache.LoadCount(
                    @"Assets\Themes\WoodlandNature\" + file) >= 1,
                    file + " was not loaded by the shared cache.");
            foreach (string file in new[]
            {
                "botanical-clock-skin.png",
                "botanical-hour-hand.png",
                "botanical-minute-hand.png",
                "botanical-second-hand.png",
                "botanical-weather-skin.png",
                "botanical-calendar-skin.png"
            })
                Require(ThemeSkinCache.LoadCount(
                    @"Assets\Themes\BotanicalNature\" + file) >= 1,
                    file + " was not loaded by the shared cache.");
            RequireRecycleBinSkinLoaded("ArtDeco", "art-deco");
            RequireRecycleBinSkinLoaded("Industrial", "industrial");
            RequireRecycleBinSkinLoaded("Steampunk", "steampunk");
            RequireRecycleBinSkinLoaded("WoodlandNature", "woodland");
            RequireRecycleBinSkinLoaded("BotanicalNature", "botanical");
        }

        private static void VerifyDesignerPreviewPriority()
        {
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "EmilyDesk-GalleryPreview-" +
                    Guid.NewGuid().ToString("N"));
            string oldRoot = Environment.GetEnvironmentVariable(
                "EMILYDESK_DESIGNER_ARTWORK_ROOT");
            Directory.CreateDirectory(temporary);
            try
            {
                string fallback = Path.Combine(temporary, "fallback.png");
                using (var image = new Bitmap(32, 32,
                    PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(Color.DarkOliveGreen);
                    image.Save(fallback, ImageFormat.Png);
                }
                Environment.SetEnvironmentVariable(
                    "EMILYDESK_DESIGNER_ARTWORK_ROOT",
                    temporary);
                var descriptor = new WidgetDescriptor(
                    "native.clock", "Clock", "Preview verification",
                    "1.0.0", "EmilyDesk", string.Empty, fallback,
                    "Clock", true, true, string.Empty, new string[0]);
                var item = new GalleryWidgetItem(
                    descriptor, "Preview Test");
                WidgetPreviewResolver.Clear();
                string before = WidgetPreviewResolver.Resolve(item, null);
                string designer = Path.Combine(
                    temporary, "Clock", "Preview Test", "preview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(designer));
                using (var image = new Bitmap(32, 32,
                    PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(Color.Goldenrod);
                    image.Save(designer, ImageFormat.Png);
                }
                File.SetLastWriteTimeUtc(fallback,
                    DateTime.UtcNow.AddMinutes(-4));
                File.SetLastWriteTimeUtc(designer,
                    DateTime.UtcNow.AddMinutes(-2));
                string after = WidgetPreviewResolver.Resolve(item, null);
                Require(!string.Equals(before, designer,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(after, designer,
                        StringComparison.OrdinalIgnoreCase),
                    "A cached bundled preview hid newer Designer artwork.");
                File.SetLastWriteTimeUtc(fallback,
                    DateTime.UtcNow.AddMinutes(2));
                string packaged = WidgetPreviewResolver.Resolve(item, null);
                Require(string.Equals(packaged, fallback,
                        StringComparison.OrdinalIgnoreCase),
                    "Stale Designer artwork hid a newer packaged preview.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "EMILYDESK_DESIGNER_ARTWORK_ROOT", oldRoot);
                WidgetPreviewResolver.Clear();
                try
                {
                    if (Directory.Exists(temporary))
                        Directory.Delete(temporary, true);
                }
                catch { }
            }
        }

        private static void RequireRecycleBinSkinLoaded(
            string folder, string prefix)
        {
            string basePath = Path.Combine(
                "Assets", "Themes", folder,
                prefix + "-recycle-bin-");
            Require(ThemeSkinCache.LoadCount(basePath + "empty.png") +
                ThemeSkinCache.LoadCount(basePath + "full.png") >= 1,
                folder + " Recycle Bin skin was not loaded by the shared cache.");
        }

        private static void VerifyWeatherIconPack(
            string root, string packName)
        {
            string directory = Path.Combine(
                root, "Assets", "WeatherIconPacks", packName);
            Require(Directory.Exists(directory),
                packName + " weather icon pack is missing.");
            for (int icon = 1; icon <= 44; icon++)
            {
                string path = Path.Combine(directory, icon + ".png");
                Require(File.Exists(path),
                    packName + " weather icon " + icon + " is missing.");
                using (Image image = Image.FromFile(path))
                    Require(image.Width == 256 && image.Height == 256,
                        packName + " weather icon " + icon +
                        " does not use the 256 x 256 standard canvas.");
            }
        }

        private static void VerifyClockAndNatureArtwork(string root)
        {
            var expected = new Dictionary<string, Size>
            {
                { @"Industrial\industrial-hour-hand.png", new Size(64, 384) },
                { @"Industrial\industrial-minute-hand.png", new Size(40, 512) },
                { @"Industrial\industrial-second-hand.png", new Size(12, 560) },
                { @"BotanicalNature\botanical-hour-hand.png", new Size(64, 384) },
                { @"BotanicalNature\botanical-minute-hand.png", new Size(40, 512) },
                { @"BotanicalNature\botanical-second-hand.png", new Size(12, 560) },
                { @"BotanicalNature\botanical-dock-skin.png", new Size(1200, 200) },
                { @"WoodlandNature\woodland-hour-hand.png", new Size(64, 384) },
                { @"WoodlandNature\woodland-minute-hand.png", new Size(40, 512) },
                { @"WoodlandNature\woodland-second-hand.png", new Size(12, 560) }
            };
            foreach (KeyValuePair<string, Size> item in expected)
            {
                string path = Path.Combine(root, "Assets", "Themes", item.Key);
                Require(File.Exists(path), item.Key + " is missing.");
                using (var image = new Bitmap(path))
                {
                    Require(image.Size == item.Value,
                        item.Key + " uses the wrong production canvas.");
                    Require(image.GetPixel(0, 0).A == 0,
                        item.Key + " lost its transparent canvas.");
                }
            }
        }

        private static void VerifySlidePanelComponent()
        {
            var relative = new SlidePanel
            {
                ParentBounds = new RectangleF(100, 80, 500, 300),
                CompositionBounds = new RectangleF(0, 0, 800, 500),
                OpenDirection = SlidePanelOpenDirection.Left
            };
            Require(Math.Abs(relative.RelativePanelSize - .80F) < .001F,
                "SlidePanel default relative size is not 80%.");
            relative.FitRelativePanel(2F, 24F);
            Require(relative.PanelBounds == new RectangleF(100, 110, 400, 240) &&
                Math.Abs(relative.SlideOffset - 376F) < .001F,
                "SlidePanel relative fitting, centering, aspect, or automatic offset changed.");
            foreach (SlidePanelOpenDirection direction in Enum.GetValues(
                typeof(SlidePanelOpenDirection)))
            {
                var panel = new SlidePanel
                {
                    CompositionBounds = new RectangleF(0, 0, 1000, 700),
                    ParentBounds = new RectangleF(300, 200, 400, 300),
                    PanelBounds = new RectangleF(300, 250, 180, 180),
                    OpenDirection = direction,
                    SlideOffset = 125F,
                    Duration = TimeSpan.Zero,
                    ClipToParent = true,
                    Scale = 1F
                };
                Require(panel.IsCollapsed &&
                    !panel.PanelAcceptsInput(new PointF(320, 270)),
                    "Collapsed SlidePanel intercepted input.");
                RectangleF closed = panel.CalculateLayout().PanelBounds;
                for (int cycle = 0; cycle < 8; cycle++)
                {
                    panel.Toggle(DateTime.UtcNow);
                    RectangleF opened = panel.CalculateLayout().PanelBounds;
                    float dx = opened.X - closed.X;
                    float dy = opened.Y - closed.Y;
                    Require(direction == SlidePanelOpenDirection.Left ? dx == -125F && dy == 0F :
                        direction == SlidePanelOpenDirection.Right ? dx == 125F && dy == 0F :
                        direction == SlidePanelOpenDirection.Top ? dy == -125F && dx == 0F :
                        dy == 125F && dx == 0F,
                        "SlidePanel direction or SlideOffset semantics changed.");
                    Require(!panel.CalculateLayout().RevealClip.IsEmpty,
                        "Open SlidePanel had no reveal clip.");
                    panel.Toggle(DateTime.UtcNow);
                    Require(panel.IsCollapsed &&
                        panel.CalculateLayout().RevealClip.IsEmpty,
                        "SlidePanel did not complete a repeated collapse.");
                }
            }

            var smooth = new SlidePanel
            {
                CompositionBounds = new RectangleF(0, 0, 800, 500),
                ParentBounds = new RectangleF(300, 100, 400, 300),
                PanelBounds = new RectangleF(300, 150, 200, 200),
                OpenDirection = SlidePanelOpenDirection.Left,
                SlideOffset = 160F,
                Duration = TimeSpan.FromMilliseconds(400),
                Easing = SlidePanelEasing.SmoothEaseInOut
            };
            DateTime start = DateTime.UtcNow;
            smooth.SetSlided(true, start);
            smooth.Advance(start.AddMilliseconds(200));
            Require(Math.Abs(smooth.Progress - .5F) < .001F,
                "SlidePanel smooth ease-in-out midpoint is not deterministic.");
            smooth.Advance(start.AddMilliseconds(400));
            Require(smooth.Progress == 1F && !smooth.IsAnimating,
                "SlidePanel smooth animation did not complete exactly.");
            foreach (float scale in new[] { .5F, .75F, 1F, 1.25F, 1.5F, 1.75F, 2F })
            {
                smooth.Scale = scale;
                Require(smooth.Scale == scale && smooth.CalculateLayout().ParentBounds ==
                    new RectangleF(300, 100, 400, 300),
                    "SlidePanel runtime scale changed fixed composition geometry.");
            }
            var order = new List<string>();
            using (var bitmap = new Bitmap(800, 500))
            using (Graphics graphics = Graphics.FromImage(bitmap))
                SlidePanelRenderer.Render(graphics, smooth,
                    delegate { order.Add("panel"); },
                    delegate { order.Add("parent"); });
            Require(order.SequenceEqual(new[] { "panel", "parent" }),
                "SlidePanel renderer did not paint the parent above the child layer.");
        }

        private static IWidget Load(
            string root, string projectName, string typeName)
        {
            string path = Path.Combine(
                root,
                "src",
                "XWidgetReborn.Widgets." + projectName,
                "bin",
                "Release",
                "XWidgetReborn.Widgets." + projectName + ".dll");
            return (IWidget)Assembly.LoadFrom(path).CreateInstance(typeName);
        }

        private static void VerifyWidget(IWidget widget, string[] names)
        {
            var host = new TestHost();
            if (widget.Name == "Weather")
                host.SetSetting("weather.panelAnimation", "Instant");
            ((IOfficialWidget)widget).AttachHost(host);
            IWidgetThemeProvider provider = (IWidgetThemeProvider)widget;
            if (widget.Name == "Weather")
            {
                VerifyDesignerPanelStartupState(widget);
                VerifyWeatherAnimationPersistence(widget, host);
            }
            // Imported themes are valid additions. Stage 5 only needs to prove
            // that every required built-in theme remains available; an
            // already-installed package such as Ember Glow must not make a
            // clean source build fail merely because it adds an eighth theme.
            string[] exposedThemes = provider.Themes.ToArray();
            Require(names.All(expected => exposedThemes.Contains(
                    expected, StringComparer.OrdinalIgnoreCase)),
                widget.Name +
                " does not expose every required built-in theme.");
            foreach (string name in names)
            {
                provider.Theme = name;
                Require(provider.Theme == name,
                    widget.Name + " did not retain theme " + name + ".");
                Require(host.GetSetting("appearance", string.Empty) == name,
                    widget.Name + " did not persist theme " + name + ".");
                if (widget.Name == "Weather" && name == "Modern")
                    VerifyDefaultWeatherGeometry(widget, host);
                if (name == "Art Deco")
                {
                    Require(host.WindowShape == WidgetWindowShape.AlphaRectangle,
                        widget.Name + " did not request a per-pixel-alpha window.");
                    Size expected = widget.Name == "Clock"
                        ? new Size(360, 360)
                        : widget.Name == "Calendar"
                            ? new Size(566, 390)
                            : widget.Name == "Recycle Bin"
                                ? widget.DefaultSize
                                : new Size(1081, 494);
                    Require(host.PreferredSize == expected,
                        widget.Name + " did not use its Art Deco asset aspect ratio.");
                    if (widget.Name == "Calendar")
                    {
                        Require(host.FixedCompositionCount > 0 &&
                            host.FixedCompositionBounds ==
                                new Rectangle(7, 28, 550, 336),
                            "Art Deco Calendar did not anchor its visible artwork for edge snapping.");
                    }
                    if (widget.Name == "Weather")
                        VerifyArtDecoWeatherToggle(widget, host);
                }
                if (name == "Industrial")
                {
                    Require(host.WindowShape == WidgetWindowShape.AlphaRectangle,
                        widget.Name + " did not request an Industrial alpha window.");
                    Size expected = widget.Name == "Clock"
                        ? new Size(360, 360)
                        : widget.Name == "Calendar"
                            ? new Size(548, 382)
                            : widget.Name == "Recycle Bin"
                                ? widget.DefaultSize
                                : new Size(1081, 494);
                    Require(host.PreferredSize == expected,
                        widget.Name + " Industrial baseline footprint changed.");
                    if (widget.Name == "Calendar")
                    {
                        Require(host.FixedCompositionCount > 0 &&
                            host.FixedCompositionBounds ==
                                new Rectangle(21, 46, 508, 294),
                            "Industrial Calendar did not match the visible Weather footprint or anchor its artwork for edge snapping.");
                        VerifyIndustrialCalendarProductionDefaults(widget);
                    }
                    if (widget.Name == "Weather")
                        VerifyIndustrialWeatherToggle(widget, host);
                }
                if (name == "Woodland Nature")
                {
                    Require(host.WindowShape == WidgetWindowShape.AlphaRectangle,
                        widget.Name + " did not request a Woodland alpha window.");
                    Size expected = widget.Name == "Clock"
                        ? new Size(360, 360)
                        : widget.Name == "Calendar"
                            ? new Size(548, 382)
                            : widget.Name == "Recycle Bin"
                                ? widget.DefaultSize
                                // The v98 presentation-only enlargement matches
                                // the calendar's visible frame at 100% scale.
                                : new Size(1141, 524);
                    Require(host.PreferredSize == expected,
                        widget.Name + " Woodland preferred size was " +
                        host.PreferredSize + "; expected " + expected + ".");
                }
                if (name == "Botanical Nature")
                {
                    Require(host.WindowShape == WidgetWindowShape.AlphaRectangle,
                        widget.Name + " did not request a Botanical alpha window.");
                    Size expected = widget.Name == "Clock"
                        ? new Size(360, 360)
                        : widget.Name == "Calendar"
                            ? new Size(548, 382)
                            : widget.Name == "Recycle Bin"
                                ? widget.DefaultSize
                                : new Size(1081, 494);
                    Require(host.PreferredSize == expected,
                        widget.Name + " Botanical baseline footprint changed.");
                }
                foreach (float scale in new[] { .5F, .75F, 1F, 1.25F, 1.5F, 1.75F, 2F })
                {
                    Size baseline = name == "Art Deco" || name == "Industrial" ||
                        name == "Woodland Nature" || name == "Botanical Nature"
                        ? host.PreferredSize
                        : widget.DefaultSize;
                    int width = Math.Max(1, (int)Math.Round(
                        baseline.Width * scale));
                    int height = Math.Max(1, (int)Math.Round(
                        baseline.Height * scale));
                    using (var bitmap = new Bitmap(width, height))
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        widget.Render(graphics, new Rectangle(0, 0, width, height));
                        if (widget.Name == "Weather")
                            Require(host.GetSetting(
                                "weather.panelAnimation", string.Empty) == "Instant",
                                "Weather rendering overwrote the user's animation choice.");
                        Require(provider.Theme == name,
                            widget.Name + " fell back while rendering " + name + ".");
                        if (name == "Art Deco" || name == "Industrial" ||
                            name == "Botanical Nature")
                        {
                            Require(bitmap.GetPixel(0, 0).A == 0 &&
                                bitmap.GetPixel(width - 1, height - 1).A == 0,
                                widget.Name + " painted opaque " + name + " corners.");
                        }
                        string previewDirectory = Environment.GetEnvironmentVariable(
                            "EMILYDESK_ARTDECO_PREVIEW_DIR");
                        if (name == "Art Deco" && Math.Abs(scale - 1F) < .01F &&
                            !string.IsNullOrWhiteSpace(previewDirectory))
                        {
                            Directory.CreateDirectory(previewDirectory);
                            bitmap.Save(Path.Combine(previewDirectory,
                                widget.Name.ToLowerInvariant() + ".png"));
                        }
                        string industrialPreviewDirectory =
                            Environment.GetEnvironmentVariable(
                                "EMILYDESK_INDUSTRIAL_PREVIEW_DIR");
                        if (name == "Industrial" && Math.Abs(scale - 1F) < .01F &&
                            !string.IsNullOrWhiteSpace(industrialPreviewDirectory))
                        {
                            Directory.CreateDirectory(industrialPreviewDirectory);
                            bitmap.Save(Path.Combine(industrialPreviewDirectory,
                                widget.Name.ToLowerInvariant() + ".png"));
                        }
                    }
                }
            }
        }

        private static void VerifyDesignerPanelStartupState(IWidget widget)
        {
            MethodInfo method = widget.GetType().GetMethod(
                "DesignerPanelStartsOpen",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(method != null,
                "Weather does not expose a deterministic Designer panel startup policy.");
            Type settingsType = method.GetParameters()[0].ParameterType;
            object settings = Activator.CreateInstance(settingsType);
            PropertyInfo state = settingsType.GetProperty("State");
            Require(state != null,
                "Weather Designer panel startup state is unavailable.");
            state.SetValue(settings, 1, null);
            Require(!(bool)method.Invoke(null, new[] { settings }),
                "Weather used the Designer preview state to open its panel at startup.");
            state.SetValue(settings, 0, null);
            Require(!(bool)method.Invoke(null, new[] { settings }),
                "Weather did not start with its details panel collapsed.");
        }

        private static void VerifyWeatherAnimationPersistence(
            IWidget widget, TestHost host)
        {
            MethodInfo setMode = widget.GetType().GetMethod(
                "SetAnimationMode",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo mode = widget.GetType().GetField(
                "_animationMode",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Require(setMode != null && mode != null,
                "Weather animation controls are unavailable.");
            Require(string.Equals((string)mode.GetValue(widget), "Instant",
                    StringComparison.Ordinal),
                "Weather did not restore its saved animation choice.");
            setMode.Invoke(widget, new object[] { "Quick" });
            Require(host.GetSetting("weather.panelAnimation", string.Empty) == "Quick",
                "Weather did not persist the Quick animation choice.");
            setMode.Invoke(widget, new object[] { "Instant" });
            Require(host.GetSetting("weather.panelAnimation", string.Empty) == "Instant",
                "Weather did not persist the Instant animation choice.");
        }

        private static void VerifyIndustrialCalendarProductionDefaults(
            IWidget widget)
        {
            string previous = Environment.GetEnvironmentVariable(
                "EMILYDESK_IGNORE_DESIGNER_LAYOUT");
            Environment.SetEnvironmentVariable(
                "EMILYDESK_IGNORE_DESIGNER_LAYOUT", "1");
            try
            {
                Require(CalendarBounds(widget, "PreviousBounds") ==
                        new Rectangle(52, 70, 38, 30) &&
                    CalendarBounds(widget, "NextBounds") ==
                        new Rectangle(450, 70, 38, 30) &&
                    CalendarBounds(widget, "TodayBounds") ==
                        new Rectangle(410, 300, 78, 18),
                    "Industrial Calendar production control alignment changed.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "EMILYDESK_IGNORE_DESIGNER_LAYOUT", previous);
            }
        }

        private static Rectangle CalendarBounds(IWidget widget, string name)
        {
            MethodInfo method = widget.GetType().GetMethod(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Require(method != null,
                "Calendar production bounds are unavailable for " + name + ".");
            return (Rectangle)method.Invoke(widget, null);
        }

        private static void VerifyIndustrialWeatherToggle(
            IWidget widget, TestHost host)
        {
            FieldInfo slideField = widget.GetType().GetField(
                "_industrialSlidePanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var slide = (SlidePanel)slideField.GetValue(widget);
            MethodInfo visibleMethod = widget.GetType().GetMethod(
                "IndustrialMainVisibleBounds",
                BindingFlags.NonPublic | BindingFlags.Static);
            RectangleF mainVisible = (RectangleF)visibleMethod.Invoke(
                null, new object[] { slide.ParentBounds });
            Require(slide.CompositionBounds == new RectangleF(0, 0, 1441, 659) &&
                slide.ParentBounds == new RectangleF(691F, 79.5F, 750F, 500F) &&
                Math.Abs(slide.PanelBounds.Width - mainVisible.Width * .80F) < .01F &&
                Math.Abs(slide.PanelBounds.Height - mainVisible.Height * .80F) < .01F &&
                slide.OpenDirection == SlidePanelOpenDirection.Left &&
                Math.Abs(slide.RelativePanelSize - .80F) < .001F &&
                Math.Abs(slide.SlideOffset -
                    (slide.PanelBounds.Width - 52F)) < .01F,
                "Industrial Weather fixed SlidePanel configuration changed.");
            Require(slide.ParentBounds.Contains(slide.PanelBounds),
                "Industrial panel cannot be fully concealed by its parent artwork.");
            foreach (float userScale in new[]
                { .5F, .75F, 1F, 1.25F, 1.5F, 1.75F, 2F })
            {
                float surfaceX = 1081F / 1441F * userScale;
                float surfaceY = 494F / 659F * userScale;
                float displayedMainWidth = mainVisible.Width * surfaceX;
                float displayedMainHeight = mainVisible.Height * surfaceY;
                float displayedPanelWidth = slide.PanelBounds.Width * surfaceX;
                float displayedPanelHeight = slide.PanelBounds.Height * surfaceY;
                Require(Math.Abs(displayedPanelWidth - displayedMainWidth * .80F) <= 1F &&
                    Math.Abs(displayedPanelHeight - displayedMainHeight * .80F) <= 1F &&
                    Math.Abs(displayedPanelWidth / displayedMainWidth - .80F) < .001F &&
                    Math.Abs(displayedPanelHeight / displayedMainHeight - .80F) < .001F,
                    "Industrial Weather geometry double-scaled at " +
                    userScale.ToString("0.00") + "x.");
            }
            Size fixedSize = host.PreferredSize;
            RectangleF fixedMain = slide.ParentBounds;
            for (int cycle = 0; cycle < 8; cycle++)
            {
                ToggleThroughCurrentHotspot(widget, fixedSize);
                Require(slide.IsSlided && host.PreferredSize == fixedSize &&
                    slide.ParentBounds == fixedMain,
                    "Industrial SlidePanel moved or resized its parent while opening.");
                ToggleThroughCurrentHotspot(widget, fixedSize);
                Require(slide.IsCollapsed && host.PreferredSize == fixedSize &&
                    slide.ParentBounds == fixedMain,
                    "Industrial SlidePanel failed a repeated close.");
            }
            Require(!slide.PanelAcceptsInput(new PointF(
                slide.PanelBounds.X + 1F, slide.PanelBounds.Y + 1F)),
                "Collapsed Industrial SlidePanel intercepted input.");
        }

        private static void VerifyArtDecoWeatherToggle(
            IWidget widget, TestHost host)
        {
            FieldInfo slideField = widget.GetType().GetField(
                "_artDecoSlidePanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var slide = (SlidePanel)slideField.GetValue(widget);
            Require(slide.CompositionBounds == new RectangleF(0, 0, 1441, 659) &&
                slide.ParentBounds.Width == 755F &&
                Math.Abs(slide.ParentBounds.Height - 520.5F) < .01F &&
                Math.Abs(slide.PanelBounds.Width - 577.18F) < .01F &&
                Math.Abs(slide.PanelBounds.Height - 372.73F) < .01F &&
                slide.OpenDirection == SlidePanelOpenDirection.Left &&
                Math.Abs(slide.SlideOffset - 454.35F) < .01F,
                "Art Deco Weather does not use the approved fixed SlidePanel configuration.");
            foreach (float userScale in new[]
                { .5F, .75F, 1F, 1.25F, 1.5F, 1.75F, 2F })
            {
                float mainWidth = slide.ParentBounds.Width * userScale;
                float mainHeight = slide.ParentBounds.Height * userScale;
                float panelWidth = slide.PanelBounds.Width * userScale;
                float panelHeight = slide.PanelBounds.Height * userScale;
                Require(Math.Abs(panelWidth / mainWidth - .7645F) < .001F &&
                    Math.Abs(panelHeight / mainHeight - .7161F) < .001F &&
                    Math.Abs(slide.SlideOffset * userScale / userScale - 454.35F) < .001F,
                    "Art Deco panel/main proportions or SlideOffset double-scaled at " +
                    userScale.ToString("0.00") + "x.");
            }
            Require(slide.ParentBounds.Contains(slide.PanelBounds),
                "Enlarged Art Deco panel cannot be fully concealed by the main layer.");
            MethodInfo safeMethod = widget.GetType().GetMethod(
                "ArtDecoWeatherDetailsSafeBounds",
                BindingFlags.NonPublic | BindingFlags.Static);
            RectangleF safeBounds = (RectangleF)safeMethod.Invoke(
                null, new object[] { slide.PanelBounds });
            Require(slide.PanelBounds.Contains(safeBounds),
                "Weather Details safe area crosses the panel border.");
            for (int detailIndex = 0; detailIndex < 8; detailIndex++)
            {
                Require(safeBounds.Contains(WeatherRectangle(widget,
                        "WeatherDetailsLabelRectangle", safeBounds, detailIndex)) &&
                    safeBounds.Contains(WeatherRectangle(widget,
                        "WeatherDetailsValueRectangle", safeBounds, detailIndex)),
                    "Enlarged Weather Details text crosses its safe frame.");
            }
            Size fixedSize = host.PreferredSize;
            RectangleF fixedMain = slide.ParentBounds;
            for (int cycle = 0; cycle < 8; cycle++)
            {
                ToggleThroughCurrentHotspot(widget, fixedSize);
                Require(host.PreferredSize == fixedSize &&
                    slide.ParentBounds == fixedMain && slide.IsSlided,
                    "Art Deco SlidePanel moved or resized its parent while opening.");
                ToggleThroughCurrentHotspot(widget, fixedSize);
                Require(host.PreferredSize == fixedSize &&
                    slide.ParentBounds == fixedMain && slide.IsCollapsed,
                    "Art Deco SlidePanel moved or resized its parent while closing.");
            }
            Require(!slide.PanelAcceptsInput(new PointF(
                slide.PanelBounds.X + 1F, slide.PanelBounds.Y + 1F)),
                "Collapsed Art Deco SlidePanel intercepted input.");
            return;
#pragma warning disable 0162
            object expandedLayout = WeatherLayout(widget, 1F);
            RectangleF main = LayoutRectangle(expandedLayout, "MainRectangle");
            RectangleF panel = LayoutRectangle(expandedLayout, "PanelRectangle");
            RectangleF clip = LayoutRectangle(expandedLayout, "ClipRectangle");
            RectangleF mainVisible = LayoutRectangle(
                expandedLayout, "MainVisibleAlphaRectangle");
            RectangleF panelVisible = LayoutRectangle(
                expandedLayout, "PanelVisibleAlphaRectangle");
            RectangleF safe = LayoutRectangle(
                expandedLayout, "SafeContentRectangle");
            SizeF composite = LayoutSize(expandedLayout, "CompositeSize");
            float overlap = LayoutFloat(expandedLayout, "VisibleOverlap");
            float canvasOverlap = LayoutFloat(expandedLayout, "CanvasOverlap");
            Require(string.Equals(LayoutValue(expandedLayout, "Direction").ToString(),
                "Left", StringComparison.Ordinal),
                "Art Deco Weather did not use the shared left-opening layout.");
            Require(Math.Abs(panel.Right - main.Left - canvasOverlap) < .1F,
                "Art Deco Weather panel is not attached beneath the main frame.");
            Require(Math.Abs(clip.Right - (main.Left + canvasOverlap)) < .1F,
                "Art Deco Weather clip does not use the shared overlap boundary.");
            Require(overlap >= 30F && overlap <= 34F,
                "Art Deco Weather overlap is not alpha-inset aware.");
            Require(Math.Abs(
                (panelVisible.Right - mainVisible.Left) - overlap) <= .2F,
                "Art Deco Weather visible frame edges still contain a gap.");
            Require(Math.Abs(
                (panelVisible.Top - mainVisible.Top) - 11F) <= .2F &&
                Math.Abs(
                (mainVisible.Bottom - panelVisible.Bottom) - 11F) <= .2F,
                "Art Deco Weather panel protrudes beyond the main visible frame height.");
            object collapsedLayout = WeatherLayout(widget, 0F);
            RectangleF collapsedMainVisible = LayoutRectangle(
                collapsedLayout, "MainVisibleAlphaRectangle");
            RectangleF collapsedPanelVisible = LayoutRectangle(
                collapsedLayout, "PanelVisibleAlphaRectangle");
            Require(collapsedMainVisible.Contains(collapsedPanelVisible),
                "Collapsed Art Deco panel alpha extends outside the main visible frame.");
            Require(safe.Left > panelVisible.Left + 18F &&
                safe.Top >= panelVisible.Top + 18F &&
                panelVisible.Right - safe.Right >= 18F &&
                panelVisible.Bottom - safe.Bottom >= 18F,
                "Art Deco Weather safe content margins do not exclude the ornament and border.");
            Require(safe.Contains(WeatherRectangle(widget,
                    "WeatherDetailsTitleRectangle", safe)) &&
                safe.Contains(WeatherRectangle(widget,
                    "WeatherDetailsDividerRectangle", safe)),
                "Art Deco Weather title or divider escapes the safe content rectangle.");
            for (int detailIndex = 0; detailIndex < 8; detailIndex++)
            {
                Require(safe.Contains(WeatherRectangle(widget,
                        "WeatherDetailsLabelRectangle", safe, detailIndex)) &&
                    safe.Contains(WeatherRectangle(widget,
                        "WeatherDetailsValueRectangle", safe, detailIndex)),
                    "Art Deco Weather detail text escapes the safe content rectangle.");
                if (detailIndex / 2 < 3)
                    Require(safe.Contains(WeatherRectangle(widget,
                        "WeatherDetailsSeparatorRectangle", safe, detailIndex)),
                        "Art Deco Weather detail divider escapes the safe content rectangle.");
            }

            MethodInfo iconMethod = widget.GetType().GetMethod(
                "ArtDecoCurrentIconBounds",
                BindingFlags.NonPublic | BindingFlags.Static);
            RectangleF icon = (RectangleF)iconMethod.Invoke(null, null);
            Require(icon.Bottom <= 184F - 8F,
                "Art Deco Weather current-condition icon crosses the divider clearance.");

            for (int cycle = 0; cycle < 4; cycle++)
            {
                ToggleThroughCurrentHotspot(widget, host.PreferredSize);
                Require(host.PreferredSize == new Size(
                    (int)Math.Round(composite.Width),
                    (int)Math.Round(composite.Height)),
                    "Art Deco Weather forecast did not expand.");
                Require(host.AnchoredRightResizeCount > 0,
                    "Art Deco Weather did not use the shared anchored-resize path.");
                ToggleThroughCurrentHotspot(widget, host.PreferredSize);
                Require(host.PreferredSize == new Size(530, 367),
                    "Art Deco Weather forecast did not close.");
            }
#pragma warning restore 0162
        }

        private static void VerifyDefaultWeatherGeometry(
            IWidget widget, TestHost host)
        {
            object collapsed = WeatherLayout(widget, 0F);
            object expanded = WeatherLayout(widget, 1F);
            Require(LayoutSize(collapsed, "CompositeSize") == new SizeF(360F, 210F) &&
                LayoutSize(expanded, "CompositeSize") == new SizeF(622F, 210F),
                "Default Weather composite geometry changed.");
            RectangleF main = LayoutRectangle(expanded, "MainRectangle");
            RectangleF panel = LayoutRectangle(expanded, "PanelRectangle");
            RectangleF clip = LayoutRectangle(expanded, "ClipRectangle");
            Require(main == new RectangleF(0F, 0F, 360F, 210F) &&
                panel == new RectangleF(342F, 0F, 280F, 210F) &&
                clip == new RectangleF(342F, 0F, 280F, 210F) &&
                LayoutFloat(expanded, "VisibleOverlap") == 18F &&
                string.Equals(LayoutValue(expanded, "Direction").ToString(),
                    "Right", StringComparison.Ordinal),
                "Default Weather panel direction or overlap changed.");
            int anchoredBefore = host.AnchoredRightResizeCount;
            ToggleThroughCurrentHotspot(widget, host.PreferredSize);
            Require(host.PreferredSize == new Size(622, 210),
                "Default Weather no longer expands to its established size.");
            ToggleThroughCurrentHotspot(widget, host.PreferredSize);
            Require(host.PreferredSize == new Size(360, 210) &&
                host.AnchoredRightResizeCount == anchoredBefore,
                "Default Weather no longer closes with its left edge fixed.");
        }

        private static void ToggleThroughCurrentHotspot(IWidget widget, Size size)
        {
            using (var bitmap = new Bitmap(size.Width, size.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
                widget.Render(graphics, new Rectangle(Point.Empty, size));
            WidgetHotspot hotspot = ((IWidgetHotspotProvider)widget)
                .GetHotspots(size).First(h => h.Id == "forecast-toggle");
            Point point = new Point(
                hotspot.Bounds.Left + hotspot.Bounds.Width / 2,
                hotspot.Bounds.Top + hotspot.Bounds.Height / 2);
            var input = (IWidgetPointerInput)widget;
            Require(input.PointerDown(point, WidgetPointerButton.Left),
                "Weather toggle hotspot did not consume pointer-down.");
            input.PointerUp(point, WidgetPointerButton.Left);
        }

        private static object WeatherLayout(IWidget widget, float progress)
        {
            MethodInfo method = widget.GetType().GetMethod(
                "CalculateCompositeLayout",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return method.Invoke(widget, new object[] { progress });
        }

        private static RectangleF WeatherRectangle(
            IWidget widget, string methodName, RectangleF safe)
        {
            return WeatherRectangle(widget, methodName, safe, null);
        }

        private static RectangleF WeatherRectangle(
            IWidget widget, string methodName, RectangleF safe, int? index)
        {
            MethodInfo method = widget.GetType().GetMethod(
                methodName, BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = index.HasValue
                ? new object[] { safe, index.Value }
                : new object[] { safe };
            return (RectangleF)method.Invoke(null, arguments);
        }

        private static object LayoutValue(object layout, string property)
        {
            return layout.GetType().GetProperty(property).GetValue(layout, null);
        }

        private static RectangleF LayoutRectangle(object layout, string property)
        {
            return (RectangleF)LayoutValue(layout, property);
        }

        private static SizeF LayoutSize(object layout, string property)
        {
            return (SizeF)LayoutValue(layout, property);
        }

        private static float LayoutFloat(object layout, string property)
        {
            return (float)LayoutValue(layout, property);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private sealed class TestHost : IWidgetHostContext
        {
            private readonly Dictionary<string, string> _settings =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string GetSetting(string key, string fallback)
            {
                string value;
                return _settings.TryGetValue(key, out value) ? value : fallback;
            }
            public void SetSetting(string key, string value)
            {
                _settings[key] = value;
            }
            public double Opacity { get; set; }
            public float Scale { get; set; }
            public Size PreferredSize { get; private set; }
            public WidgetWindowShape WindowShape { get; private set; }
            public int AnchoredRightResizeCount { get; private set; }
            public int FixedCompositionCount { get; private set; }
            public Rectangle FixedCompositionBounds { get; private set; }
            public void SetPreferredSize(Size size) { PreferredSize = size; }
            public void SetPreferredSizeAnchoredRight(Size size)
            {
                PreferredSize = size;
                AnchoredRightResizeCount++;
            }
            public void SetFixedCompositionSurface(Size size, Rectangle parentBounds)
            {
                PreferredSize = size;
                FixedCompositionBounds = parentBounds;
                FixedCompositionCount++;
            }
            public void SetWindowShape(WidgetWindowShape shape) { WindowShape = shape; }
            public void Invalidate() { }
            public void ReportDiagnostic(string message) { }
        }
    }
}
