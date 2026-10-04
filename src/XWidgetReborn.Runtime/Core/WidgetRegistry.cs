using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Runtime.Compatibility;

namespace XWidgetReborn.Runtime.Core
{
    internal interface IWidgetRegistry
    {
        event EventHandler Changed;
        void Refresh();
        WidgetDescriptor[] GetInstalledWidgets();
        WidgetDescriptor Find(string id);
        string GetManifestPath(string id);
        bool Contains(string id);
        bool IsEnabledByDefault(string id);
        bool IsLegacy(string id);
        IWidget Create(string id);
    }

    internal sealed class WidgetRegistry : IWidgetRegistry
    {
        private sealed class Registration
        {
            public WidgetDescriptor Descriptor;
            public Func<IWidget> Factory;
            public bool EnabledByDefault;
            public string ManifestPath;
            public bool IsLegacy;
        }

        private readonly Dictionary<string, Registration> _registrations =
            new Dictionary<string, Registration>(StringComparer.OrdinalIgnoreCase);
        private readonly string _root;
        private readonly string _importedRoot;
        private readonly string _legacyRoot;
        private readonly IRuntimeLoggingService _logging;
        private readonly IEventDispatcher _events;

        public event EventHandler Changed;

        public WidgetRegistry(
            string root,
            string importedRoot,
            string legacyRoot,
            IRuntimeLoggingService logging,
            IEventDispatcher events)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentNullException("root");
            if (string.IsNullOrWhiteSpace(importedRoot))
                throw new ArgumentNullException("importedRoot");
            if (string.IsNullOrWhiteSpace(legacyRoot))
                throw new ArgumentNullException("legacyRoot");
            if (logging == null) throw new ArgumentNullException("logging");
            if (events == null) throw new ArgumentNullException("events");
            _root = root;
            _importedRoot = importedRoot;
            _legacyRoot = legacyRoot;
            _logging = logging;
            _events = events;
            RemoveObsoletePackagePreviews();
        }

        private void RemoveObsoletePackagePreviews()
        {
            foreach (string widget in new[] { "Clock", "Calendar" })
            {
                string stale = Path.Combine(_root, widget, "preview-steampunk.png");
                if (File.Exists(stale)) File.Delete(stale);
            }
        }

        public void Refresh()
        {
            _logging.Debug(
                "Widget registry scan started. Bundled=" + _root +
                "; Imported=" + _importedRoot + ".");
            if (!Directory.Exists(_root)) Directory.CreateDirectory(_root);
            if (!Directory.Exists(_importedRoot))
                Directory.CreateDirectory(_importedRoot);

            var discovered = new Dictionary<string, Registration>(
                StringComparer.OrdinalIgnoreCase);
            DiscoverRoot(_root, discovered, false);
            DiscoverRoot(_importedRoot, discovered, true);

            // EmilyDesk V1 is native-only. Keep the legacy adapter source isolated
            // for possible future migration tooling, but never discover or launch
            // external XWidget runtime content from the production registry.
            _logging.Information(
                "Legacy XWidget runtime discovery is disabled for EmilyDesk V1.");

            var removed = new List<string>();
            foreach (string id in _registrations.Keys)
                if (!discovered.ContainsKey(id)) removed.Add(id);

            var installed = new List<string>();
            foreach (string id in discovered.Keys)
                if (!_registrations.ContainsKey(id)) installed.Add(id);

            _registrations.Clear();
            foreach (KeyValuePair<string, Registration> item in discovered)
                _registrations.Add(item.Key, item.Value);

            foreach (string id in removed)
                _events.Publish(RuntimeEventType.WidgetRemoved, id);
            foreach (string id in installed)
                _events.Publish(RuntimeEventType.WidgetInstalled, id);

            _logging.Information(
                "Widget registry scan completed. Installed=" +
                _registrations.Count + "; Added=" + installed.Count +
                "; Removed=" + removed.Count + ".");
            OnChanged();
        }

