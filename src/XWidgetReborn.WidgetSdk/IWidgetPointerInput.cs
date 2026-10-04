using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>
    /// Optional pointer-input contract for native widgets with interactive
    /// regions. Returning false from PointerDown preserves normal host drag.
    /// </summary>
    public interface IWidgetPointerInput
    {
        bool PointerDown(
            Point location,
            WidgetPointerButton button);
        void PointerMove(Point location);
        void PointerUp(
            Point location,
            WidgetPointerButton button);
        void PointerLeave();
    }

    public enum WidgetPointerButton
    {
        None,
        Left,
        Right,
        Middle
    }
}
