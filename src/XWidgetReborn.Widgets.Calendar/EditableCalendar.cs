using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Widgets.Calendar
{
    public sealed partial class NativeCalendarWidget
    {
        private bool _editableReferenceOnly;

        private SavedDesignerLayout EditableCalendarLayout()
        {
            if (!IsModern && !IsVintage && !IsArtDeco) return null;
            SavedDesignerLayout layout = SavedDesignerLayout.Current(_appearance, "calendar");
            return layout != null && layout.EditableLayerVersion >= 1 && layout.Elements != null ? layout : null;
        }

        public void RenderEditableDesignerReference(Graphics graphics, Rectangle bounds)
        {
            GraphicsState state = graphics.Save();
            bool previous = _editableReferenceOnly;
            try
            {
                _editableReferenceOnly = true;
                Size size = DesignSize();
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / (float)size.Width, bounds.Height / (float)size.Height);
                var canvas = new Rectangle(0, 0, size.Width - 1, size.Height - 1);
                if (IsArtDeco) RenderArtDecoStyle(graphics, canvas);
                else DrawCalendar(graphics, canvas);
                if (IsModern)
                    using (var tint = new SolidBrush(Color.FromArgb(22, ModernThemePainter.Accent)))
                        for (int row = 0; row < 6; row += 2)
                            graphics.FillRectangle(tint, 22, 110 + row * 220F / 6, 376, 220F / 6);
            }
            finally { _editableReferenceOnly = previous; graphics.Restore(state); }
        }

        public void RenderEditableButtonBackground(Graphics graphics, string id, RectangleF bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            CalendarButton button;
            if (id == "previous-button") button = CalendarButton.Previous;
            else if (id == "next-button") button = CalendarButton.Next;
            else if (id == "today-button") button = CalendarButton.Today;
            else return;
            using (var font = new Font("Segoe UI", 10F))
                DrawButton(graphics, Rectangle.Round(bounds), "", button, Palette(), font);
        }

        private Rectangle EditableCalendarBounds(string id, Rectangle fallback)
        {
            SavedDesignerLayout layout = EditableCalendarLayout();
            if (layout == null) return fallback;
            DesignerLayer item = layout.Elements.Find(delegate(DesignerLayer e) { return e != null && e.Id == id; });
            return item != null && item.Visible && !EditableCalendarDeleted(layout, id)
                ? Rectangle.Round(item.Bounds) : Rectangle.Empty;
        }

        private static bool EditableCalendarDeleted(SavedDesignerLayout layout, string id)
        {
            return layout.DeletedElementIds != null && layout.DeletedElementIds.Contains(id);
        }

        private bool RenderEditableCalendar(Graphics graphics, Rectangle bounds)
        {
            SavedDesignerLayout layout = EditableCalendarLayout();
            if (layout == null) return false;
            Size size = DesignSize();
            DesignerLayer background = layout.Elements.Find(delegate(
                DesignerLayer item) { return item != null &&
                    string.Equals(item.Id, "main-background",
                        StringComparison.OrdinalIgnoreCase); });
            bool backgroundDeleted = EditableCalendarDeleted(layout,
                "main-background");
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
            CalendarPalette palette = Palette();
            DesignerLayer highlight = EditableCalendarDeleted(layout, "today-highlight")
                ? null : layout.Elements.Find(delegate(DesignerLayer item)
                {
                    return item != null && item.Id == "today-highlight";
                });
            foreach (DesignerLayer item in layout.Elements)
            {
                if (item == null || !item.Visible || item.Surface != 0 || EditableCalendarDeleted(layout, item.Id)) continue;
                if (string.Equals(item.Id, "main-background",
                    StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(item.Id, "today-highlight",
                    StringComparison.OrdinalIgnoreCase)) continue;
                RenderEditableButtonBackground(graphics, item.Id, item.Bounds);
                Color? dynamicColor = null;
                int index;
                if (item.Binding == "Calendar: Date" && TryCalendarIndex(item.Id, "date-", 42, out index))
                {
                    DateTime date = CalendarCellDate(index);
                    if (date.Month != _displayMonth.Month) continue;
                    if (date.Date == _now.Date)
                    {
                        if (highlight != null)
                            DrawEditableTodayHighlight(graphics, highlight, item,
                                layout.Elements.Find(delegate(DesignerLayer anchor)
                                {
                                    return anchor != null && anchor.Id == highlight.AnchorId;
                                }));
                        else if (!EditableCalendarDeleted(layout, "today-highlight"))
                        {
                            RectangleF cell = item.Bounds;
                            float inset = IsArtDeco ? 17F : 7F;
                            using (var accent = new SolidBrush(palette.Accent))
                                graphics.FillEllipse(accent, cell.X + inset, cell.Y + 4,
                                    Math.Max(1, cell.Width - inset * 2), Math.Max(1, cell.Height - 8));
                        }
                    }
                    if (date.Date == _now.Date &&
                        ((highlight != null && highlight.Visible && highlight.Opacity > 0F) ||
                         (highlight == null && !EditableCalendarDeleted(layout, "today-highlight"))))
                        dynamicColor = TodayHighlightTextColor(highlight == null
                            ? palette.Accent : Color.FromArgb(highlight.ColorArgb));
                    else if (item.ColorArgb == palette.PrimaryText.ToArgb())
                        dynamicColor =
                            IsWeekend(date.DayOfWeek) ? palette.Weekend : palette.PrimaryText;
                }
                else if (item.Binding == "Calendar: Weekday" && item.ColorArgb == palette.SecondaryText.ToArgb() &&
                    TryCalendarIndex(item.Id, "weekday-", 7, out index))
                {
                    DayOfWeek day = (DayOfWeek)(((int)FirstDayOfWeek() + index) % 7);
                    dynamicColor = IsWeekend(day) ? palette.Weekend : palette.SecondaryText;
                }
                DesignerLayerPainter.Draw(graphics, item, delegate(DesignerLayer layer) {
                    return layer.Binding == "Calendar: Footer Heading"
                        ? (IsViewingCurrentMonth() ? "Today" : "Viewing")
                        : ResolveCalendarDesignerText(layer);
                }, dynamicColor);
            }
            if (_paused)
                using (var font = new Font("Segoe UI", 10F))
                using (var brush = new SolidBrush(palette.SecondaryText))
                    DrawCentered(graphics, "Paused", font, brush, new RectangleF(326, 61, 72, 18));
            return true;
        }
    }
}
