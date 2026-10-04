using System;

namespace XWidgetReborn.Runtime.Core
{
    internal interface IEventDispatcher
    {
        event EventHandler<RuntimeEventArgs> Published;
        void Publish(RuntimeEventType type, string widgetId);
        void Publish(
            RuntimeEventType type,
            string widgetId,
            Exception error);
    }

    internal enum RuntimeEventType
    {
        WidgetInstalled,
        WidgetRemoved,
        WidgetStarted,
        WidgetStopped,
        WidgetRestarted,
        WidgetCrashed,
        WidgetFailed
    }

    internal sealed class RuntimeEventArgs : EventArgs
    {
        public RuntimeEventType Type { get; private set; }
        public string WidgetId { get; private set; }
        public Exception Error { get; private set; }

        public RuntimeEventArgs(RuntimeEventType type, string widgetId, Exception error)
        {
            Type = type;
            WidgetId = widgetId;
            Error = error;
        }
    }

    internal sealed class EventDispatcher : IEventDispatcher
    {
        public event EventHandler<RuntimeEventArgs> Published;

        public void Publish(RuntimeEventType type, string widgetId)
        {
            Publish(type, widgetId, null);
        }

        public void Publish(RuntimeEventType type, string widgetId, Exception error)
        {
            EventHandler<RuntimeEventArgs> handler = Published;
            if (handler != null)
                handler(this, new RuntimeEventArgs(type, widgetId, error));
        }
    }
}
