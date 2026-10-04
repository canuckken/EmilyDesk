using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace XWidgetReborn.Runtime.Core
{
    internal static class WidgetStateStore
    {
        private static string DirectoryPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmilyDesk", "WidgetState"); }
        }

        private static string LegacyDirectoryPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XWidget Reborn", "WidgetState"); }
        }


        // 2.6.0-2.6.2 could persist a disabled clock during abnormal Engine
        // shutdown. Reinstalling preserves Local AppData, so that stale value
        // prevented the first native widget from appearing after an upgrade.
        // Apply this schema migration once, preserving position and topmost state.
        public static void ApplySchemaMigrations(
            IRuntimeLoggingService logging)
        {
            const string currentSchema = "3";
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                MigrateNativeState(logging);
                string schemaPath = Path.Combine(DirectoryPath, "state.schema");
                string existing = File.Exists(schemaPath) ? File.ReadAllText(schemaPath).Trim() : null;
                if (string.Equals(existing, currentSchema, StringComparison.Ordinal)) return;

                SaveEnabled("native.clock", true, logging);
                File.WriteAllText(schemaPath, currentSchema);
                logging.Information(
                    "Widget-state schema migrated to 3; EmilyDesk native state is isolated and recoverable.");
            }
            catch (Exception ex)
            {
                logging.Error("Could not migrate widget state.", ex);
            }
        }

        private static void MigrateNativeState(IRuntimeLoggingService logging)
        {
            if (!Directory.Exists(LegacyDirectoryPath)) return;
            foreach (string source in Directory.GetFiles(
                LegacyDirectoryPath,
                "native.*",
                SearchOption.TopDirectoryOnly))
            {
                string destination = Path.Combine(
                    DirectoryPath,
                    Path.GetFileName(source));
                if (!File.Exists(destination))
                    File.Copy(source, destination, false);
            }
            logging.Information(
                "Imported existing native widget state into EmilyDesk without modifying legacy files.");
        }

        public static Point LoadPosition(
            string id,
            Point fallback,
            IRuntimeLoggingService logging)
        {
            try
            {
                string path = GetPath(id, ".position");
                if (!File.Exists(path)) return fallback;
                string[] parts = File.ReadAllText(path).Split(',');
                int x, y;
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                    return new Point(x, y);
            }
            catch (Exception ex)
            {
                logging.Warning(
                    "Could not load widget position: " + ex.Message);
            }
            return fallback;
        }

        public static void SavePosition(
            string id,
            Point location,
            IRuntimeLoggingService logging)
        {
            Write(id, ".position", location.X.ToString(CultureInfo.InvariantCulture) + "," + location.Y.ToString(CultureInfo.InvariantCulture), logging);
        }

        public static bool LoadEnabled(
            string id,
            bool fallback,
            IRuntimeLoggingService logging)
        {
            string text = Read(id, ".enabled", logging);
            bool result;
            return bool.TryParse(text, out result) ? result : fallback;
        }

        public static void SaveEnabled(
            string id,
            bool enabled,
            IRuntimeLoggingService logging)
        {
            Write(id, ".enabled", enabled.ToString(CultureInfo.InvariantCulture), logging);
        }

        public static bool LoadTopMost(
            string id,
            bool fallback,
            IRuntimeLoggingService logging)
        {
            string text = Read(id, ".topmost", logging);
            bool result;
            return bool.TryParse(text, out result) ? result : fallback;
        }

        public static void SaveTopMost(
            string id,
            bool topMost,
            IRuntimeLoggingService logging)
        {
            Write(id, ".topmost", topMost.ToString(CultureInfo.InvariantCulture), logging);
        }

        public static string LoadSetting(
            string id,
            string key,
            string fallback,
            IRuntimeLoggingService logging)
        {
            string value = Read(
                id,
                "." + Sanitize(key) + ".setting",
                logging);
            return value ?? fallback;
        }

        public static void SaveSetting(
            string id,
            string key,
            string value,
            IRuntimeLoggingService logging)
        {
            Write(
                id,
                "." + Sanitize(key) + ".setting",
                value ?? string.Empty,
                logging);
        }

        private static string Read(
            string id,
            string extension,
            IRuntimeLoggingService logging)
        {
            try
            {
                string path = GetPath(id, extension);
                return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            }
            catch (Exception ex)
            {
                logging.Warning("Could not load widget state: " + ex.Message);
                return null;
            }
        }

        private static void Write(
            string id,
            string extension,
            string value,
            IRuntimeLoggingService logging)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(GetPath(id, extension), value);
            }
            catch (Exception ex)
            {
                logging.Warning("Could not save widget state: " + ex.Message);
            }
        }

        private static string GetPath(string id, string extension)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return Path.Combine(DirectoryPath, id + extension);
        }

        private static string Sanitize(string value)
        {
            value = value ?? string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace('.', '_');
        }
    }
}
