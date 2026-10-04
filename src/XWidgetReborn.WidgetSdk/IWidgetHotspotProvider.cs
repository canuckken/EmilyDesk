using System.Collections.Generic;
using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>
    /// Supplies interactive regions that the runtime can host in dedicated
    /// input windows. This is primarily used by per-pixel-alpha widgets whose
    /// visual window cannot reliably receive normal mouse input.
    /// </summary>
    public interface IWidgetHotspotProvider
    {
        IEnumerable<WidgetHotspot> GetHotspots(Size clientSize);
    }

    public sealed class WidgetHotspot
    {
        public WidgetHotspot(string id, Rectangle bounds, string toolTip)
        {
            Id = id ?? string.Empty;
            Bounds = bounds;
            ToolTip = toolTip ?? string.Empty;
        }

        public string Id { get; private set; }
        public Rectangle Bounds { get; private set; }
        public string ToolTip { get; private set; }
    }
}
