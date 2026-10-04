using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace XWidgetReborn.WidgetSdk
{
    public static class ThemeSkinCache
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Image> Images =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> Loads =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Rectangle> AlphaBounds =
            new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> FileTimes =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> NextChecks =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public static Image Get(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A skin path is required.", "relativePath");
            string resolved = EmilyDeskThemeCatalog.ResolveThemeUri(relativePath);
            string key = (resolved ?? relativePath).Replace('/', Path.DirectorySeparatorChar);
            lock (Sync)
            {
                Image image;
                string path = Path.IsPathRooted(key) ? key :
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, key);
                if (!File.Exists(path))
                {
                    string sourceTreePath = Path.GetFullPath(Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        @"..\..\..\..",
                        key));
                    if (File.Exists(sourceTreePath)) path = sourceTreePath;
                }
                if (!File.Exists(path))
                    throw new FileNotFoundException("The theme skin is missing.", path);
                if (Images.TryGetValue(key, out image))
                {
                    if (!Path.IsPathRooted(key)) return image;
                    DateTime next;
                    if (NextChecks.TryGetValue(key, out next) &&
                        DateTime.UtcNow < next) return image;
                    NextChecks[key] = DateTime.UtcNow.AddSeconds(2);
                    DateTime previous;
                    DateTime current = File.GetLastWriteTimeUtc(path);
                    if (FileTimes.TryGetValue(key, out previous) &&
                        previous == current) return image;
                }
                using (Image source = Image.FromFile(path))
                    image = new Bitmap(source);
                Images[key] = image;
                Loads[key] = LoadCount(key) + 1;
                if (Path.IsPathRooted(key))
                {
                    FileTimes[key] = File.GetLastWriteTimeUtc(path);
                    NextChecks[key] = DateTime.UtcNow.AddSeconds(2);
                    AlphaBounds.Remove(key);
                }
                return image;
            }
        }

        public static int LoadCount(string relativePath)
        {
            string resolved = EmilyDeskThemeCatalog.ResolveThemeUri(relativePath);
            string key = (resolved ?? relativePath).Replace('/', Path.DirectorySeparatorChar);
            int value;
            return Loads.TryGetValue(key, out value) ? value : 0;
        }

        public static Rectangle GetAlphaBounds(string relativePath)
        {
            string resolved = EmilyDeskThemeCatalog.ResolveThemeUri(relativePath);
            string key = (resolved ?? relativePath).Replace('/', Path.DirectorySeparatorChar);
            lock (Sync)
            {
                Rectangle cached;
                if (AlphaBounds.TryGetValue(key, out cached)) return cached;
                using (var bitmap = new Bitmap(Get(key)))
                {
                    int left = bitmap.Width;
                    int top = bitmap.Height;
                    int right = -1;
                    int bottom = -1;
                    for (int y = 0; y < bitmap.Height; y++)
                        for (int x = 0; x < bitmap.Width; x++)
                            // Ignore near-transparent export noise and soft
                            // shadow pixels. WidgetWindow uses the same alpha
                            // threshold when it finds a layered widget's
                            // visible edge, so fixed-composition widgets now
                            // snap by the real artwork edge as well.
                            if (bitmap.GetPixel(x, y).A > 8)
                            {
                                if (x < left) left = x;
                                if (y < top) top = y;
                                if (x > right) right = x;
                                if (y > bottom) bottom = y;
                            }
                    cached = right < left || bottom < top
                        ? Rectangle.Empty
                        : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
                    AlphaBounds[key] = cached;
                    return cached;
                }
            }
        }
    }
}