        private void DiscoverRoot(
            string root,
            IDictionary<string, Registration> discovered,
            bool imported)
        {
            foreach (string manifestPath in Directory.GetFiles(
                root,
                "manifest.json",
                SearchOption.AllDirectories))
            {
                try
                {
                    string widgetDirectory = Path.GetDirectoryName(manifestPath);
                    if (File.Exists(Path.Combine(
                        widgetDirectory,
                        WidgetPackagePaths.DisabledMarkerFileName)))
                        continue;
                    Registration registration = LoadManifest(manifestPath);
                    if (imported &&
                        (registration.Descriptor.IsOfficial ||
                         registration.Descriptor.IsBundled ||
                         registration.Descriptor.Id.StartsWith(
                            "native.",
                            StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException(
                            "Imported widgets cannot impersonate bundled EmilyDesk widgets.");
                    if (discovered.ContainsKey(registration.Descriptor.Id))
                        throw new InvalidDataException(
                            imported
                                ? "An imported widget cannot replace the bundled widget id: " +
                                    registration.Descriptor.Id
                                : "Duplicate widget id: " + registration.Descriptor.Id);
                    discovered.Add(registration.Descriptor.Id, registration);
                }
                catch (Exception ex)
                {
                    _logging.Error(
                        "Widget manifest rejected: " + manifestPath + ".",
                        ex);
                    _events.Publish(
                        RuntimeEventType.WidgetFailed,
                        Path.GetFileName(Path.GetDirectoryName(manifestPath)),
                        ex);
                }
            }
        }

        private void DiscoverLegacyWidgets(
            IDictionary<string, Registration> discovered)
        {
            if (!Directory.Exists(_legacyRoot))
                Directory.CreateDirectory(_legacyRoot);
            foreach (string manifestPath in Directory.GetFiles(
                _legacyRoot,
                "widget.xwl",
                SearchOption.AllDirectories))
            {
                string folder = Path.GetDirectoryName(manifestPath);
                if (!File.Exists(Path.Combine(folder, "main.xul")))
                    continue;
                try
                {
                    LegacyWidgetManifest legacy =
                        LegacyXWidgetAdapter.TryLoad(manifestPath);
                    string id = LegacyWidgetIdentity.CreateId(folder);
                    legacy.Id = id;
                    if (discovered.ContainsKey(id))
                        throw new InvalidDataException(
                            "Duplicate legacy widget id: " + id);

                    string iconPath = FindLegacyImage(
                        folder,
                        "icon.png",
                        "Default.png");
                    string previewPath = iconPath;
                    if (string.IsNullOrWhiteSpace(
                        previewPath))
                        previewPath =
                            FindAnyLegacyImage(folder);
                    var descriptor = new WidgetDescriptor(
                        id,
                        legacy.Name,
                        legacy.Description,
                        legacy.Version,
                        legacy.Author,
                        iconPath,
                        previewPath);
                    string capturedId = id;
                    string capturedManifestPath = manifestPath;
                    LegacyWidgetManifest capturedLegacy = legacy;
                    discovered.Add(
                        id,
                        new Registration
                        {
                            Descriptor = descriptor,
                            EnabledByDefault = false,
                            ManifestPath = manifestPath,
                            IsLegacy = true,
                            Factory = delegate
                            {
                                return new LegacyExternalWidget(
                                    capturedId,
                                    capturedManifestPath,
                                    capturedLegacy);
                            }
                        });
                    _logging.Debug(
                        "Legacy widget registered: " + id + " (" +
                        descriptor.Name + ") from " + manifestPath + ".");
                }
                catch (Exception ex)
                {
                    _logging.Error(
                        "Legacy widget rejected: " + manifestPath + ".",
                        ex);
                    _events.Publish(
                        RuntimeEventType.WidgetFailed,
                        Path.GetFileName(folder),
                        ex);
                }
            }
        }

        private static string FindLegacyImage(
            string folder,
            params string[] names)
        {
            foreach (string name in names)
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path)) return path;
            }
            return string.Empty;
        }

