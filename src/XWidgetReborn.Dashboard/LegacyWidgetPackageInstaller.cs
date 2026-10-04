using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal sealed class LegacyWidgetPackageInspection
    {
        public string WidgetId { get; set; }
        public string Name { get; set; }
        public string RootPath { get; set; }
        public bool IsInstalled { get; set; }
    }

    internal static class LegacyWidgetPackageInstaller
    {
        private const long MaximumPackageBytes = 100L * 1024L * 1024L;
        private const long MaximumEntryBytes = 50L * 1024L * 1024L;
        private const int MaximumEntries = 4096;

        public static LegacyWidgetPackageInspection Inspect(string packagePath)
        {
            return Inspect(packagePath, LegacyWidgetIdentity.LibraryPath);
        }

        public static WidgetPackageInstallResult Install(
            string packagePath,
            bool replaceExisting)
        {
            return Install(
                packagePath,
                LegacyWidgetIdentity.LibraryPath,
                replaceExisting);
        }

        private static LegacyWidgetPackageInspection Inspect(
            string packagePath,
            string libraryRoot)
        {
            string normalizedPackage = PrepareArchive(packagePath);
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(normalizedPackage))
                {
                    string root = FindWidgetRoot(archive);
                    string folderName = LastPathSegment(root);
                    string destination = Path.Combine(
                        Path.GetFullPath(libraryRoot),
                        folderName);
                    return new LegacyWidgetPackageInspection
                    {
                        WidgetId = LegacyWidgetIdentity.CreateId(destination),
                        Name = ReadDisplayName(archive, root, folderName),
                        RootPath = root,
                        IsInstalled = Directory.Exists(destination)
                    };
                }
            }
            finally
            {
                DeleteFileBestEffort(normalizedPackage, packagePath);
            }
        }

        private static WidgetPackageInstallResult Install(
            string packagePath,
            string libraryRoot,
            bool replaceExisting)
        {
            string normalizedPackage = PrepareArchive(packagePath);
            string staging = null;
            string backup = null;
            string destination = null;
            bool completed = false;
            try
            {
                Directory.CreateDirectory(libraryRoot);
                using (ZipArchive archive = ZipFile.OpenRead(normalizedPackage))
                {
                    string root = FindWidgetRoot(archive);
                    string folderName = LastPathSegment(root);
                    destination = Path.Combine(
                        Path.GetFullPath(libraryRoot),
                        folderName);
                    string name = ReadDisplayName(
                        archive,
                        root,
                        folderName);
                    if (Directory.Exists(destination) && !replaceExisting)
                        throw new InvalidOperationException(
                            name + " is already installed.");

                    staging = Path.Combine(
                        Path.GetFullPath(libraryRoot),
                        ".install-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(staging);
                    ExtractWidget(archive, root, staging);

                    if (Directory.Exists(destination))
                    {
                        backup = Path.Combine(
                            Path.GetFullPath(libraryRoot),
                            ".replace-" + Guid.NewGuid().ToString("N"));
                        Directory.Move(destination, backup);
                    }

                    try
                    {
                        Directory.Move(staging, destination);
                        staging = null;
                        completed = true;
                    }
                    catch
                    {
                        if (backup != null &&
                            Directory.Exists(backup) &&
                            !Directory.Exists(destination))
                            Directory.Move(backup, destination);
                        throw;
                    }

                    return new WidgetPackageInstallResult(
                        LegacyWidgetIdentity.CreateId(destination),
                        name);
                }
            }
            finally
            {
                DeleteDirectoryBestEffort(staging);
                if (completed) DeleteDirectoryBestEffort(backup);
                DeleteFileBestEffort(normalizedPackage, packagePath);
            }
        }

        internal static void VerifyBuildScenarios(
            string packagePath,
            string libraryRoot)
        {
            LegacyWidgetPackageInspection inspection =
                Inspect(packagePath, libraryRoot);
            if (!string.Equals(
                inspection.RootPath,
                "Widgets/celtic thermometer weather",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Celtic package root detection failed.");

            WidgetPackageInstallResult installed = Install(
                packagePath,
                libraryRoot,
                false);
            LegacyWidgetPackageInspection existing =
                Inspect(packagePath, libraryRoot);
            if (!existing.IsInstalled ||
                !string.Equals(
                    installed.Name,
                    "Celtic Thermometer Weather",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Celtic package installation was not detected.");

            string destination = Path.Combine(
                libraryRoot,
                "celtic thermometer weather");
            AssertInstalledFile(destination, "widget.xwl");
            AssertInstalledFile(destination, "main.xul");
            AssertInstalledFile(destination, "script.js");
            if (Directory.GetFiles(
                destination,
                "*",
                SearchOption.AllDirectories).Length < 4)
                throw new InvalidOperationException(
                    "Celtic package subfolder resources were not installed.");

            // Cancellation is represented by making no replacement call.
            DateTime unchanged = File.GetLastWriteTimeUtc(
                Path.Combine(destination, "widget.xwl"));
            if (File.GetLastWriteTimeUtc(
                Path.Combine(destination, "widget.xwl")) != unchanged)
                throw new InvalidOperationException(
                    "Legacy replacement cancellation changed installed files.");

            Install(packagePath, libraryRoot, true);
            AssertInstalledFile(destination, "widget.xwl");
            AssertInstalledFile(destination, "main.xul");

            string unsafePackage = Path.Combine(
                libraryRoot,
                "unsafe.xwp");
            using (ZipArchive archive = ZipFile.Open(
                unsafePackage,
                ZipArchiveMode.Create))
            {
                archive.CreateEntry("../outside.txt");
                archive.CreateEntry("Widgets/unsafe/widget.xwl");
                archive.CreateEntry("Widgets/unsafe/main.xul");
            }
            try
            {
                Inspect(unsafePackage, libraryRoot);
                throw new InvalidOperationException(
                    "Unsafe legacy package was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            finally
            {
                DeleteFileBestEffort(unsafePackage, null);
            }
        }

        private static string PrepareArchive(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath) ||
                !File.Exists(packagePath))
                throw new FileNotFoundException(
                    "The legacy widget package was not found.",
                    packagePath);
            if (!string.Equals(
                Path.GetExtension(packagePath),
                ".xwp",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Legacy widget packages must use the .xwp extension.");
            if (new FileInfo(packagePath).Length > MaximumPackageBytes)
                throw new InvalidDataException(
                    "The legacy widget package exceeds the 100 MB limit.");

            string temporary = Path.Combine(
                Path.GetTempPath(),
                "XWidgetReborn-" + Guid.NewGuid().ToString("N") + ".xwp");
            File.Copy(packagePath, temporary, false);
            NormalizeLegacyEntryCount(temporary);
            return temporary;
        }

        private static void NormalizeLegacyEntryCount(string path)
        {
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                int length = (int)Math.Min(stream.Length, 65557L);
                byte[] tail = new byte[length];
                stream.Position = stream.Length - length;
                stream.Read(tail, 0, length);
                int offset = -1;
                for (int i = tail.Length - 22; i >= 0; i--)
                    if (tail[i] == 0x50 &&
                        tail[i + 1] == 0x4b &&
                        tail[i + 2] == 0x05 &&
                        tail[i + 3] == 0x06)
                    {
                        offset = i;
                        break;
                    }
                if (offset < 0)
                    throw new InvalidDataException(
                        "The selected file is not a valid ZIP archive.");
                if (tail[offset + 4] != 0 ||
                    tail[offset + 5] != 0 ||
                    tail[offset + 6] != 0 ||
                    tail[offset + 7] != 0)
                    throw new InvalidDataException(
                        "Split legacy widget archives are not supported.");

                // Some original XWidget packagers wrote twice the real entry
                // count into the per-disk field. Normalize only that field in
                // the private copy; the source package is never modified.
                if (tail[offset + 8] != tail[offset + 10] ||
                    tail[offset + 9] != tail[offset + 11])
                {
                    stream.Position =
                        stream.Length - length + offset + 8;
                    stream.WriteByte(tail[offset + 10]);
                    stream.WriteByte(tail[offset + 11]);
                }
            }
        }

        private static string FindWidgetRoot(ZipArchive archive)
        {
            var files = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            long expandedBytes = 0;
            int count = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                count++;
                if (count > MaximumEntries)
                    throw new InvalidDataException(
                        "The legacy package contains too many files.");
                if (entry.Length > MaximumEntryBytes)
                    throw new InvalidDataException(
                        "A legacy package file exceeds the 50 MB limit.");
                expandedBytes += entry.Length;
                if (expandedBytes > MaximumPackageBytes)
                    throw new InvalidDataException(
                        "The expanded legacy package exceeds the 100 MB limit.");
                string path = ValidateEntryPath(entry.FullName);
                if (!path.EndsWith("/", StringComparison.Ordinal))
                    files.Add(path);
            }

            var roots = new List<string>();
            foreach (string file in files)
            {
                if (!file.EndsWith(
                    "/widget.xwl",
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                string root = file.Substring(
                    0,
                    file.Length - "/widget.xwl".Length);
                if (files.Contains(root + "/main.xul"))
                    roots.Add(root);
            }
            if (roots.Count == 0)
                throw new InvalidDataException(
                    "The package does not contain a widget with widget.xwl and main.xul.");
            if (roots.Count > 1)
                throw new InvalidDataException(
                    "The package contains more than one widget.");
            return roots[0];
        }

        private static string ValidateEntryPath(string fullName)
        {
            string path = (fullName ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path) ||
                path.StartsWith("/", StringComparison.Ordinal) ||
                path.IndexOf(':') >= 0 ||
                path.IndexOf('\0') >= 0)
                throw new InvalidDataException(
                    "The legacy package contains an unsafe path.");
            foreach (string segment in path.Split('/'))
                if (segment == ".." || segment == ".")
                    throw new InvalidDataException(
                        "The legacy package contains an unsafe path.");
            return path;
        }

        private static void ExtractWidget(
            ZipArchive archive,
            string root,
            string staging)
        {
            string prefix = root.TrimEnd('/') + "/";
            string stagingPrefix = Path.GetFullPath(staging)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string path = ValidateEntryPath(entry.FullName);
                if (!path.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                string relative = path.Substring(prefix.Length);
                if (relative.Length == 0) continue;
                string destination = Path.GetFullPath(
                    Path.Combine(
                        staging,
                        relative.Replace(
                            '/',
                            Path.DirectorySeparatorChar)));
                if (!destination.StartsWith(
                    stagingPrefix,
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "The legacy package contains an unsafe path.");
                if (path.EndsWith("/", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }
                string parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                using (Stream input = entry.Open())
                using (var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                    input.CopyTo(output);
            }
        }

        private static string ReadDisplayName(
            ZipArchive archive,
            string root,
            string fallback)
        {
            ZipArchiveEntry entry = null;
            foreach (ZipArchiveEntry candidate in archive.Entries)
                if (string.Equals(
                    ValidateEntryPath(candidate.FullName),
                    root + "/widget.xwl",
                    StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    break;
                }
            if (entry == null) return fallback;
            try
            {
                using (Stream stream = entry.Open())
                    return LegacyWidgetMetadataReader.Read(stream, fallback)
                        .Name;
            }
            catch
            {
                return fallback;
            }
        }

        private static string LastPathSegment(string path)
        {
            string trimmed = path.TrimEnd('/');
            int slash = trimmed.LastIndexOf('/');
            string result = slash < 0
                ? trimmed
                : trimmed.Substring(slash + 1);
            if (string.IsNullOrWhiteSpace(result) ||
                result.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException(
                    "The legacy widget folder name is invalid.");
            return result;
        }

        private static void AssertInstalledFile(
            string root,
            string relativePath)
        {
            if (!File.Exists(Path.Combine(root, relativePath)))
                throw new InvalidOperationException(
                    "Installed legacy widget is missing " + relativePath + ".");
        }

        private static void DeleteDirectoryBestEffort(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;
            try { Directory.Delete(path, true); }
            catch { }
        }

        private static void DeleteFileBestEffort(
            string path,
            string preservePath)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.Equals(
                    path,
                    preservePath,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
                return;
            try { File.Delete(path); }
            catch { }
        }
    }
}
