using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace XWidgetReborn.WidgetSdk
{
    public sealed class EmilyDeskTheme
    {
        internal EmilyDeskTheme(string id, string name, Color surfaceTop,
            Color surfaceBottom, Color primaryText, Color secondaryText,
            Color accent, Color border, Color weekend, string wallpaperFileName,
            string packageRoot, IDictionary<string, string> assets,
            IDictionary<string, string> layouts)
        {
            Id = id; Name = name; SurfaceTop = surfaceTop; SurfaceBottom = surfaceBottom;
            PrimaryText = primaryText; SecondaryText = secondaryText; Accent = accent;
            Border = border; Weekend = weekend; WallpaperFileName = wallpaperFileName;
            PackageRoot = packageRoot;
            Assets = assets ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Layouts = layouts ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public Color SurfaceTop { get; private set; }
        public Color SurfaceBottom { get; private set; }
        public Color PrimaryText { get; private set; }
        public Color SecondaryText { get; private set; }
        public Color Accent { get; private set; }
        public Color Border { get; private set; }
        public Color Weekend { get; private set; }
        public string WallpaperFileName { get; private set; }
        public string PackageRoot { get; private set; }
        public IDictionary<string, string> Assets { get; private set; }
        public IDictionary<string, string> Layouts { get; private set; }
        public bool IsImported { get { return !string.IsNullOrEmpty(PackageRoot); } }

        public string Asset(string key)
        {
            string relative;
            return Assets.TryGetValue(key ?? string.Empty, out relative)
                ? EmilyDeskThemeCatalog.ResolvePackagePath(PackageRoot, relative) : null;
        }

        public string Layout(string kind)
        {
            string relative;
            return Layouts.TryGetValue(kind ?? string.Empty, out relative)
                ? EmilyDeskThemeCatalog.ResolvePackagePath(PackageRoot, relative) : null;
        }
    }

    public static class EmilyDeskThemeCatalog
    {
        private static readonly object ImportSync = new object();
        private static EmilyDeskTheme[] ImportedCache = new EmilyDeskTheme[0];
        private static DateTime NextImportScanUtc = DateTime.MinValue;
        private sealed class ThemeManifest
        {
            public string id { get; set; }
            public string name { get; set; }
            public string version { get; set; }
            public string surfaceTop { get; set; }
            public string surfaceBottom { get; set; }
            public string primaryText { get; set; }
            public string secondaryText { get; set; }
            public string accent { get; set; }
            public string border { get; set; }
            public string weekend { get; set; }
            public string wallpaper { get; set; }
            public Dictionary<string, string> assets { get; set; }
            public Dictionary<string, string> layouts { get; set; }
        }

        private static readonly EmilyDeskTheme[] BuiltIns =
        {
            Theme("modern", "Modern", 31,42,56, 18,24,33, 255,255,255, 190,205,220, 54,149,226, 70,124,176, 243,125,137, "EmilyDesk_Modern_Wallpaper_1_3840x2160.png"),
            Theme("vintage", "Vintage", 239,224,190, 205,180,135, 58,42,25, 105,79,47, 168,116,45, 132,94,42, 151,73,68, "EmilyDesk_Vintage_Wallpaper_1_3840x2160.png"),
            Theme("steampunk", "Steampunk", 125,90,40, 43,34,24, 250,225,177, 205,172,119, 214,158,58, 105,71,30, 235,151,98, "EmilyDesk_Steampunk_Wallpaper_1_3840x2160.png"),
            Theme("industrial", "Industrial", 83,86,86, 29,31,31, 240,235,220, 183,181,169, 230,165,55, 112,114,111, 230,165,55, "EmilyDesk_Industrial_Wallpaper_1_3840x2160.png"),
            Theme("art-deco", "Art Deco", 27,27,25, 7,9,9, 245,230,196, 200,178,126, 40,143,135, 191,151,73, 40,143,135, "EmilyDesk_Art_Deco_Wallpaper_1_3840x2160.png"),
            Theme("botanical", "Botanical Nature", 245,240,226, 217,221,197, 37,63,51, 99,117,91, 210,126,139, 139,151,116, 205,105,126, "EmilyDesk_Botanical_Nature_Wallpaper_1_3840x2160.png"),
            Theme("woodland", "Woodland Nature", 53,67,53, 18,34,28, 242,226,191, 180,160,122, 184,105,55, 105,75,52, 204,139,83, "EmilyDesk_Woodland_Nature_Wallpaper_1_3840x2160.png")
        };

        private static EmilyDeskTheme Theme(string id, string name,
            int r1,int g1,int b1, int r2,int g2,int b2,
            int rt,int gt,int bt, int rs,int gs,int bs,
            int ra,int ga,int ba, int rb,int gb,int bb,
            int rw,int gw,int bw, string wallpaper)
        {
            return new EmilyDeskTheme(id, name, Color.FromArgb(r1,g1,b1),
                Color.FromArgb(r2,g2,b2), Color.FromArgb(rt,gt,bt),
                Color.FromArgb(rs,gs,bs), Color.FromArgb(ra,ga,ba),
                Color.FromArgb(rb,gb,bb), Color.FromArgb(rw,gw,bw),
                wallpaper, null, null, null);
        }

        public static string ThemesRoot
        {
            get
            {
                string buildThemes = Environment.GetEnvironmentVariable(
                    "EMILYDESK_THEME_ROOT");
                if (!string.IsNullOrWhiteSpace(buildThemes))
                    return Path.GetFullPath(buildThemes);
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk", "Themes");
            }
        }

        public static IEnumerable<string> Names
        {
            get { foreach (EmilyDeskTheme item in All()) yield return item.Name; }
        }

        public static IEnumerable<string> BuiltInNames
        {
            get { foreach (EmilyDeskTheme item in BuiltIns) yield return item.Name; }
        }

        public static IEnumerable<EmilyDeskTheme> All()
        {
            foreach (EmilyDeskTheme item in BuiltIns) yield return item;
            foreach (EmilyDeskTheme item in Imported()) yield return item;
        }

        public static EmilyDeskTheme Get(string value)
        {
            foreach (EmilyDeskTheme item in All())
                if (string.Equals(value, item.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, item.Id, StringComparison.OrdinalIgnoreCase)) return item;
            string normalized = NormalizeLegacy(value);
            foreach (EmilyDeskTheme item in BuiltIns) if (item.Name == normalized) return item;
            return BuiltIns[0];
        }

        public static string Normalize(string value) { return Get(value).Name; }

        public static string ThemeLayoutPath(string theme, string kind)
        {
            EmilyDeskTheme item = Get(theme);
            return item.IsImported ? item.Layout(kind) : null;
        }

        public static void Refresh()
        {
            lock (ImportSync) NextImportScanUtc = DateTime.MinValue;
        }

        public static string ResolveThemeUri(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("theme://",
                StringComparison.OrdinalIgnoreCase)) return value;
            string remainder = value.Substring("theme://".Length).Replace('\\', '/');
            int slash = remainder.IndexOf('/');
            if (slash <= 0 || slash == remainder.Length - 1) return null;
            EmilyDeskTheme theme = Get(remainder.Substring(0, slash));
            return theme.IsImported ? ResolvePackagePath(theme.PackageRoot,
                remainder.Substring(slash + 1)) : null;
        }

        internal static string ResolvePackagePath(string root, string relative)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrWhiteSpace(relative) ||
                Path.IsPathRooted(relative)) return null;
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            return full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        private static IEnumerable<EmilyDeskTheme> Imported()
        {
            lock (ImportSync)
            {
                if (DateTime.UtcNow < NextImportScanUtc) return ImportedCache;
                NextImportScanUtc = DateTime.UtcNow.AddSeconds(2);
                var result = new List<EmilyDeskTheme>();
                if (Directory.Exists(ThemesRoot))
                {
                    string[] directories;
                    try { directories = Directory.GetDirectories(ThemesRoot); }
                    catch { directories = new string[0]; }
                    Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (EmilyDeskTheme builtIn in BuiltIns) seen.Add(builtIn.Name);
                    foreach (string directory in directories)
                    {
                        if (Path.GetFileName(directory).StartsWith(".",
                            StringComparison.Ordinal)) continue;
                        string path = Path.Combine(directory, "theme.json");
                        if (!File.Exists(path)) continue;
                        EmilyDeskTheme theme = LoadImported(path);
                        if (theme != null && !seen.Contains(theme.Name))
                        { seen.Add(theme.Name); result.Add(theme); }
                    }
                }
                ImportedCache = result.ToArray();
                return ImportedCache;
            }
        }

        private static EmilyDeskTheme LoadImported(string manifestPath)
        {
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
                ThemeManifest manifest = serializer.Deserialize<ThemeManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.name) ||
                    !Regex.IsMatch(manifest.id ?? string.Empty, "^[a-z0-9][a-z0-9-]{1,63}$")) return null;
                string root = Path.GetDirectoryName(manifestPath);
                if (manifest.assets == null || string.IsNullOrEmpty(
                    ResolvePackagePath(root, Value(manifest.assets, "weather")))) return null;
                return new EmilyDeskTheme(manifest.id, manifest.name.Trim(),
                    ParseColor(manifest.surfaceTop, Color.FromArgb(54, 29, 20)),
                    ParseColor(manifest.surfaceBottom, Color.FromArgb(16, 12, 10)),
                    ParseColor(manifest.primaryText, Color.FromArgb(247, 226, 190)),
                    ParseColor(manifest.secondaryText, Color.FromArgb(200, 157, 103)),
                    ParseColor(manifest.accent, Color.FromArgb(238, 142, 38)),
                    ParseColor(manifest.border, Color.FromArgb(151, 86, 42)),
                    ParseColor(manifest.weekend, Color.FromArgb(238, 142, 38)),
                    manifest.wallpaper, root,
                    new Dictionary<string, string>(manifest.assets, StringComparer.OrdinalIgnoreCase),
                    manifest.layouts == null ? null : new Dictionary<string, string>(manifest.layouts,
                        StringComparer.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        private static string Value(IDictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value) ? value : null;
        }

        private static Color ParseColor(string value, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value)) return fallback;
                string hex = value.Trim().TrimStart('#');
                if (hex.Length != 6) return fallback;
                return Color.FromArgb(Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch { return fallback; }
        }

        private static string NormalizeLegacy(string value)
        {
            if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase)) return "Vintage";
            if (string.Equals(value, "Brass", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Copper", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Aged Steel", StringComparison.OrdinalIgnoreCase)) return "Steampunk";
            if (string.Equals(value, "Midnight", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Ocean", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "EmilyDesk", StringComparison.OrdinalIgnoreCase)) return "Modern";
            foreach (EmilyDeskTheme item in BuiltIns)
                if (string.Equals(value, item.Name, StringComparison.OrdinalIgnoreCase)) return item.Name;
            return "Modern";
        }

        public static double ContrastRatio(Color foreground, Color background)
        {
            double lighter = Math.Max(Luminance(foreground), Luminance(background));
            double darker = Math.Min(Luminance(foreground), Luminance(background));
            return (lighter + .05D) / (darker + .05D);
        }

        public static Color BestTextOn(Color background)
        {
            Color light = Color.White, dark = Color.FromArgb(18, 20, 20);
            return ContrastRatio(light, background) >= ContrastRatio(dark, background) ? light : dark;
        }

        private static double Luminance(Color color)
        {
            return .2126D * Channel(color.R) + .7152D * Channel(color.G) + .0722D * Channel(color.B);
        }

        private static double Channel(byte value)
        {
            double channel = value / 255D;
            return channel <= .03928D ? channel / 12.92D :
                Math.Pow((channel + .055D) / 1.055D, 2.4D);
        }
    }
}
