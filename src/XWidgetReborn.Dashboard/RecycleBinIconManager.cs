using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal static class RecycleBinIconManager
    {
        private const string RecycleBinClassId =
            "{645FF040-5081-101B-9F08-00AA002F954E}";
        private const string RegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\" +
            RecycleBinClassId + @"\DefaultIcon";
        private const uint AssociationChanged = 0x08000000;
        private const uint IdList = 0x0000;
        private const string MissingValue = "<EMILYDESK-NO-VALUE>";
        private static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EmilyDesk", "recycle-bin-icons.previous");

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(
            uint eventId, uint flags, IntPtr item1, IntPtr item2);

        public static bool CanRestore
        {
            get { return File.Exists(StatePath); }
        }

        public static string EmptyIconPath(string themeName)
        {
            return IconPath(themeName, "empty.ico");
        }

        public static string FullIconPath(string themeName)
        {
            return IconPath(themeName, "full.ico");
        }

        public static void Apply(string themeName)
        {
            string empty = EmptyIconPath(themeName);
            string full = FullIconPath(themeName);
            ValidateIcon(empty);
            ValidateIcon(full);
            RememberPrevious();
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                RegistryPath))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        "Windows did not allow the Recycle Bin icons to change.");
                key.SetValue(null, empty + ",0", RegistryValueKind.String);
                key.SetValue("empty", empty + ",0", RegistryValueKind.String);
                key.SetValue("full", full + ",0", RegistryValueKind.String);
            }
            RefreshShell();
        }

        public static void RestorePrevious()
        {
            if (!File.Exists(StatePath))
                throw new InvalidOperationException(
                    "No previous Recycle Bin icons have been saved by EmilyDesk.");
            string[] values = File.ReadAllLines(StatePath);
            if (values.Length < 3)
                throw new InvalidDataException(
                    "The saved Recycle Bin icon information is incomplete.");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                RegistryPath))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        "Windows did not allow the Recycle Bin icons to be restored.");
                RestoreValue(key, null, values[0]);
                RestoreValue(key, "empty", values[1]);
                RestoreValue(key, "full", values[2]);
            }
            File.Delete(StatePath);
            RefreshShell();
        }

        public static Image LoadPreview(string themeName, bool full)
        {
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(themeName);
            if (theme.IsImported)
            {
                string asset = theme.Asset(full
                    ? "recycleFull" : "recycleEmpty");
                if (string.IsNullOrWhiteSpace(asset) || !File.Exists(asset))
                    throw new FileNotFoundException(
                        "The selected themed Recycle Bin image is missing.",
                        asset);
                using (Image source = Image.FromFile(asset))
                    return new Bitmap(source);
            }
            string path = full
                ? FullIconPath(themeName) : EmptyIconPath(themeName);
            ValidateIcon(path);
            using (var icon = new Icon(path, 128, 128))
                return icon.ToBitmap();
        }

        private static string IconPath(string themeName, string fileName)
        {
            string normalized = EmilyDeskThemeCatalog.Get(themeName).Name;
            string folder = normalized.Replace(" ", string.Empty);
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "RecycleBins", folder, fileName);
        }

        private static void ValidateIcon(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "The selected themed Recycle Bin icon is missing.", path);
            try
            {
                using (var icon = new Icon(path))
                {
                    if (icon.Width < 1 || icon.Height < 1)
                        throw new InvalidDataException(
                            "The selected Recycle Bin icon is empty.");
                }
            }
            catch (ArgumentException)
            {
                throw new InvalidDataException(
                    "The selected Recycle Bin icon is invalid.");
            }
        }

        private static void RememberPrevious()
        {
            if (File.Exists(StatePath)) return;
            string defaultValue = null;
            string emptyValue = null;
            string fullValue = null;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                RegistryPath))
            {
                if (key != null)
                {
                    defaultValue = Convert.ToString(key.GetValue(null, null));
                    emptyValue = Convert.ToString(key.GetValue("empty", null));
                    fullValue = Convert.ToString(key.GetValue("full", null));
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
            File.WriteAllLines(StatePath, new[]
            {
                Encode(defaultValue), Encode(emptyValue), Encode(fullValue)
            });
        }

        private static string Encode(string value)
        {
            return string.IsNullOrEmpty(value) ? MissingValue : value;
        }

        private static void RestoreValue(
            RegistryKey key, string name, string value)
        {
            if (string.Equals(value, MissingValue, StringComparison.Ordinal))
                key.DeleteValue(name ?? string.Empty, false);
            else
                key.SetValue(name, value, RegistryValueKind.String);
        }

        private static void RefreshShell()
        {
            SHChangeNotify(AssociationChanged, IdList,
                IntPtr.Zero, IntPtr.Zero);
        }
    }
}
