using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace XWidgetReborn.WidgetSdk
{
    // One routing/persistence contract for the Designer and every live widget.
    public static class DesignerLayoutFiles
    {
        private sealed class Entry
        {
            public string Path;
            public DateTime WriteUtc;
            public long Length;
            public object Value;
        }
        private static readonly object Sync = new object();
        [ThreadStatic] private static int _suspendDepth;
        [ThreadStatic] private static string _isolatedDirectory;
        private sealed class DirectoryScope : IDisposable
        {
            private readonly string _previous;
            public DirectoryScope(string path)
            {
                _previous = _isolatedDirectory;
                _isolatedDirectory = Path.GetFullPath(path);
                Reload();
            }
            public void Dispose() { _isolatedDirectory = _previous; Reload(); }
        }
        // Used by verification/preview hosts; never changes Windows user data.
        public static IDisposable UseIsolatedDirectory(string directory)
        {
            return new DirectoryScope(directory);
        }
        private sealed class PreviewScope : IDisposable
        {
            public PreviewScope() { _suspendDepth++; }
            public void Dispose() { _suspendDepth--; }
        }
        public static IDisposable SuspendSavedLayouts() { return new PreviewScope(); }
        private static readonly Dictionary<string, Entry> Cache =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static bool IgnoreSavedLayouts
        {
            get { return _suspendDepth > 0 || Environment.GetEnvironmentVariable(
                "EMILYDESK_IGNORE_DESIGNER_LAYOUT") == "1"; }
        }

        public static string FileName(string theme, string kind)
        {
            EmilyDeskTheme selected = EmilyDeskThemeCatalog.Get(theme);
            string name = selected.Name;
            string stem = selected.IsImported ? selected.Id
                : name == "Botanical Nature" ? "botanical"
                : name == "Woodland Nature" ? "woodland"
                : name.ToLowerInvariant().Replace(' ', '-');
            string revision = (stem == "botanical" &&
                (kind == "weather" || kind == "calendar")) ||
                (stem == "woodland" && kind == "weather") ? ".v2" : "";
            return stem + "-" + kind + revision + ".layout.json";
        }

        public static string LocalPath(string fileName)
        {
            if (_isolatedDirectory != null) return Path.Combine(_isolatedDirectory, fileName);
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", fileName);
        }

        public static string LivePath(string fileName)
        {
            if (_isolatedDirectory != null) return LocalPath(fileName);
            string shared = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts", fileName);
            return PreferredPath(shared, LocalPath(fileName),
                IgnoreSavedLayouts ? null : BundledPath(fileName));
        }

        // Shipped layouts are read-only defaults. A personal save always wins,
        // regardless of the ZIP/install timestamp, and Save still writes LocalPath.
        public static string PreferredPath(string shared, string local, string bundled)
        {
            string saved = NewestPath(shared, local);
            if (File.Exists(saved)) return saved;
            return !string.IsNullOrEmpty(bundled) && File.Exists(bundled)
                ? bundled : saved;
        }

        public static string BundledPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || Path.GetFileName(fileName) != fileName)
                return null;
            string relative = Path.Combine("Assets", "DesignerLayouts", fileName);
            string installed = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);
            if (File.Exists(installed)) return installed;
            string source = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", relative));
            if (File.Exists(source)) return source;
            string stem = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(fileName));
            int dash = stem.LastIndexOf('-');
            if (dash > 0)
            {
                string imported = EmilyDeskThemeCatalog.ThemeLayoutPath(
                    stem.Substring(0, dash), stem.Substring(dash + 1));
                if (File.Exists(imported)) return imported;
            }
            return null;
        }

        // Public for the isolated persistence regression test. Local wins ties.
        public static string NewestPath(string shared, string local)
        {
            if (!File.Exists(shared)) return local;
            if (!File.Exists(local)) return shared;
            return File.GetLastWriteTimeUtc(local) >=
                File.GetLastWriteTimeUtc(shared) ? local : shared;
        }

        public static T Load<T>(string fileName) where T : class
        {
            return IgnoreSavedLayouts ? null : LoadPath<T>(LivePath(fileName));
        }

        public static T LoadPath<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            lock (Sync)
            {
                string key = typeof(T).FullName + "|" + Path.GetFullPath(path);
                Entry previous;
                Cache.TryGetValue(key, out previous);
                try
                {
                    var info = new FileInfo(path);
                    if (previous != null && previous.WriteUtc == info.LastWriteTimeUtc &&
                        previous.Length == info.Length) return previous.Value as T;
                    var serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = 16 * 1024 * 1024;
                    T value = serializer.Deserialize<T>(File.ReadAllText(path));
                    if (value == null) return previous == null ? null : previous.Value as T;
                    Cache[key] = new Entry { Path = path, WriteUtc = info.LastWriteTimeUtc,
                        Length = info.Length, Value = value };
                    return value;
                }
                catch
                {
                    // Only this exact path/type's last good save can be reused.
                    // A damaged theme must never display another theme's layout.
                    return previous == null ? null : previous.Value as T;
                }
            }
        }

        public static void Reload()
        {
            lock (Sync) Cache.Clear();
        }
    }
}
