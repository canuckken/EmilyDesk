using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Web.Script.Serialization;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal sealed class WidgetPackageManifest
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string version { get; set; }
        public string author { get; set; }
        public string assembly { get; set; }
        public string type { get; set; }
        public string minimumSdkVersion { get; set; }
        public string icon { get; set; }
        public string preview { get; set; }
        public bool enabledByDefault { get; set; }
        public string category { get; set; }
        public bool official { get; set; }
        public bool bundled { get; set; }
        public string minimumEngineVersion { get; set; }
        public string[] capabilities { get; set; }
    }

    internal sealed class WidgetPackageInstallResult
    {
        public string WidgetId { get; private set; }
        public string Name { get; private set; }

        public WidgetPackageInstallResult(string widgetId, string name)
        {
            WidgetId = widgetId;
            Name = name;
        }
    }

    internal sealed class WidgetPackageInspection
    {
        public string WidgetId { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string InstalledName { get; set; }
        public string InstalledVersion { get; set; }
        public bool IsInstalled { get; set; }
        public int VersionComparison { get; set; }
    }

    internal sealed class InstalledWidgetPackage
    {
        public string DirectoryPath { get; set; }
        public WidgetPackageManifest Manifest { get; set; }
    }

    internal static class WidgetPackageInstaller
    {
        private const long MaximumPackageBytes = 100L * 1024L * 1024L;
        private const long MaximumEntryBytes = 50L * 1024L * 1024L;
        private const int MaximumEntries = 256;

        public static WidgetPackageInstallResult Install(string packagePath)
        {
            return Install(
                packagePath,
                WidgetPackagePaths.ImportedWidgetsRoot,
                false);
        }

        public static WidgetPackageInstallResult Replace(string packagePath)
        {
            return Install(
                packagePath,
                WidgetPackagePaths.ImportedWidgetsRoot,
                true);
        }

        public static WidgetPackageInspection Inspect(string packagePath)
        {
            return Inspect(
                packagePath,
                WidgetPackagePaths.ImportedWidgetsRoot);
        }

        public static bool CanUninstall(string widgetId)
        {
            return FindInstalledWidget(
                WidgetPackagePaths.ImportedWidgetsRoot,
                widgetId) != null;
        }

        public static WidgetPackageInstallResult Uninstall(string widgetId)
        {
            return Uninstall(
                widgetId,
                WidgetPackagePaths.ImportedWidgetsRoot);
        }

        private static WidgetPackageInstallResult Uninstall(
            string widgetId,
            string widgetsRoot)
        {
            InstalledWidgetPackage installed = FindInstalledWidget(
                widgetsRoot,
                widgetId);
            if (installed == null)
                throw new InvalidOperationException(
                    "The selected widget is bundled with EmilyDesk and cannot be uninstalled separately.");

            string name = DisplayName(installed.Manifest);
            try
            {
                Directory.Delete(installed.DirectoryPath, true);
            }
            catch
            {
                // A loaded plug-in DLL can remain locked until the Engine exits.
                // The marker immediately removes it from discovery and allows a
                // later EmilyDesk start to finish deleting the package safely.
                File.WriteAllText(
                    Path.Combine(
                        installed.DirectoryPath,
                        WidgetPackagePaths.DisabledMarkerFileName),
                    DateTime.UtcNow.ToString("o"));
            }
            return new WidgetPackageInstallResult(widgetId, name);
        }

        public static void CleanupDisabledPackages()
        {
            string root = WidgetPackagePaths.ImportedWidgetsRoot;
            if (!Directory.Exists(root)) return;
            foreach (string directory in Directory.GetDirectories(root))
            {
                if (!File.Exists(Path.Combine(
                    directory,
                    WidgetPackagePaths.DisabledMarkerFileName)))
                    continue;
                DeleteDirectoryBestEffort(directory);
            }
        }

        private static WidgetPackageInspection Inspect(
            string packagePath,
            string widgetsRoot)
        {
            string staging = CreateValidatedStaging(packagePath, widgetsRoot);
            try
            {
                WidgetPackageManifest package = ReadManifest(staging);
                InstalledWidgetPackage installed = FindInstalledWidget(
                    widgetsRoot,
                    package.id);
                return new WidgetPackageInspection
                {
                    WidgetId = package.id,
                    Name = package.name,
                    Author = package.author,
                    Version = package.version,
                    InstalledName = installed == null
                        ? string.Empty
                        : installed.Manifest.name,
                    InstalledVersion = installed == null
                        ? string.Empty
                        : installed.Manifest.version,
                    IsInstalled = installed != null,
                    VersionComparison = installed == null
                        ? 1
                        : CompareVersions(
                            package.version,
                            installed.Manifest.version)
                };
            }
            finally
            {
                DeleteDirectoryBestEffort(staging);
            }
        }

        internal static void VerifyBuildScenarios(
            string packagePath,
            string widgetsRoot)
        {
            WidgetPackageInstallResult installed = Install(
                packagePath,
                widgetsRoot,
                false);
            WidgetPackageInspection same = Inspect(packagePath, widgetsRoot);
            if (!same.IsInstalled ||
                same.VersionComparison != 0 ||
                !string.Equals(
                    same.Name,
                    same.InstalledName,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Same-version package detection failed for WidgetId=" +
                    installed.WidgetId + ".");

            InstalledWidgetPackage existing = FindInstalledWidget(
                widgetsRoot,
                installed.WidgetId);
            string manifestPath = Path.Combine(
                existing.DirectoryPath,
                "manifest.json");
            string packageVersion = existing.Manifest.version;
            existing.Manifest.version = "0.0.0";
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(
                manifestPath,
                serializer.Serialize(existing.Manifest));

            WidgetPackageInspection newer = Inspect(packagePath, widgetsRoot);
            if (!newer.IsInstalled || newer.VersionComparison <= 0)
                throw new InvalidOperationException(
                    "Newer-version package detection failed for WidgetId=" +
                    installed.WidgetId + ".");

            // Cancellation intentionally performs no installation operation.
            WidgetPackageManifest afterCancellation = ReadManifest(
                existing.DirectoryPath);
            if (!string.Equals(
                afterCancellation.version,
                "0.0.0",
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Replacement cancellation changed WidgetId=" +
                    installed.WidgetId + ".");

            Install(packagePath, widgetsRoot, true);
            InstalledWidgetPackage replaced = FindInstalledWidget(
                widgetsRoot,
                installed.WidgetId);
            if (replaced == null ||
                !string.Equals(
                    replaced.Manifest.version,
                    packageVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Newer-version replacement failed for WidgetId=" +
                    installed.WidgetId + ".");

            Uninstall(installed.WidgetId, widgetsRoot);
            if (FindInstalledWidget(widgetsRoot, installed.WidgetId) != null)
                throw new InvalidOperationException(
                    "Widget package removal failed for WidgetId=" +
                    installed.WidgetId + ".");
        }

        private static WidgetPackageInstallResult Install(
            string packagePath,
            string widgetsRoot,
            bool replaceExisting)
        {
            string staging = CreateValidatedStaging(packagePath, widgetsRoot);
            string backup = null;
            InstalledWidgetPackage installed = null;
            WidgetPackageManifest manifest = null;
            bool installedSuccessfully = false;
            try
            {
                manifest = ReadManifest(staging);
                installed = FindInstalledWidget(widgetsRoot, manifest.id);
                if (installed != null)
                {
                    string installedName = DisplayName(installed.Manifest);
                    if (!replaceExisting)
                        throw new InvalidOperationException(
                            installedName + " is already installed.");
                    if (CompareVersions(
                        manifest.version,
                        installed.Manifest.version) <= 0)
                        throw new InvalidOperationException(
                            installedName +
                            " does not need to be replaced.");
                }
                else if (replaceExisting)
                {
                    throw new InvalidOperationException(
                        DisplayName(manifest) + " is not currently installed.");
                }

                string destination = installed == null
                    ? Path.Combine(widgetsRoot, manifest.id)
                    : installed.DirectoryPath;
                if (installed == null && Directory.Exists(destination))
                    throw new InvalidOperationException(
                        DisplayName(manifest) + " is already installed.");

                if (installed != null)
                {
                    backup = Path.Combine(
                        widgetsRoot,
                        ".replace-" + Guid.NewGuid().ToString("N"));
                    Directory.Move(destination, backup);
                }

                try
                {
                    Directory.Move(staging, destination);
                    staging = null;
                    installedSuccessfully = true;
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
                    manifest.id,
                    DisplayName(manifest));
            }
            finally
            {
                DeleteDirectoryBestEffort(staging);
                if (installedSuccessfully)
                    DeleteDirectoryBestEffort(backup);
            }
        }

        private static string CreateValidatedStaging(
            string packagePath,
            string widgetsRoot)
        {
            if (string.IsNullOrWhiteSpace(packagePath) ||
                !File.Exists(packagePath))
                throw new FileNotFoundException(
                    "The widget package was not found.",
                    packagePath);
            string extension = Path.GetExtension(packagePath);
            if (!string.Equals(
                extension,
                ".emilywidget",
                StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                extension,
                ".xwrwidget",
                StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    extension,
                    ".xrwwidget",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "EmilyDesk widget packages must use the .emilywidget extension.");
            if (new FileInfo(packagePath).Length > MaximumPackageBytes)
                throw new InvalidDataException(
                    "The widget package exceeds the 100 MB limit.");

            if (string.IsNullOrWhiteSpace(widgetsRoot))
                throw new ArgumentException(
                    "A widget installation directory is required.",
                    "widgetsRoot");
            widgetsRoot = Path.GetFullPath(widgetsRoot);
            Directory.CreateDirectory(widgetsRoot);
            string staging = Path.Combine(
                widgetsRoot,
                ".install-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(staging);
                ExtractValidatedArchive(packagePath, staging);

                string manifestPath = Path.Combine(staging, "manifest.json");
                if (!File.Exists(manifestPath))
                    throw new InvalidDataException(
                        "The package must contain manifest.json at its root.");

                WidgetPackageManifest manifest = ReadManifest(staging);
                ValidateManifest(
                    manifest,
                    staging,
                    string.Equals(
                        Path.GetFullPath(widgetsRoot).TrimEnd(
                            Path.DirectorySeparatorChar),
                        Path.GetFullPath(
                            WidgetPackagePaths.ImportedWidgetsRoot).TrimEnd(
                                Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase));

                string assemblyPath = ResolvePackagePath(
                    staging,
                    manifest.assembly,
                    "assembly");
                ValidateWidgetAssembly(
                    assemblyPath,
                    manifest.type,
                    manifest.minimumSdkVersion);
                ValidateImage(staging, manifest.icon, "icon");
                ValidateImage(staging, manifest.preview, "preview");
                return staging;
            }
            catch
            {
                DeleteDirectoryBestEffort(staging);
                throw;
            }
        }

        private static WidgetPackageManifest ReadManifest(string directory)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Deserialize<WidgetPackageManifest>(
                File.ReadAllText(Path.Combine(directory, "manifest.json")));
        }

        private static InstalledWidgetPackage FindInstalledWidget(
            string widgetsRoot,
            string widgetId)
        {
            if (!Directory.Exists(widgetsRoot)) return null;
            foreach (string directory in Directory.GetDirectories(widgetsRoot))
            {
                string name = Path.GetFileName(directory);
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;
                if (File.Exists(Path.Combine(
                    directory,
                    WidgetPackagePaths.DisabledMarkerFileName)))
                    continue;
                string manifestPath = Path.Combine(directory, "manifest.json");
                if (!File.Exists(manifestPath)) continue;
                try
                {
                    WidgetPackageManifest manifest = ReadManifest(directory);
                    if (manifest != null && string.Equals(
                        manifest.id,
                        widgetId,
                        StringComparison.OrdinalIgnoreCase))
                        return new InstalledWidgetPackage
                        {
                            DirectoryPath = directory,
                            Manifest = manifest
                        };
                }
                catch { }
            }
            return null;
        }

        private static int CompareVersions(string candidate, string installed)
        {
            Version candidateVersion;
            Version installedVersion;
            if (Version.TryParse(candidate, out candidateVersion) &&
                Version.TryParse(installed, out installedVersion))
                return candidateVersion.CompareTo(installedVersion);
            return string.Equals(
                candidate,
                installed,
                StringComparison.OrdinalIgnoreCase) ? 0 : -1;
        }

        private static string DisplayName(WidgetPackageManifest manifest)
        {
            return string.IsNullOrWhiteSpace(manifest.name)
                ? "This widget"
                : manifest.name;
        }

        private static void DeleteDirectoryBestEffort(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;
            try { Directory.Delete(path, true); }
            catch { }
        }

        private static void ExtractValidatedArchive(
            string packagePath,
            string staging)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            int count = 0;
            string rootPrefix = Path.GetFullPath(staging)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            using (var stream = File.OpenRead(packagePath))
            using (var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Read,
                false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    count++;
                    if (count > MaximumEntries)
                        throw new InvalidDataException(
                            "The package contains more than 256 entries.");
                    if (entry.Length > MaximumEntryBytes)
                        throw new InvalidDataException(
                            "A package entry exceeds the 50 MB limit: " +
                            entry.FullName);
                    totalBytes += entry.Length;
                    if (totalBytes > MaximumPackageBytes)
                        throw new InvalidDataException(
                            "The expanded package exceeds the 100 MB limit.");

                    string relative = entry.FullName.Replace(
                        '/',
                        Path.DirectorySeparatorChar);
                    if (relative.IndexOf('\0') >= 0 ||
                        Path.IsPathRooted(relative) ||
                        relative.IndexOf(':') >= 0)
                        throw new InvalidDataException(
                            "The package contains an unsafe path: " +
                            entry.FullName);

                    string destination = Path.GetFullPath(
                        Path.Combine(staging, relative));
                    if (!destination.StartsWith(
                        rootPrefix,
                        StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(
                            "The package attempts to write outside its widget directory: " +
                            entry.FullName);
                    if (!paths.Add(destination))
                        throw new InvalidDataException(
                            "The package contains a duplicate path: " +
                            entry.FullName);

                    if (entry.FullName.EndsWith(
                        "/",
                        StringComparison.Ordinal))
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
        }

        private static void ValidateManifest(
            WidgetPackageManifest manifest,
            string staging,
            bool importedPackage)
        {
            if (manifest == null)
                throw new InvalidDataException("manifest.json is empty.");
            Require(manifest.id, "id");
            Require(manifest.name, "name");
            Require(manifest.version, "version");
            Require(manifest.author, "author");
            Require(manifest.description, "description");
            Require(manifest.assembly, "assembly");
            Require(manifest.type, "type");

            Version packageVersion;
            if (!Version.TryParse(manifest.version, out packageVersion))
                throw new InvalidDataException(
                    "The widget version is not a valid version number.");

            Version requiredEngine;
            if (!string.IsNullOrWhiteSpace(manifest.minimumEngineVersion))
            {
                if (!Version.TryParse(
                    manifest.minimumEngineVersion,
                    out requiredEngine))
                    throw new InvalidDataException(
                        "minimumEngineVersion is not a valid version.");
                Version currentEngine;
                if (!Version.TryParse(
                    AppConstants.Version,
                    out currentEngine) ||
                    currentEngine.CompareTo(requiredEngine) < 0)
                    throw new InvalidDataException(
                        "This widget requires EmilyDesk " +
                        requiredEngine + " or later.");
            }

            if (importedPackage &&
                (manifest.official || manifest.bundled ||
                    manifest.id.StartsWith(
                        "native.",
                        StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(
                    "Imported widgets cannot replace or impersonate bundled EmilyDesk widgets.");

            if (manifest.id.Length > 100 ||
                manifest.id.StartsWith(".", StringComparison.Ordinal) ||
                manifest.id.EndsWith(".", StringComparison.Ordinal))
                throw new InvalidDataException("The widget id is invalid.");
            foreach (char c in manifest.id)
                if (!(char.IsLetterOrDigit(c) ||
                    c == '.' || c == '-' || c == '_'))
                    throw new InvalidDataException(
                        "The widget id may contain only letters, numbers, dots, hyphens, and underscores.");

            string assemblyPath = ResolvePackagePath(
                staging,
                manifest.assembly,
                "assembly");
            if (!string.Equals(
                Path.GetExtension(assemblyPath),
                ".dll",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The manifest assembly must be a DLL.");
        }

        private static void ValidateWidgetAssembly(
            string assemblyPath,
            string typeName,
            string minimumSdkVersion)
        {
            Version required;
            if (!string.IsNullOrWhiteSpace(minimumSdkVersion))
            {
                if (!Version.TryParse(minimumSdkVersion, out required))
                    throw new InvalidDataException(
                        "minimumSdkVersion is not a valid version.");
                Version current = typeof(IWidget).Assembly.GetName().Version;
                if (current.CompareTo(required) < 0)
                    throw new InvalidDataException(
                        "The package requires Widget SDK " + required +
                        ", but " + current + " is installed.");
            }

            AssemblyName.GetAssemblyName(assemblyPath);
            AppDomain validationDomain = null;
            try
            {
                var setup = new AppDomainSetup
                {
                    ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
                };
                validationDomain = AppDomain.CreateDomain(
                    "XWidgetPackageValidation-" + Guid.NewGuid().ToString("N"),
                    null,
                    setup);
                var validator = (WidgetAssemblyValidator)
                    validationDomain.CreateInstanceFromAndUnwrap(
                        typeof(WidgetAssemblyValidator).Assembly.Location,
                        typeof(WidgetAssemblyValidator).FullName);
                validator.Validate(assemblyPath, typeName);
            }
            finally
            {
                if (validationDomain != null)
                    AppDomain.Unload(validationDomain);
            }
        }

        private static void ValidateImage(
            string staging,
            string relativePath,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            string path = ResolvePackagePath(
                staging,
                relativePath,
                fieldName);
            try
            {
                using (Image image = Image.FromFile(path))
                {
                    if (image.Width <= 0 || image.Height <= 0)
                        throw new InvalidDataException(
                            "The package " + fieldName + " is empty.");
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "The package " + fieldName + " is not a valid image.",
                    ex);
            }
        }

        private static string ResolvePackagePath(
            string staging,
            string relativePath,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.IndexOf(':') >= 0)
                throw new InvalidDataException(
                    "The manifest " + fieldName + " path is invalid.");
            string rootPrefix = Path.GetFullPath(staging)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(
                Path.Combine(staging, relativePath));
            if (!path.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
                throw new InvalidDataException(
                    "The manifest " + fieldName +
                    " file is missing or outside the package.");
            return path;
        }

        private static void Require(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException(
                    "manifest.json requires '" + fieldName + "'.");
        }
    }

    public sealed class WidgetAssemblyValidator : MarshalByRefObject
    {
        public void Validate(string assemblyPath, string typeName)
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type widgetType = assembly.GetType(typeName, true, false);
            if (!typeof(IWidget).IsAssignableFrom(widgetType))
                throw new InvalidDataException(
                    "The configured widget type does not implement IWidget.");
            if (widgetType.IsAbstract ||
                widgetType.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidDataException(
                    "The widget type must be concrete and have a public parameterless constructor.");
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
