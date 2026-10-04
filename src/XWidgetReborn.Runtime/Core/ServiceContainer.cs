using System;
using System.Collections.Generic;
using System.Drawing;
using XWidgetReborn.Shared;

namespace XWidgetReborn.Runtime.Core
{
    internal sealed class ServiceContainer
    {
        private bool _sealed;
        private readonly Dictionary<Type, object> _services =
            new Dictionary<Type, object>();

        public void Register<TService>(TService service) where TService : class
        {
            if (service == null) throw new ArgumentNullException("service");
            if (_sealed) throw new InvalidOperationException("Runtime service registration is sealed.");
            Type type = typeof(TService);
            if (_services.ContainsKey(type))
                throw new InvalidOperationException("Runtime service is already registered: " + type.FullName);
            _services.Add(type, service);
        }

        public bool TryGet<TService>(out TService service) where TService : class
        {
            object value;
            if (_services.TryGetValue(typeof(TService), out value))
            {
                service = (TService)value;
                return true;
            }
            service = null;
            return false;
        }

        public void Seal()
        {
            _sealed = true;
        }

        public TService Get<TService>() where TService : class
        {
            object service;
            if (!_services.TryGetValue(typeof(TService), out service))
                throw new InvalidOperationException(
                    "Runtime service is not registered: " + typeof(TService).FullName);
            return (TService)service;
        }
    }

    internal interface IRuntimeLoggingService
    {
        void Information(string message);
        void Warning(string message);
        void Error(string message, Exception error);
        void Debug(string message);
    }

    internal sealed class RuntimeLoggingService : IRuntimeLoggingService
    {
        public void Information(string message) { RuntimeLog.Write("INFO  " + message); }
        public void Warning(string message) { RuntimeLog.Write("WARN  " + message); }
        public void Error(string message, Exception error)
        {
            RuntimeLog.Write("ERROR " + message +
                (error == null ? string.Empty : Environment.NewLine + error));
        }
        public void Debug(string message) { RuntimeLog.Write("DEBUG " + message); }
    }

    internal interface IRuntimeSettingsService
    {
        void ApplyMigrations();
        bool IsWidgetEnabled(string widgetId, bool fallback);
        void SetWidgetEnabled(string widgetId, bool enabled);
        Point GetWidgetPosition(string widgetId, Point fallback);
        void SetWidgetPosition(string widgetId, Point position);
        bool IsWidgetTopMost(string widgetId, bool fallback);
        void SetWidgetTopMost(string widgetId, bool topMost);
        string GetWidgetSetting(
            string widgetId,
            string key,
            string fallback);
        void SetWidgetSetting(
            string widgetId,
            string key,
            string value);
    }

    internal sealed class RuntimeSettingsService : IRuntimeSettingsService
    {
        private readonly IRuntimeLoggingService _logging;

        public RuntimeSettingsService(IRuntimeLoggingService logging)
        {
            if (logging == null) throw new ArgumentNullException("logging");
            _logging = logging;
        }

        public void ApplyMigrations()
        {
            WidgetStateStore.ApplySchemaMigrations(_logging);
        }
        public bool IsWidgetEnabled(string widgetId, bool fallback)
        {
            return WidgetStateStore.LoadEnabled(widgetId, fallback, _logging);
        }
        public void SetWidgetEnabled(string widgetId, bool enabled)
        {
            WidgetStateStore.SaveEnabled(widgetId, enabled, _logging);
        }
        public Point GetWidgetPosition(string widgetId, Point fallback)
        {
            return WidgetStateStore.LoadPosition(widgetId, fallback, _logging);
        }
        public void SetWidgetPosition(string widgetId, Point position)
        {
            WidgetStateStore.SavePosition(widgetId, position, _logging);
        }
        public bool IsWidgetTopMost(string widgetId, bool fallback)
        {
            return WidgetStateStore.LoadTopMost(widgetId, fallback, _logging);
        }
        public void SetWidgetTopMost(string widgetId, bool topMost)
        {
            WidgetStateStore.SaveTopMost(widgetId, topMost, _logging);
        }
        public string GetWidgetSetting(
            string widgetId,
            string key,
            string fallback)
        {
            return WidgetStateStore.LoadSetting(
                widgetId,
                key,
                fallback,
                _logging);
        }
        public void SetWidgetSetting(
            string widgetId,
            string key,
            string value)
        {
            WidgetStateStore.SaveSetting(
                widgetId,
                key,
                value,
                _logging);
        }
    }

    internal interface IRuntimeWeatherService
    {
        Uri CompatibilityEndpoint { get; }
    }

    internal sealed class RuntimeWeatherService : IRuntimeWeatherService
    {
        private readonly Uri _compatibilityEndpoint =
            new Uri(AppConstants.ListenerPrefix, UriKind.Absolute);

        public Uri CompatibilityEndpoint { get { return _compatibilityEndpoint; } }
    }
}
