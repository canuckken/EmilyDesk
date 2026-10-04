using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>
    /// Optional widgets implement this contract to expose an editable layout
    /// to EmilyDesk Designer without creating a compile-time dependency.
    /// </summary>
    public interface IWidgetDesignerProvider
    {
        string DesignerKind { get; }
        string DesignerTheme { get; }
        SavedDesignerLayout CreateDesignerLayout();
        void RenderDesignerBackground(Graphics graphics, Rectangle bounds);
        string ResolveDesignerText(DesignerLayer layer);
    }

    /// <summary>
    /// Optional widgets implement this companion contract when their editable
    /// background also contains generated chrome such as panels and meters.
    /// </summary>
    public interface IWidgetDesignerBackgroundLayerProvider
    {
        void RenderDesignerBackgroundLayer(
            Graphics graphics,
            Rectangle bounds,
            string imagePath,
            float opacity);
    }

    /// <summary>
    /// Draws chrome behind an editable text field after image layers have
    /// rendered. Currency badges use this so LCD images can remain editable.
    /// </summary>
    public interface IWidgetDesignerTextBackgroundProvider
    {
        void RenderDesignerTextBackground(Graphics graphics,
            DesignerLayer layer);
    }
}
