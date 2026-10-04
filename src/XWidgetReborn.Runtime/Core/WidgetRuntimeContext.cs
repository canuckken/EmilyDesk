using System;
using System.Drawing;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Runtime.Core
{
    internal sealed class WidgetRuntimeContext : IRuntimeContext
    {
        private readonly string _widgetId;
        private readonly IWidgetLogger _logger;
        private readonly IWidgetSettings _settings;
        private readonly IWidgetWeatherService _weather;
        private readonly IWidgetEventBus _events;

        public WidgetRuntimeContext(string widgetId, ServiceContainer services)
        {
            if (string.IsNullOrWhiteSpace(widgetId)) throw new ArgumentNullException("widgetId");
            if (services == null) throw new ArgumentNullException("services");
            _widgetId = widgetId;
            _logger = new WidgetLogger(widgetId, services.Get<IRuntimeLoggingService>());
            _settings = new WidgetSettings(widgetId, services.Get<IRuntimeSettingsService>());
            _weather = new WidgetWeatherService(services.Get<IRuntimeWeatherService>());
            _events = services.Get<IWidgetEventBus>();
        }

        public string EngineVersion { get { return AppConstants.BuildDisplay; } }
        public IWidgetLogger Log { get { return _logger; } }
        public IWidgetSettings Settings { get { return _settings; } }
        public IWidgetWeatherService Weather { get { return _weather; } }
        public IWidgetEventBus Events { get { return _events; } }

        private sealed class WidgetLogger : IWidgetLogger
        {
            private readonly string _prefix;
            private readonly IRuntimeLoggingService _inner;
            public WidgetLogger(string widgetId, IRuntimeLoggingService inner) { _prefix = "[" + widgetId + "] "; _inner = inner; }
            public void Information(string message) { _inner.Information(_prefix + message); }
            public void Warning(string message) { _inner.Warning(_prefix + message); }
            public void Error(string message, Exception error) { _inner.Error(_prefix + message, error); }
            public void Debug(string message) { _inner.Debug(_prefix + message); }
        }

        private sealed class WidgetSettings : IWidgetSettings
        {
            private readonly string _widgetId;
            private readonly IRuntimeSettingsService _inner;
            public WidgetSettings(string widgetId, IRuntimeSettingsService inner) { _widgetId = widgetId; _inner = inner; }
            public bool GetBoolean(string key, bool fallback)
            {
                bool value;
                return bool.TryParse(_inner.GetWidgetSetting(_widgetId, key, fallback.ToString()), out value) ? value : fallback;
            }
            public void SetBoolean(string key, bool value) { _inner.SetWidgetSetting(_widgetId, key, value.ToString()); }
            public string GetString(string key, string fallback) { return _inner.GetWidgetSetting(_widgetId, key, fallback); }
            public void SetString(string key, string value) { _inner.SetWidgetSetting(_widgetId, key, value); }
            public Point GetPosition(Point fallback) { return _inner.GetWidgetPosition(_widgetId, fallback); }
            public void SetPosition(Point position) { _inner.SetWidgetPosition(_widgetId, position); }
            public bool GetTopMost(bool fallback) { return _inner.IsWidgetTopMost(_widgetId, fallback); }
            public void SetTopMost(bool topMost) { _inner.SetWidgetTopMost(_widgetId, topMost); }
        }

        private sealed class WidgetWeatherService : IWidgetWeatherService
        {
            private readonly IRuntimeWeatherService _inner;
            public WidgetWeatherService(IRuntimeWeatherService inner) { _inner = inner; }
            public Uri CompatibilityEndpoint { get { return _inner.CompatibilityEndpoint; } }
        }
    }

    internal sealed class WidgetEventBus : IWidgetEventBus
    {
        public event EventHandler<WidgetRuntimeEventArgs> Published;
        public void Publish(string topic, string payload)
        {
            EventHandler<WidgetRuntimeEventArgs> handler = Published;
            if (handler != null) handler(this, new WidgetRuntimeEventArgs(topic, payload));
        }
    }
}
