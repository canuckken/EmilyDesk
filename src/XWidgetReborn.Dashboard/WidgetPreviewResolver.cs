using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal static class WidgetPreviewResolver
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, string> PathCache =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        private static readonly string[] PreferredNames =
        {
            "preview.png", "preview.jpg", "preview.jpeg",
            "thumbnail.png", "thumbnail.jpg", "thumb.png",
            "screenshot.png", "screenshot.jpg",
            "widget.png", "gallery.png", "dockpreview.png",
            "default.png", "background.png", "back.png"
        };

        public static string Resolve(
            GalleryWidgetItem item,
            Action<string, long> duration)
        {
            Stopwatch timer = Stopwatch.StartNew();
            // Compare the live Designer artwork with the packaged preview.
            // An update can install a newly generated card while an older,
            // stale Designer preview is still present in ProgramData.
            string designerPreview = ResolveDesignerPreview(item);
            string cacheKey = CacheKey(item);
            string packagedPreview = ResolveUncached(item);
            // The Ember Glow package owns the artwork shown on its gallery
            // card. An old Designer preview can have a newer timestamp after
            // an import and otherwise hide an updated package preview.
            string resolved = item.Descriptor != null &&
                !string.IsNullOrEmpty(item.Descriptor.Id) &&
                item.Descriptor.Id.StartsWith(
                    "utility.ember-glow-",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(packagedPreview) &&
                File.Exists(packagedPreview)
                    ? packagedPreview
                    : NewestExistingPreview(
                        designerPreview, packagedPreview);
            lock (Sync)
            {
                if (string.IsNullOrWhiteSpace(resolved))
                    PathCache.Remove(cacheKey);
                else
                    PathCache[cacheKey] = resolved;
            }
            ReportDuration(
                duration,
                "preview file resolution",
                timer.ElapsedMilliseconds);
            return resolved;
        }

        private static string NewestExistingPreview(
            string designerPreview,
            string packagedPreview)
        {
            bool hasDesigner = !string.IsNullOrWhiteSpace(designerPreview) &&
                File.Exists(designerPreview);
            bool hasPackaged = !string.IsNullOrWhiteSpace(packagedPreview) &&
                File.Exists(packagedPreview);
            if (!hasDesigner) return hasPackaged
                ? packagedPreview : string.Empty;
            if (!hasPackaged) return designerPreview;
            try
            {
                return File.GetLastWriteTimeUtc(designerPreview) >=
                    File.GetLastWriteTimeUtc(packagedPreview)
                    ? designerPreview : packagedPreview;
            }
            catch
            {
                return designerPreview;
            }
        }

        public static Image LoadThumbnail(
            string path,
            Size targetSize,
            Action<string, long> duration)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
                return CreatePlaceholder(targetSize);
            try
            {
                Stopwatch decode = Stopwatch.StartNew();
                using (Image source = Image.FromFile(path))
                {
                    ReportDuration(
                        duration,
                        "image decoding",
                        decode.ElapsedMilliseconds);
                    Stopwatch resize = Stopwatch.StartNew();
                    Image result = ScaleToFit(source, targetSize);
                    ReportDuration(
                        duration,
                        "image resizing",
                        resize.ElapsedMilliseconds);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    "Widget preview decode failed: Path=" +
                    path + "; Reason=" + ex.Message +
                    "; FinalFallback=W.");
                return CreatePlaceholder(targetSize);
            }
        }

        private static void ReportDuration(
            Action<string, long> duration,
            string operation,
            long milliseconds)
        {
            if (duration != null)
                duration(operation, milliseconds);
        }

        public static void Invalidate(string widgetId)
        {
            lock (Sync)
            {
                var keys = new List<string>(
                    PathCache.Keys);
                foreach (string key in keys)
                    if (key.StartsWith(
                        widgetId + "|",
                        StringComparison.OrdinalIgnoreCase))
                        PathCache.Remove(key);
            }
        }

        public static void Clear()
        {
            lock (Sync) PathCache.Clear();
        }

        private static string CacheKey(
            GalleryWidgetItem item)
        {
            return item.GalleryKey + "|" +
                (item.Descriptor.PreviewPath ??
                    string.Empty) + "|" +
                (item.Descriptor.IconPath ??
                    string.Empty);
        }

        private static string ResolveUncached(GalleryWidgetItem item)
        {
            var tested = new List<string>();
            string reason;
            string themedPreview = ResolveThemePreview(item);
            if (!string.IsNullOrWhiteSpace(themedPreview) &&
                File.Exists(themedPreview))
                return themedPreview;
            if (IsUsefulImage(
                item.Descriptor.PreviewPath,
                item.IsLegacy,
                IsNamedPreview(
                    item.Descriptor.PreviewPath),
                out reason))
                return item.Descriptor.PreviewPath;
            AddDiagnostic(
                tested,
                item.Descriptor.PreviewPath,
                reason);

            string folder = ResolveFolder(item);
            if (!string.IsNullOrWhiteSpace(folder) &&
                Directory.Exists(folder))
            {
                foreach (string name in PreferredNames)
                {
                    string candidate = FindCaseInsensitive(
                        folder,
                        name);
                    if (IsUsefulImage(
                        candidate,
                        item.IsLegacy,
                        true,
                        out reason))
                        return candidate;
                    AddDiagnostic(
                        tested,
                        candidate,
                        reason);
                }

                string representativeReason;
                string best = FindRepresentativeImage(
                    folder,
                    item.IsLegacy,
                    out representativeReason);
                if (!string.IsNullOrWhiteSpace(best))
                    return best;
                tested.Add(
                    "representative scan: " +
                    representativeReason);
            }

            if (IsUsefulImage(
                item.Descriptor.IconPath,
                item.IsLegacy,
                false,
                out reason))
                return item.Descriptor.IconPath;
            AddDiagnostic(
                tested,
                item.Descriptor.IconPath,
                reason);
            Trace.WriteLine(
                "Widget preview unresolved: Name=" +
                item.Descriptor.Name +
                "; Directory=" +
                (folder ?? string.Empty) +
                "; MetadataPreview=" +
                (item.Descriptor.PreviewPath ??
                    string.Empty) +
                "; Candidates=" +
                string.Join(
                    " | ",
                    tested.ToArray()) +
                "; FinalFallback=W.");
            return string.Empty;
        }

        private static string ResolveDesignerPreview(
            GalleryWidgetItem item)
        {
            if (item == null || item.Descriptor == null ||
                string.IsNullOrWhiteSpace(item.Theme))
                return string.Empty;
            string id = item.Descriptor.Id ?? string.Empty;
            string widget = id.EndsWith("weather",
                StringComparison.OrdinalIgnoreCase) ? "Weather" :
                id.EndsWith("clock",
                    StringComparison.OrdinalIgnoreCase) ? "Clock" :
                id.EndsWith("calendar",
                    StringComparison.OrdinalIgnoreCase) ? "Calendar" :
                string.Empty;
            if (widget.Length == 0) return string.Empty;
            string theme = item.Theme;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                theme = theme.Replace(invalid, '-');
            return Path.Combine(
                DesignerArtworkRoot(),
                widget, theme, "preview.png");
        }

        private static string DesignerArtworkRoot()
        {
            string testRoot = Environment.GetEnvironmentVariable(
                "EMILYDESK_DESIGNER_ARTWORK_ROOT");
            if (!string.IsNullOrWhiteSpace(testRoot))
                return testRoot;
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts", "Artwork");
        }

        private static string ResolveThemePreview(
            GalleryWidgetItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Theme))
                return string.Empty;
            // Optional packages carry their own complete previews. Treating
            // every Ember Glow id as a native calendar made calculator and
            // currency cards show the empty calendar cabinet.
            if (item.Descriptor != null &&
                !string.IsNullOrWhiteSpace(item.Descriptor.Id) &&
                item.Descriptor.Id.StartsWith("utility.",
                    StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string id = item.Descriptor.Id;
            string widgetName = id.EndsWith("weather", StringComparison.OrdinalIgnoreCase)
                ? "weather"
                : id.EndsWith("clock", StringComparison.OrdinalIgnoreCase)
                    ? "clock"
                    : id.EndsWith("recyclebin", StringComparison.OrdinalIgnoreCase)
                        ? "recycle-bin"
                        : "calendar";
            string generatedName = item.Theme.ToLowerInvariant()
                .Replace(" ", "-") + "-" + widgetName + ".png";
            // Ember Glow's gallery images ship with its theme. A gallery
            // preview generated by an older build may still be on disk.
            if (string.Equals(item.Theme, "Ember Glow",
                    StringComparison.OrdinalIgnoreCase))
            {
                string rendered = Path.Combine(root, "Widgets",
                    "GalleryPreviews", generatedName);
                if (File.Exists(rendered)) return rendered;
                EmilyDeskTheme emberTheme =
                    EmilyDeskThemeCatalog.Get(item.Theme);
                string galleryKey = widgetName == "recycle-bin"
                    ? "galleryRecycleBin"
                    : "gallery" + char.ToUpperInvariant(widgetName[0]) +
                        widgetName.Substring(1);
                string current = emberTheme.Asset(galleryKey);
                if (!string.IsNullOrWhiteSpace(current) &&
                    File.Exists(current)) return current;
            }
            string generated = Path.Combine(
                root,
                "Widgets",
                "GalleryPreviews",
                generatedName);
            if (File.Exists(generated))
                return generated;
            string themeFolder = string.Empty;
            string themePrefix = string.Empty;
            if (string.Equals(item.Theme, "Modern",
                    StringComparison.OrdinalIgnoreCase))
            { themeFolder = "Modern"; themePrefix = "modern"; }
            else if (string.Equals(item.Theme, "Vintage",
                    StringComparison.OrdinalIgnoreCase))
            { themeFolder = "Vintage"; themePrefix = "vintage"; }
            else if (string.Equals(item.Theme, "Industrial",
                    StringComparison.OrdinalIgnoreCase))
            { themeFolder = "Industrial"; themePrefix = "industrial"; }
            else if (string.Equals(item.Theme, "Art Deco",
                    StringComparison.OrdinalIgnoreCase))
            { themeFolder = "ArtDeco"; themePrefix = "art-deco"; }
            else if (string.Equals(item.Theme, "Steampunk",
                    StringComparison.OrdinalIgnoreCase))
            { themeFolder = "Steampunk"; themePrefix = "steampunk"; }
            else if (string.Equals(item.Theme, "Botanical Nature",
                    StringComparison.OrdinalIgnoreCase))
            { themeFolder = "BotanicalNature"; themePrefix = "botanical"; }
            else if (string.Equals(item.Theme, "Woodland Nature",
                    StringComparison.OrdinalIgnoreCase))
            { themeFolder = "WoodlandNature"; themePrefix = "woodland"; }
            if (!string.IsNullOrEmpty(themeFolder))
                return Path.Combine(root, "Assets", "Themes", themeFolder,
                    widgetName == "recycle-bin"
                        ? themePrefix + "-recycle-bin-empty.png"
                        : themePrefix + "-" + widgetName + "-skin.png");

            EmilyDeskTheme imported = EmilyDeskThemeCatalog.Get(item.Theme);
            if (imported.IsImported)
            {
                string galleryKey = widgetName == "recycle-bin"
                    ? "galleryRecycleBin"
                    : "gallery" + char.ToUpperInvariant(widgetName[0]) +
                        widgetName.Substring(1);
                string candidate = imported.Asset(galleryKey);
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    File.Exists(candidate)) return candidate;
                string assetKey = widgetName == "recycle-bin"
                    ? "recycleEmpty" : widgetName;
                candidate = imported.Asset(assetKey);
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    File.Exists(candidate)) return candidate;
            }
            return string.Empty;
        }

        private static void AddDiagnostic(
            IList<string> tested,
            string path,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            tested.Add(
                Path.GetFileName(path) +
                ": " + reason);
        }

        private static string ResolveFolder(GalleryWidgetItem item)
        {
            string path = !string.IsNullOrWhiteSpace(
                item.Descriptor.PreviewPath)
                ? item.Descriptor.PreviewPath
                : item.Descriptor.IconPath;
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try { return Path.GetDirectoryName(path); }
            catch { return string.Empty; }
        }

        private static string FindRepresentativeImage(
            string folder,
            bool legacy,
            out string reason)
        {
            string best = null;
            long bestScore = 0;
            string[] files;
            try
            {
                files = Directory.GetFiles(
                    folder,
                    "*.*",
                    SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return null;
            }

            int imageCount = 0;
            int fragmentCount = 0;
            int unsuitableCount = 0;
            foreach (string file in files)
            {
                if (!IsImageExtension(file))
                    continue;
                imageCount++;
                if (IsControlFragment(file))
                {
                    fragmentCount++;
                    continue;
                }
                try
                {
                    using (Image image = Image.FromFile(file))
                    {
                        if (legacy &&
                            !HasRepresentativeDimensions(
                                image))
                        {
                            unsuitableCount++;
                            continue;
                        }
                        if (!HasVisibleContent(image))
                        {
                            unsuitableCount++;
                            continue;
                        }
                        long score = (long)image.Width * image.Height;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = file;
                        }
                    }
                }
                catch { unsuitableCount++; }
            }
            reason = "images=" + imageCount +
                ", fragments=" + fragmentCount +
                ", unsuitable=" + unsuitableCount;
            return best;
        }

        private static bool IsUsefulImage(
            string path,
            bool requireRepresentativeSize,
            bool namedPreview,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                reason = "not provided";
                return false;
            }
            if (!File.Exists(path))
            {
                reason = "file missing";
                return false;
            }
            if (!IsImageExtension(path))
            {
                reason = "unsupported extension";
                return false;
            }
            if (IsControlFragment(path))
            {
                reason = "control fragment";
                return false;
            }
            try
            {
                using (Image image = Image.FromFile(path))
                {
                    if (requireRepresentativeSize &&
                        !(namedPreview
                            ? HasNamedPreviewDimensions(
                                image)
                            : HasRepresentativeDimensions(
                                image)))
                    {
                        reason = "dimensions " +
                            image.Width + "x" +
                            image.Height +
                            " are not representative";
                        return false;
                    }
                    if (!HasVisibleContent(image))
                    {
                        reason =
                            "transparent or blank image";
                        return false;
                    }
                    reason = "accepted";
                    return true;
                }
            }
            catch (Exception ex)
            {
                reason = "decode failed: " +
                    ex.Message;
                return false;
            }
        }

        private static bool HasNamedPreviewDimensions(
            Image image)
        {
            int shortest = Math.Min(
                image.Width,
                image.Height);
            int longest = Math.Max(
                image.Width,
                image.Height);
            return longest >= 90 &&
                shortest >= 32 &&
                (long)image.Width * image.Height >=
                    3500;
        }

        private static bool HasRepresentativeDimensions(
            Image image)
        {
            int shortest = Math.Min(
                image.Width,
                image.Height);
            int longest = Math.Max(
                image.Width,
                image.Height);
            return longest >= 120 &&
                shortest >= 40 &&
                (long)image.Width * image.Height >=
                    6000;
        }

        private static bool HasVisibleContent(Image image)
        {
            using (var bitmap = new Bitmap(image))
            {
                int visible = 0;
                const int samples = 12;
                for (int y = 0; y < samples; y++)
                    for (int x = 0; x < samples; x++)
                    {
                        int sampleX = Math.Min(
                            bitmap.Width - 1,
                            x * bitmap.Width / samples +
                                bitmap.Width /
                                    (samples * 2));
                        int sampleY = Math.Min(
                            bitmap.Height - 1,
                            y * bitmap.Height / samples +
                                bitmap.Height /
                                    (samples * 2));
                        if (bitmap.GetPixel(
                                sampleX,
                                sampleY).A > 16)
                            visible++;
                    }
                return visible >= 2;
            }
        }

        private static bool IsNamedPreview(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string name = Path.GetFileName(path);
            foreach (string preferred in PreferredNames)
                if (string.Equals(
                    name,
                    preferred,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool IsControlFragment(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path)
                .ToLowerInvariant();
            string[] rejected =
            {
                "button", "btn", "icon", "close", "hover",
                "pressed", "arrow", "hand", "needle",
                "weathericon", "condition", "control",
                "mask", "shadow", "sprite"
            };
            foreach (string value in rejected)
                if (name.Contains(value))
                    return true;
            return false;
        }

        private static bool IsImageExtension(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".png",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpg",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".bmp",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".gif",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string FindCaseInsensitive(
            string folder,
            string name)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folder))
                    if (string.Equals(
                        Path.GetFileName(file),
                        name,
                        StringComparison.OrdinalIgnoreCase))
                        return file;
            }
            catch { }
            return null;
        }

        private static Image ScaleToFit(
            Image source,
            Size target)
        {
            var result = new Bitmap(
                Math.Max(1, target.Width),
                Math.Max(1, target.Height));
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.FromArgb(238, 241, 245));
                graphics.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode
                        .HighQualityBicubic;
                float scale = Math.Min(
                    (float)target.Width / source.Width,
                    (float)target.Height / source.Height);
                int width = Math.Max(
                    1,
                    (int)(source.Width * scale));
                int height = Math.Max(
                    1,
                    (int)(source.Height * scale));
                int x = (target.Width - width) / 2;
                int y = (target.Height - height) / 2;
                graphics.DrawImage(
                    source,
                    new Rectangle(x, y, width, height));
            }
            return result;
        }

        private static Image CreatePlaceholder(Size size)
        {
            var image = new Bitmap(
                Math.Max(1, size.Width),
                Math.Max(1, size.Height));
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(232, 236, 242));
                using (var brush = new SolidBrush(
                    Color.FromArgb(96, 112, 132)))
                using (var font = new Font(
                    "Segoe UI",
                    26F,
                    FontStyle.Regular,
                    GraphicsUnit.Pixel))
                {
                    const string text = "W";
                    SizeF measured = graphics.MeasureString(text, font);
                    graphics.DrawString(
                        text,
                        font,
                        brush,
                        (size.Width - measured.Width) / 2,
                        (size.Height - measured.Height) / 2);
                }
            }
            return image;
        }
    }
}
