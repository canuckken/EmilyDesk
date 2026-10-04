using System;
using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>Optional contract for widgets that consume engine services.</summary>
    public interface IRuntimeAwareWidget
    {
        void AttachRuntime(IRuntimeContext runtime);
    }

    /// <summary>Stable service gateway supplied by the engine before Start.</summary>
    public interface IRuntimeContext
    {
        string EngineVersion { get; }
        IWidgetLogger Log { get; }
        IWidgetSettings Settings { get; }
        IWidgetWeatherService Weather { get; }
        IWidgetEventBus Events { get; }
    }

    public interface IWidgetLogger
    {
        void Information(string message);
        void Warning(string message);
        void Error(string message, Exception error);
        void Debug(string message);
    }

    public interface IWidgetSettings
    {
        bool GetBoolean(string key, bool fallback);
        void SetBoolean(string key, bool value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        Point GetPosition(Point fallback);
        void SetPosition(Point position);
        bool GetTopMost(bool fallback);
        void SetTopMost(bool topMost);
    }

    public interface IWidgetWeatherService
    {
        Uri CompatibilityEndpoint { get; }
    }

    public interface IWidgetEventBus
    {
        event EventHandler<WidgetRuntimeEventArgs> Published;
        void Publish(string topic, string payload);
    }

    public sealed class WidgetRuntimeEventArgs : EventArgs
    {
        public string Topic { get; private set; }
        public string Payload { get; private set; }

        public WidgetRuntimeEventArgs(string topic, string payload)
        {
            Topic = topic ?? string.Empty;
            Payload = payload ?? string.Empty;
        }
    }
}
