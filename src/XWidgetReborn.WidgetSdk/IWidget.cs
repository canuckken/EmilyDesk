using System;
using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>Stable public contract implemented by every hosted widget.</summary>
    public interface IWidget : IDisposable
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string Version { get; }
        Size DefaultSize { get; }
        Point DefaultLocation { get; }
        WidgetUpdateRate UpdateRate { get; }
        void Start(Action invalidate);
        void Tick(DateTime now);
        void Pause();
        void Resume();
        void Render(Graphics graphics, Rectangle bounds);
    }
}