        private static string FindAnyLegacyImage(
            string folder)
        {
            try
            {
                foreach (string path in
                    Directory.GetFiles(
                        folder,
                        "*.*",
                        SearchOption.AllDirectories))
                {
                    string extension =
                        Path.GetExtension(path);
                    if (string.Equals(
                            extension,
                            ".png",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            extension,
                            ".jpg",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            extension,
                            ".jpeg",
                            StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }
            catch { }
            return string.Empty;
        }

        private Registration LoadManifest(string manifestPath)
        {
            var serializer = new JavaScriptSerializer();
            var manifest = serializer.Deserialize<WidgetPluginManifest>(
                File.ReadAllText(manifestPath));
            if (manifest == null) throw new InvalidDataException("Manifest is empty.");
            if (string.IsNullOrWhiteSpace(manifest.id))
                throw new InvalidDataException("Manifest id is required.");
            if (string.IsNullOrWhiteSpace(manifest.assembly))
                throw new InvalidDataException("Manifest assembly is required.");
            if (string.IsNullOrWhiteSpace(manifest.type))
                throw new InvalidDataException("Manifest type is required.");

            Version required;
            if (!string.IsNullOrWhiteSpace(manifest.minimumSdkVersion) &&
                Version.TryParse(manifest.minimumSdkVersion, out required))
            {
                Version current = typeof(IWidget).Assembly.GetName().Version;
                if (current.CompareTo(required) < 0)
                    throw new InvalidDataException(
                        "Requires Widget SDK " + required +
                        " but " + current + " is installed.");
            }

            if (!string.IsNullOrWhiteSpace(manifest.minimumEngineVersion))
            {
                if (!Version.TryParse(
                    manifest.minimumEngineVersion,
                    out required))
                    throw new InvalidDataException(
                        "minimumEngineVersion is invalid.");
                Version currentEngine;
                if (!Version.TryParse(
                    AppConstants.Version,
                    out currentEngine) ||
                    currentEngine.CompareTo(required) < 0)
                    throw new InvalidDataException(
                        "Requires EmilyDesk " + required +
                        " but " + AppConstants.Version + " is installed.");
            }

            string folder = Path.GetDirectoryName(manifestPath);
            string assemblyPath = Path.GetFullPath(
                Path.Combine(folder, manifest.assembly));
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException(
                    "Widget assembly was not found.",
                    assemblyPath);

            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type widgetType = assembly.GetType(manifest.type, true, false);
            if (!typeof(IWidget).IsAssignableFrom(widgetType))
                throw new InvalidDataException(
                    "Type does not implement IWidget: " + manifest.type);
            if (widgetType.IsAbstract ||
                widgetType.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidDataException(
                    "Widget type must be concrete and have a public parameterless constructor.");

            string iconPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(manifest.icon))
            {
                string candidate = Path.GetFullPath(
                    Path.Combine(folder, manifest.icon));
                if (File.Exists(candidate)) iconPath = candidate;
                else _logging.Warning("Widget icon was not found: " + candidate + ".");
            }
            string previewPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(manifest.preview))
            {
                string candidate = Path.GetFullPath(
                    Path.Combine(folder, manifest.preview));
                if (File.Exists(candidate)) previewPath = candidate;
                else _logging.Warning("Widget preview was not found: " + candidate + ".");
            }

            var descriptor = new WidgetDescriptor(
                manifest.id,
                manifest.name,
                manifest.description,
                manifest.version,
                manifest.author,
                iconPath,
                previewPath,
                manifest.category,
                manifest.official,
                manifest.bundled,
                manifest.minimumEngineVersion,
                manifest.capabilities);
            var registration = new Registration
            {
                Descriptor = descriptor,
                EnabledByDefault = manifest.enabledByDefault,
                ManifestPath = manifestPath,
                Factory = delegate
                {
                    var widget = (IWidget)Activator.CreateInstance(widgetType);
                    if (!string.Equals(
                        widget.Id,
                        manifest.id,
                        StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(
                            "Widget ID does not match its manifest: " +
                            widget.Id + " / " + manifest.id);
                    return widget;
                }
            };
            _logging.Debug(
                "Widget registered: " + manifest.id +
                " (" + descriptor.Name + ") from " + manifestPath + ".");
            return registration;
        }

        public WidgetDescriptor[] GetInstalledWidgets()
        {
            var result = new List<WidgetDescriptor>();
            foreach (Registration registration in _registrations.Values)
                result.Add(registration.Descriptor);
            result.Sort(delegate(WidgetDescriptor left, WidgetDescriptor right)
            {
                return string.Compare(
                    left.Name,
                    right.Name,
                    StringComparison.OrdinalIgnoreCase);
            });
            return result.ToArray();
        }

        public WidgetDescriptor Find(string id)
        {
            Registration registration;
            return !string.IsNullOrWhiteSpace(id) &&
                _registrations.TryGetValue(id, out registration)
                ? registration.Descriptor
                : null;
        }

        public string GetManifestPath(string id)
        {
            Registration registration;
            return !string.IsNullOrWhiteSpace(id) &&
                _registrations.TryGetValue(id, out registration)
                ? registration.ManifestPath
                : null;
        }

        public bool Contains(string id)
        {
            return Find(id) != null;
        }

        public bool IsEnabledByDefault(string id)
        {
            Registration registration;
            return !string.IsNullOrWhiteSpace(id) &&
                _registrations.TryGetValue(id, out registration) &&
                registration.EnabledByDefault;
        }

        public bool IsLegacy(string id)
        {
            Registration registration;
            return !string.IsNullOrWhiteSpace(id) &&
                _registrations.TryGetValue(id, out registration) &&
                registration.IsLegacy;
        }

        public IWidget Create(string id)
        {
            Registration registration;
            return !string.IsNullOrWhiteSpace(id) &&
                _registrations.TryGetValue(id, out registration)
                ? registration.Factory()
                : null;
        }

        private void OnChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
