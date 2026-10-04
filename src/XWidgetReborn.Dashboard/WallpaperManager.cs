using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal static class WallpaperManager
    {
        public const string AutomaticResolution = "Automatic (best match)";
        public const string WideResolution = "3840 x 2160 (16:9)";
        public const string TallResolution = "3840 x 2400 (16:10)";

        private const int GetWallpaper = 0x0073;
        private const int SetWallpaper = 0x0014;
        private const int UpdateIni = 0x01;
        private const int SendChange = 0x02;
        private static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EmilyDesk", "wallpaper.previous");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(
            int action, int parameter, StringBuilder value, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(
            int action, int parameter, string value, int flags);

        public static string WallpapersDirectory
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Wallpapers");
            }
        }

        public static string CurrentPath()
        {
            var value = new StringBuilder(1024);
            return SystemParametersInfo(GetWallpaper, value.Capacity, value, 0)
                ? value.ToString()
                : string.Empty;
        }

        public static string RecommendedPath(string themeName)
        {
            return CollectionPath(themeName, 1, AutomaticResolution);
        }

        public static string CollectionPath(
            string themeName, int design, string resolution)
        {
            if (design < 1 || design > 2)
                throw new ArgumentOutOfRangeException(
                    "design", "Wallpaper design must be 1 or 2.");

            EmilyDeskTheme selectedTheme =
                EmilyDeskThemeCatalog.Get(themeName);
            string size = ResolutionSize(resolution);
            if (selectedTheme.IsImported)
            {
                string key = "wallpaper" + design +
                    (size == "3840x2400" ? "Tall" : "Wide");
                string imported = selectedTheme.Asset(key);
                if (!string.IsNullOrWhiteSpace(imported))
                    return imported;
            }

            string theme = selectedTheme.Name
                .Replace(" ", "_");
            string fileName = string.Format(
                "EmilyDesk_{0}_Wallpaper_{1}_{2}.png",
                theme, design, size);
            return Path.Combine(WallpapersDirectory, fileName);
        }

        public static string ResolvedResolution(string resolution)
        {
            return ResolutionSize(resolution) == "3840x2400"
                ? TallResolution
                : WideResolution;
        }

        private static string ResolutionSize(string resolution)
        {
            if (string.Equals(
                resolution, TallResolution, StringComparison.Ordinal))
                return "3840x2400";
            if (string.Equals(
                resolution, WideResolution, StringComparison.Ordinal))
                return "3840x2160";

            Screen screen = Screen.PrimaryScreen;
            if (screen != null && screen.Bounds.Height > 0)
            {
                double ratio = (double)screen.Bounds.Width /
                    screen.Bounds.Height;
                if (ratio < 1.69D) return "3840x2400";
            }
            return "3840x2160";
        }

        public static void Apply(string path, string layout)
        {
            ValidateImage(path);
            RememberPrevious();
            SetLayout(layout);
            if (!SystemParametersInfo(
                SetWallpaper,
                0,
                Path.GetFullPath(path),
                UpdateIni | SendChange))
                throw new InvalidOperationException(
                    "Windows did not accept the wallpaper change.");
        }

        public static bool CanRestore
        {
            get { return File.Exists(StatePath); }
        }

        public static void RestorePrevious()
        {
            if (!File.Exists(StatePath))
                throw new InvalidOperationException(
                    "No previous wallpaper has been saved by EmilyDesk.");
            string[] values = File.ReadAllLines(StatePath);
            if (values.Length < 3)
                throw new InvalidDataException(
                    "The saved wallpaper information is incomplete.");
            string path = values[0];
            if (!string.IsNullOrWhiteSpace(path)) ValidateImage(path);
            SetRegistry(values[1], values[2]);
            if (!SystemParametersInfo(
                SetWallpaper, 0, path, UpdateIni | SendChange))
                throw new InvalidOperationException(
                    "Windows did not restore the previous wallpaper.");
            File.Delete(StatePath);
        }

        public static void ValidateImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(
                    "The selected wallpaper image is missing.", path);
            try
            {
                using (Image image = Image.FromFile(path))
                {
                    if (image.Width < 1 || image.Height < 1)
                        throw new InvalidDataException(
                            "The selected wallpaper image is empty.");
                }
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidDataException(
                    "The selected file is not a supported wallpaper image.");
            }
        }

        private static void RememberPrevious()
        {
            if (File.Exists(StatePath)) return;
            string style = "10";
            string tile = "0";
            using (RegistryKey desktop = Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Desktop"))
            {
                if (desktop != null)
                {
                    style = Convert.ToString(
                        desktop.GetValue("WallpaperStyle", "10"));
                    tile = Convert.ToString(
                        desktop.GetValue("TileWallpaper", "0"));
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
            File.WriteAllLines(StatePath, new[]
            {
                CurrentPath(), style, tile
            });
        }

        private static void SetLayout(string layout)
        {
            string style = "10";
            string tile = "0";
            switch (layout)
            {
                case "Fit": style = "6"; break;
                case "Stretch": style = "2"; break;
                case "Tile": style = "0"; tile = "1"; break;
                case "Center": style = "0"; break;
                case "Span": style = "22"; break;
            }
            SetRegistry(style, tile);
        }

        private static void SetRegistry(string style, string tile)
        {
            using (RegistryKey desktop = Registry.CurrentUser.CreateSubKey(
                @"Control Panel\Desktop"))
            {
                desktop.SetValue("WallpaperStyle", style);
                desktop.SetValue("TileWallpaper", tile);
            }
        }
    }
}
