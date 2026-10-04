using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Runtime.Core
{
    internal sealed class RuntimeHost : IDisposable
    {
        private readonly ServiceContainer _services;
        private readonly IRuntimeLoggingService _logging;
        private readonly IEventDispatcher _events;
        private readonly EngineScheduler _scheduler;
        private readonly IWidgetRegistry _registry;
        private readonly ILifecycleManager _lifecycle;
        private readonly IRuntimePackageManager _packages;
        private readonly IWidgetEventBus _widgetEvents;
        private readonly SynchronizationContext _synchronizationContext;
        public event EventHandler Changed;

        public RuntimeHost(IRuntimeLoggingService logging)
        {
            if (logging == null) throw new ArgumentNullException("logging");
            _services = new ServiceContainer();
            _logging = logging;
            _events = new EventDispatcher();
            _events.Published += RuntimeEventPublished;

            var settings = new RuntimeSettingsService(_logging);
            var weather = new RuntimeWeatherService();
            _synchronizationContext = SynchronizationContext.Current;
            var widgetEvents = new WidgetEventBus();
            _widgetEvents = widgetEvents;
            _widgetEvents.Published += WidgetEventPublished;
            _scheduler = new EngineScheduler(_logging);
            _registry = new WidgetRegistry(
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Widgets"),
                WidgetPackagePaths.ImportedWidgetsRoot,
                LegacyWidgetIdentity.LibraryPath,
                _logging,
                _events);
            _packages = new RuntimePackageManager(_registry, _logging);

            _services.Register<IRuntimeLoggingService>(_logging);
            _services.Register<IRuntimeSettingsService>(settings);
            _services.Register<IRuntimeWeatherService>(weather);
            _services.Register<XWidgetReborn.WidgetSdk.IWidgetEventBus>(widgetEvents);
            _services.Register<IWidgetRegistry>(_registry);
            _services.Register<IEventDispatcher>(_events);
            _services.Register<IRuntimePackageManager>(_packages);

            _lifecycle = new LifecycleManager(
                _services.Get<IWidgetRegistry>(),
                _scheduler,
                _services.Get<IRuntimeSettingsService>(),
                _services.Get<IRuntimeLoggingService>(),
                _services.Get<IEventDispatcher>(),
                _services);
            _services.Register<ILifecycleManager>(_lifecycle);
            _services.Seal();
            _lifecycle.Changed += delegate { OnChanged(); };
            _registry.Changed += delegate { OnChanged(); };
            _logging.Debug("RuntimeHost constructed.");
        }

        public bool ClockIsOpen
        {
            get { return _lifecycle.IsRunning(EngineClient.NativeClockWidgetId); }
        }
        public string[] OpenWidgetIds { get { return _lifecycle.GetRunningWidgetIds(); } }
        public WidgetDescriptor[] AvailableWidgets
        {
            get { return _registry.GetInstalledWidgets(); }
        }

        public void Start(string widgetPath)
        {
            _logging.Information("RuntimeHost coordinating package discovery and lifecycle restore.");
            _packages.RefreshInstalledPackages();
            _lifecycle.Restore();
            _logging.Information(
                "RuntimeHost startup coordination completed. Clock open=" +
                ClockIsOpen + ".");
        }

        public bool OpenWidget(string id) { return _lifecycle.Start(id); }
        public bool OpenWidget(string id, string theme)
        {
            string selected = XWidgetReborn.WidgetSdk.
                EmilyDeskThemeCatalog.Normalize(theme);
            if (_lifecycle.IsRunning(id) && !_lifecycle.Stop(id))
                return false;
            _services.Get<IRuntimeSettingsService>().SetWidgetSetting(
                id,
                "appearance",
                selected);
            return _lifecycle.Start(id);
        }
        public bool CloseWidget(string id) { return _lifecycle.Stop(id); }
        public void RefreshWidgets() { _packages.RefreshInstalledPackages(); }
        public void RefreshDesignerLayouts()
        {
            XWidgetReborn.WidgetSdk.DesignerLayoutFiles.Reload();
            _services.Get<XWidgetReborn.WidgetSdk.IWidgetEventBus>().Publish(
                "designer.saved", "all");
        }
        public void RefreshWeatherWidgets()
        {
            _services.Get<XWidgetReborn.WidgetSdk.IWidgetEventBus>().Publish(
                "weather.refresh",
                "provider-changed");
        }
        public void BringWidgetsAboveXWidgetReborn(IntPtr rebornWindow) { _lifecycle.BringWidgetsAboveXWidgetReborn(rebornWindow); }
        public void PlaceNormalWidgetsBehind(IntPtr applicationWindow) { _lifecycle.PlaceNormalWidgetsBehind(applicationWindow); }
        public void LogWidgetLayerSnapshot(IntPtr rebornWindow, string trigger) { _lifecycle.LogWidgetLayerSnapshot(rebornWindow, trigger); }
        public void SetWidgetLayerMode(WidgetLayerMode mode) { _lifecycle.SetWidgetLayerMode(mode); }

        private void RuntimeEventPublished(object sender, RuntimeEventArgs e)
        {
            _logging.Information(
                "Runtime event: " + e.Type + "; Widget=" + e.WidgetId +
                (e.Error == null ? "." : "; Error=" + e.Error.Message));
        }

        private void WidgetEventPublished(
            object sender,
            WidgetRuntimeEventArgs e)
        {
            if (e == null || !string.Equals(e.Topic,
                    "widget.switch-theme",
                    StringComparison.OrdinalIgnoreCase))
                return;
            Action switchTheme = delegate { SwitchOptionalWidgetTheme(e.Payload); };
            if (_synchronizationContext != null)
                _synchronizationContext.Post(delegate { switchTheme(); }, null);
            else
                switchTheme();
        }

        private void SwitchOptionalWidgetTheme(string payload)
        {
            string[] fields = (payload ?? string.Empty).Split('\t');
            if (fields.Length < 2)
                return;
            string sourceId = fields[0];
            string targetId = fields[1];
            if (string.IsNullOrWhiteSpace(sourceId) ||
                string.IsNullOrWhiteSpace(targetId) ||
                string.Equals(sourceId, targetId,
                    StringComparison.OrdinalIgnoreCase) ||
                !_registry.Contains(targetId) ||
                !_lifecycle.IsRunning(sourceId))
                return;

            IRuntimeSettingsService settings =
                _services.Get<IRuntimeSettingsService>();
            Point position = settings.GetWidgetPosition(
                sourceId, Point.Empty);
            int x;
            int y;
            if (fields.Length >= 4 &&
                int.TryParse(fields[2], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out x) &&
                int.TryParse(fields[3], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out y))
                position = new Point(x, y);

            settings.SetWidgetPosition(targetId, position);
            settings.SetWidgetTopMost(targetId,
                settings.IsWidgetTopMost(sourceId, false));
            foreach (string key in new[]
            {
                "scale", "opacity", "host.layerMode",
                "host.lockPosition", "host.snapToEdges",
                "host.keepOnScreen", "host.clickThrough",
                "host.fullOpacityOnHover", "from", "to", "amount"
            })
            {
                const string missing = "__emilydesk_missing_setting__";
                string value = settings.GetWidgetSetting(
                    sourceId, key, missing);
                if (!string.Equals(value, missing,
                        StringComparison.Ordinal))
                    settings.SetWidgetSetting(targetId, key, value);
            }

            bool targetWasRunning = _lifecycle.IsRunning(targetId);
            if (targetWasRunning && !_lifecycle.Stop(targetId))
                return;
            if (!_lifecycle.Stop(sourceId))
            {
                if (targetWasRunning) _lifecycle.Start(targetId);
                return;
            }
            if (!_lifecycle.Start(targetId))
                _lifecycle.Start(sourceId);
        }

        private void OnChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _logging.Debug("RuntimeHost.Dispose entered.");
            _widgetEvents.Published -= WidgetEventPublished;
            _events.Published -= RuntimeEventPublished;
            _lifecycle.Dispose();
            _scheduler.Dispose();
            _logging.Debug("RuntimeHost.Dispose completed.");
        }
    }
}
