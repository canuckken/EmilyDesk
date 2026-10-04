using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal sealed class ThemePackageManifest
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public Dictionary<string, string> assets { get; set; }
        public Dictionary<string, string> layouts { get; set; }
    }

    internal sealed class ThemePackageInspection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public bool IsInstalled { get; set; }
    }

    internal static class ThemePackageInstaller
    {
        private const long MaximumPackageBytes = 512L * 1024L * 1024L;
        private const long MaximumEntryBytes = 256L * 1024L * 1024L;
        private const long MaximumExpandedBytes = 512L * 1024L * 1024L;
        private const int MaximumEntries = 1024;

        public static ThemePackageInspection Inspect(string packagePath)
        {
            string staging = CreateValidatedStaging(packagePath);
            try
            {
                ThemePackageManifest manifest = ReadManifest(staging);
                return new ThemePackageInspection {
                    Id = manifest.id, Name = manifest.name,
                    Version = manifest.version,
                    IsInstalled = Directory.Exists(Path.Combine(
                        EmilyDeskThemeCatalog.ThemesRoot, manifest.id)) };
            }
            finally { DeleteBestEffort(staging); }
        }

        public static ThemePackageInspection Install(string packagePath, bool replace)
        {
            string staging = CreateValidatedStaging(packagePath);
            string backup = null;
            bool committed = false;
            try
            {
                ThemePackageManifest manifest = ReadManifest(staging);
                string root = EmilyDeskThemeCatalog.ThemesRoot;
                string destination = Path.Combine(root, manifest.id);
                if (Directory.Exists(destination))
                {
                    if (!replace) throw new InvalidOperationException(
                        manifest.name + " is already installed.");
                    backup = Path.Combine(root, ".replace-" + Guid.NewGuid().ToString("N"));
                    Directory.Move(destination, backup);
                }
                try
                {
                    Directory.Move(staging, destination);
                    staging = null;
                    committed = true;
                }
                catch
                {
                    if (backup != null && Directory.Exists(backup) &&
                        !Directory.Exists(destination)) Directory.Move(backup, destination);
                    throw;
                }
                EmilyDeskThemeCatalog.Refresh();
                DesignerLayoutFiles.Reload();
                return new ThemePackageInspection { Id = manifest.id,
                    Name = manifest.name, Version = manifest.version, IsInstalled = true };
            }
            finally
            {
                DeleteBestEffort(staging);
                if (committed) DeleteBestEffort(backup);
            }
        }

        private static string CreateValidatedStaging(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new FileNotFoundException("The theme package was not found.", packagePath);
            if (!string.Equals(Path.GetExtension(packagePath), ".emilytheme",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Theme packages must use the .emilytheme extension.");
            if (new FileInfo(packagePath).Length > MaximumPackageBytes)
                throw new InvalidDataException("The theme package exceeds 512 MB.");

            string root = EmilyDeskThemeCatalog.ThemesRoot;
            Directory.CreateDirectory(root);
            string staging = Path.Combine(root, ".install-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                ExtractValidated(packagePath, staging);
                ThemePackageManifest manifest = ReadManifest(staging);
                ValidateManifest(manifest, staging);
                return staging;
            }
            catch { DeleteBestEffort(staging); throw; }
        }

        private static void ExtractValidated(string archivePath, string staging)
        {
            string prefix = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            long expanded = 0;
            int count = 0;
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (++count > MaximumEntries)
                        throw new InvalidDataException("The theme contains too many files.");
                    if (entry.Length > MaximumEntryBytes ||
                        (expanded += entry.Length) > MaximumExpandedBytes)
                        throw new InvalidDataException("The expanded theme is too large.");
                    string name = (entry.FullName ?? string.Empty).Replace('/',
                        Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (Path.IsPathRooted(name) || name.IndexOf(':') >= 0)
                        throw new InvalidDataException("The theme contains an unsafe path.");
                    string destination = Path.GetFullPath(Path.Combine(staging, name));
                    if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The theme contains a path outside its package.");
                    int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                    if (unixType == 0xA000)
                        throw new InvalidDataException("Symbolic links are not allowed in themes.");
                    if (string.IsNullOrEmpty(entry.Name))
                    { Directory.CreateDirectory(destination); continue; }
                    string extension = Path.GetExtension(destination).ToLowerInvariant();
                    if (extension != ".png" && extension != ".json")
                        throw new InvalidDataException(
                            "Theme archives may contain only PNG and JSON files.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (Stream source = entry.Open())
                    using (FileStream target = new FileStream(destination,
                        FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        source.CopyTo(target);
                }
            }
        }

        private static ThemePackageManifest ReadManifest(string directory)
        {
            string path = Path.Combine(directory, "theme.json");
            if (!File.Exists(path)) throw new InvalidDataException(
                "The package must contain theme.json at its root.");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
            ThemePackageManifest manifest = serializer.Deserialize<ThemePackageManifest>(
                File.ReadAllText(path));
            if (manifest == null) throw new InvalidDataException("theme.json is invalid.");
            return manifest;
        }

        private static void ValidateManifest(ThemePackageManifest manifest, string root)
        {
            if (manifest.schemaVersion != 1)
                throw new InvalidDataException("This theme package version is not supported.");
            if (!Regex.IsMatch(manifest.id ?? string.Empty,
                "^[a-z0-9][a-z0-9-]{1,63}$"))
                throw new InvalidDataException("The theme id is invalid.");
            if (string.IsNullOrWhiteSpace(manifest.name) || manifest.name.Length > 80)
                throw new InvalidDataException("The theme name is invalid.");
            foreach (string builtInName in EmilyDeskThemeCatalog.BuiltInNames)
            {
                EmilyDeskTheme builtIn = EmilyDeskThemeCatalog.Get(builtInName);
                if (string.Equals(manifest.name, builtIn.Name,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(manifest.id, builtIn.Id,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "An imported theme cannot replace a built-in EmilyDesk theme.");
            }
            if (string.IsNullOrWhiteSpace(manifest.version))
                throw new InvalidDataException("The theme version is required.");
            string[] required = { "weather", "weatherDetails", "calendar", "clock",
                "recycleEmpty", "recycleFull", "dock", "weatherIcons" };
            foreach (string key in required)
            {
                string relative;
                if (manifest.assets == null || !manifest.assets.TryGetValue(key, out relative))
                    throw new InvalidDataException("The theme is missing its " + key + " asset.");
                ValidatePackagePath(root, relative, key == "weatherIcons");
            }
            foreach (KeyValuePair<string, string> asset in manifest.assets)
                ValidatePackagePath(root, asset.Value,
                    string.Equals(asset.Key, "weatherIcons",
                        StringComparison.OrdinalIgnoreCase));
            foreach (string kind in new[] { "weather", "calendar", "clock" })
            {
                string relative;
                if (manifest.layouts == null || !manifest.layouts.TryGetValue(kind, out relative))
                    throw new InvalidDataException("The theme is missing its " + kind + " layout.");
                ValidatePackagePath(root, relative, false);
            }
            foreach (KeyValuePair<string, string> layout in manifest.layouts)
                ValidatePackagePath(root, layout.Value, false);
        }

        private static void ValidatePackagePath(string root, string relative, bool directory)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("A theme asset path is invalid.");
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                (directory ? !Directory.Exists(full) : !File.Exists(full)))
                throw new InvalidDataException("A referenced theme asset is missing or unsafe.");
            if (!directory)
            {
                string ext = Path.GetExtension(full).ToLowerInvariant();
                if (ext != ".png" && ext != ".json")
                    throw new InvalidDataException("Themes may reference only PNG and JSON files.");
            }
        }

        private static void DeleteBestEffort(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }
    }
}
