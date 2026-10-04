using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace EmilyDesk.Designer
{
    internal static class DesignerWallpaperManager
    {
        private const int GetWallpaper = 0x0073;
        private const int SetWallpaper = 0x0014;
        private const int UpdateIni = 0x01;
        private const int SendChange = 0x02;
        private static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EmilyDesk", "wallpaper.previous");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(
            int action, int parameter, StringBuilder value, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(
            int action, int parameter, string value, int flags);

        public static void Apply(string path)
        {
            Validate(path);
            RememberPrevious();
            SetRegistry("10", "0");
            if (!SystemParametersInfo(SetWallpaper, 0,
                Path.GetFullPath(path), UpdateIni | SendChange))
                throw new InvalidOperationException(
                    "Windows did not accept the wallpaper change.");
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
            SetRegistry(values[1], values[2]);
            if (!SystemParametersInfo(SetWallpaper, 0, values[0],
                UpdateIni | SendChange))
                throw new InvalidOperationException(
                    "Windows did not restore the previous wallpaper.");
            File.Delete(StatePath);
        }

        private static void Validate(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(
                    "The selected wallpaper image is missing.", path);
            try
            {
                using (Image image = Image.FromFile(path))
                    if (image.Width < 1 || image.Height < 1)
                        throw new InvalidDataException(
                            "The selected wallpaper image is empty.");
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
                if (desktop != null)
                {
                    style = Convert.ToString(
                        desktop.GetValue("WallpaperStyle", "10"));
                    tile = Convert.ToString(
                        desktop.GetValue("TileWallpaper", "0"));
                }
            var current = new StringBuilder(1024);
            SystemParametersInfo(GetWallpaper, current.Capacity, current, 0);
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
            File.WriteAllLines(StatePath,
                new[] { current.ToString(), style, tile });
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
