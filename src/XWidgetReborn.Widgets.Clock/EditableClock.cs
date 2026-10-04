using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Widgets.Clock
{
    public sealed partial class NativeClockWidget
    {
        private bool _editableReferenceOnly;

        public void RenderEditableDesignerReference(Graphics graphics, Rectangle bounds)
        {
            GraphicsState state = graphics.Save();
            bool previous = _editableReferenceOnly;
            string previousMode = _mode;
            try
            {
                _editableReferenceOnly = true;
                _mode = "Analog";
                float size = IsArtDeco ? 360 : 300;
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / size, bounds.Height / size);
                var canvas = new Rectangle(0, 0, (int)size - 1, (int)size - 1);
                if (IsArtDeco) RenderArtDeco(graphics, canvas);
                else RenderModern(graphics, canvas);
            }
            finally { _mode = previousMode; _editableReferenceOnly = previous; graphics.Restore(state); }
        }

        public void RenderEditableDesignerSymbol(Graphics graphics, string id, RectangleF bounds,
            float rotation, PointF pivot, int colorArgb, float opacity)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0 || opacity <= 0) return;
            if (opacity < .999F)
            {
                // Render one symbol offscreen so both its fill and outline
                // share the same alpha, including the native decorative hands.
                using (var bitmap = new Bitmap(720, 720, PixelFormat.Format32bppArgb))
                using (Graphics temporary = Graphics.FromImage(bitmap))
                using (var attributes = new ImageAttributes())
                {
                    temporary.SmoothingMode = SmoothingMode.AntiAlias;
                    temporary.TranslateTransform(180, 180);
                    RenderEditableDesignerSymbol(temporary, id, bounds, rotation, pivot, colorArgb, 1F);
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Max(0, Math.Min(1, opacity)) });
                    graphics.DrawImage(bitmap, new Rectangle(-180, -180, 720, 720),
                        0, 0, 720, 720, GraphicsUnit.Pixel, attributes);
                }
                return;
            }
            Color ink = Color.FromArgb(colorArgb);
            if (id == "clock-centre-pivot")
            {
                if (IsArtDeco)
                {
                    GraphicsState state = graphics.Save();
                    try
                    {
                        graphics.TranslateTransform(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                        graphics.ScaleTransform(bounds.Width / 11F, bounds.Height / 11F);
                        DrawArtDecoHub(graphics, 0, 0);
                    }
                    finally { graphics.Restore(state); }
                }
                else
                {
                    if (IsModern)
                        using (var halo = new SolidBrush(Color.FromArgb(70, ModernThemePainter.Accent)))
                            graphics.FillEllipse(halo, RectangleF.Inflate(bounds, 5, 5));
                    using (var brush = new SolidBrush(ink))
                    using (var edge = new Pen(IsVintage ? VintageThemePainter.DarkBrass : ModernThemePainter.DeepBorder, 1.2F))
                    {
                        graphics.FillEllipse(brush, bounds);
                        graphics.DrawEllipse(edge, bounds);
                    }
                }
                return;
            }
            bool second = id == "clock-second-hand", hour = id == "clock-hour-hand";
            float length = Math.Max(1, bounds.Height - (second ? 17 : 13));
            double turn = rotation / 360D;
            if (IsArtDeco)
            {
                if (second) DrawArtDecoSecondHand(graphics, pivot.X, pivot.Y, length, turn);
                else DrawArtDecoHand(graphics, pivot.X, pivot.Y, length, bounds.Width / 2, hour ? 4 : 5, turn);
            }
            else if (second)
            {
                using (var pen = new Pen(ink, Math.Max(.1F, bounds.Width)))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    DrawHand(graphics, pen, pivot.X, pivot.Y, length, turn);
                }
            }
            else if (IsVintage) DrawVintageHand(graphics, pivot.X, pivot.Y, length, bounds.Width / 2, turn);
            else DrawModernHand(graphics, pivot.X, pivot.Y, length, bounds.Width / 2, turn, ink);
        }

        private bool RenderEditableClock(Graphics graphics, Size size)
        {
            if (_mode == "Digital" || (!IsModern && !IsVintage && !IsArtDeco)) return false;
            SavedDesignerLayout layout = SavedDesignerLayout.Current(_appearance, "clock");
            if (layout == null || layout.EditableLayerVersion < 1 || layout.Elements == null) return false;
            DesignerLayer background = layout.Elements.Find(delegate(
                DesignerLayer item) { return item != null &&
                    string.Equals(item.Id, "main-background",
                        StringComparison.OrdinalIgnoreCase); });
            bool backgroundDeleted = layout.DeletedElementIds != null &&
                layout.DeletedElementIds.Exists(delegate(string id)
                {
                    return string.Equals(id, "main-background",
                        StringComparison.OrdinalIgnoreCase);
                });
            if (!layout.ReplaceDefaultBackground && background == null &&
                !backgroundDeleted)
                RenderEditableDesignerReference(graphics,
                    new Rectangle(Point.Empty, size));
            if (background != null && background.Visible)
            {
                if (string.IsNullOrWhiteSpace(background.ImagePath))
                    RenderEditableDesignerReference(graphics,
                        Rectangle.Round(background.Bounds));
                else DesignerLayerPainter.DrawImageAlphaCropped(graphics,
                    background.ImagePath, background.Bounds,
                    background.Opacity);
            }
            else if (background == null && !backgroundDeleted)
                DesignerLayerPainter.DrawImage(graphics,
                    layout.BackgroundImage,
                    new RectangleF(0, 0, size.Width, size.Height), 1F);
            DesignerLayer hub = layout.Elements.Find(delegate(DesignerLayer item) {
                return item != null && item.Binding == "Clock: Centre Pivot";
            });
            PointF sharedPivot = hub == null ? (IsArtDeco ? new PointF(180, 174) : new PointF(150, IsVintage ? 149 : 140))
                : new PointF(hub.Bounds.X + hub.Bounds.Width / 2, hub.Bounds.Y + hub.Bounds.Height / 2);
            double seconds = _displayTime.Second + (_smoothSeconds ? _displayTime.Millisecond / 1000D : 0);
            double minutes = _displayTime.Minute + seconds / 60;
            double hours = (_displayTime.Hour % 12) + minutes / 60;
            foreach (DesignerLayer item in layout.Elements)
            {
                if (item == null || !item.Visible || item.Surface != 0 ||
                    (layout.DeletedElementIds != null && layout.DeletedElementIds.Contains(item.Id))) continue;
                if (string.Equals(item.Id, "main-background",
                    StringComparison.OrdinalIgnoreCase)) continue;
                if (item.Binding == "Clock: Date" && !_showDate) continue;
                if (item.Binding == "Clock: Second Hand" && !_showSeconds) continue;
                bool hour = item.Binding == "Clock: Hour Hand", minute = item.Binding == "Clock: Minute Hand";
                bool second = item.Binding == "Clock: Second Hand";
                if (item.Kind == 1 && (hour || minute || second))
                {
                    double turn = hour ? hours / 12 : minute ? minutes / 60 : seconds / 60;
                    float tail = second ? 17F : 13F;
                    PointF pivot = ClockHandPivot(item, sharedPivot);
                    float rotationOffset = layout.ClockHandEditVersion > 0
                        ? item.PreviewRotation : 0F;
                    Image image = ResolveClockImage(item.ImagePath, null,
                        (int)Math.Ceiling(item.Bounds.Width * 4F),
                        (int)Math.Ceiling(item.Bounds.Height * 4F));
                    if (image != null)
                        DrawClockImageHand(graphics, image, pivot.X, pivot.Y, item.Bounds.Width, item.Bounds.Height,
                            Math.Max(1, item.Bounds.Height - tail), turn,
                            rotationOffset,
                            item.HandPivotX, item.HandPivotY,
                            item.Opacity);
                    else RenderEditableDesignerSymbol(graphics, hour ? "clock-hour-hand" : minute
                        ? "clock-minute-hand" : "clock-second-hand", item.Bounds,
                        (float)(turn * 360) + rotationOffset,
                        pivot, item.ColorArgb, item.Opacity);
                }
                else if (item.Kind == 1 && item.Binding == "Clock: Centre Pivot")
                {
                    if (!DesignerLayerPainter.DrawImage(graphics, item.ImagePath, item.Bounds, item.Opacity))
                        RenderEditableDesignerSymbol(graphics, "clock-centre-pivot", item.Bounds, 0, sharedPivot,
                            item.ColorArgb, item.Opacity);
                }
                else DesignerLayerPainter.Draw(graphics, item, delegate(DesignerLayer layer) {
                    return layer.Binding == "Clock: Date" && !IsArtDeco
                        ? _displayTime.ToString("dddd, MMMM d", CultureInfo.CurrentCulture)
                        : ResolveClockDesignerText(layer);
                });
            }
            return true;
        }
    }
}
