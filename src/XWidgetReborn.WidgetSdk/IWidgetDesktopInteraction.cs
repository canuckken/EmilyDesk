using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    public interface IWidgetDoubleClickInput
    {
        bool PointerDoubleClick(
            Point location,
            WidgetPointerButton button);
    }

    public interface IWidgetFileDropTarget
    {
        bool CanAcceptFiles(string[] paths);
        void DropFiles(string[] paths);
    }
}
