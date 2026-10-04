using System;
using System.Collections.Generic;
using System.Drawing;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Widgets.Weather;

namespace EmilyDesk.Designer
{
    internal sealed partial class DesignerForm
    {
        // Version zero was a flat native preview with optional user overlays.
        // Seed real layers once, then keep the saved document authoritative.
        private void CompleteEditableThemeLayout()
        {
            if (_layout.EditableLayerVersion >= 1)
            {
                EnsureCalendarMoveGroups();
                return;
            }
            var overlays = _layout.Elements ?? new List<DesignerElement>();
            _layout.Elements = new List<DesignerElement>();
            if (_widgetKind == "calendar") SeedEditableCalendar();
            else if (_widgetKind == "clock") SeedEditableClock();
            else SeedEditableWeather();
            foreach (DesignerElement element in overlays)
            {
                if (element == null) continue;
                _layout.Elements.RemoveAll(delegate(DesignerElement seed) { return seed.Id == element.Id; });
                _layout.Elements.Add(element);
            }
            if (_layout.DeletedElementIds != null)
                _layout.Elements.RemoveAll(delegate(DesignerElement item) {
                    return _layout.DeletedElementIds.Contains(item.Id);
                });
            _layout.EditableLayerVersion = 1;
            _layout.IndustrialDetailsLayoutVersion = 4;
            EnsureCalendarMoveGroups();
        }

        private void EnsureCalendarMoveGroups()
        {
            if (_widgetKind != "calendar" ||
                _layout == null || _layout.Elements == null) return;
            foreach (DesignerElement item in _layout.Elements)
            {
                if (item == null) continue;
                string id = item.Id ?? string.Empty;
                string binding = item.Binding ?? string.Empty;
                if (id.StartsWith("weekday-",
                        StringComparison.OrdinalIgnoreCase) ||
                    id.StartsWith("date-",
                        StringComparison.OrdinalIgnoreCase) ||
                    binding.Equals("Calendar: Weekday",
                        StringComparison.OrdinalIgnoreCase) ||
                    binding.Equals("Calendar: Date",
                        StringComparison.OrdinalIgnoreCase))
                {
                    item.MoveGroup = "calendar-grid-text";
                    item.MoveWithGroup = true;
                }
            }
            _layout.CalendarMoveGroupVersion = 1;
        }

        private Color EditableInk(bool secondary)
        {
            if (_weatherTheme == "Modern")
                return secondary ? ModernThemePainter.SecondaryText : ModernThemePainter.PrimaryText;
            if (_weatherTheme == "Vintage")
                return secondary ? VintageThemePainter.MutedInk : VintageThemePainter.Ink;
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_weatherTheme);
            return secondary ? theme.SecondaryText : theme.PrimaryText;
        }

        private DesignerElement ThemeText(string id, string name, string text,
            string binding, float x, float y, float w, float h, float font,
            bool bold, bool secondary = false, DesignerSurface surface = DesignerSurface.Main)
        {
            DesignerElement item = CreateTextElement(id, name, text, binding, x, y, w, h, font, bold);
            item.FontName = _weatherTheme == "Vintage" ? "Georgia" : "Segoe UI";
            item.ColorArgb = EditableInk(secondary).ToArgb();
            item.Alignment = DesignerTextAlignment.Left;
            item.WordWrap = false;
            item.Trimming = DesignerTextTrimming.EllipsisCharacter;
            item.Surface = surface;
            _layout.Elements.Add(item);
            return item;
        }

        private void ThemeRule(string id, float x, float y, float w, float h,
            DesignerSurface surface = DesignerSurface.Main)
        {
            DesignerElement item = CreateDividerElement(id, id.Replace('-', ' '), x, y, w, h);
            item.ColorArgb = EmilyDeskThemeCatalog.Get(_weatherTheme).Border.ToArgb();
            item.Opacity = .3F;
            item.Surface = surface;
            _layout.Elements.Add(item);
        }

        private void SeedEditableCalendar()
        {
            bool deco = _weatherTheme == "Art Deco";
            DesignerElement month = ThemeText("month-title", "Month and Year", "September 2026",
                "Calendar: Month and Year", deco ? 131 : 82, deco ? 74 : 18, deco ? 260 : 256, deco ? 30 : 38, 17, true);
            if (deco) month.FontName = "Georgia";
            ThemeText("previous-button", "Previous Month", "\u2039", "Calendar: Previous Month",
                deco ? 52 : 22, deco ? 72 : 20, deco ? 38 : 42, deco ? 30 : 36, 17, true);
            ThemeText("next-button", "Next Month", "\u203a", "Calendar: Next Month",
                deco ? 432 : 356, deco ? 72 : 20, deco ? 38 : 42, deco ? 30 : 36, 17, true);
            ThemeText("today-button", "Today Button", "Today", "Calendar: Today",
                deco ? 390 : 174, deco ? 284 : 52, deco ? 80 : 72, deco ? 23 : 25, 10, false);
            if (!deco)
                ThemeText("footer-title", "Footer Heading", "Today", "Calendar: Footer Heading",
                    22, 335, 376, 19, 9.5F, true);
            ThemeText("footer", "Full Date", "Tuesday, September 1, 2026", "Calendar: Full Date",
                deco ? 62 : 22, deco ? 286 : 353, deco ? 315 : 376, deco ? 20 : 24, 10, false, !deco);
            RectangleF grid = deco ? new RectangleF(47, 111, 428, 171) : new RectangleF(22, 82, 376, 248);
            float header = deco ? 24 : 28;
            float width = grid.Width / 7F, height = (grid.Height - header) / 6F;
            string[] days = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            for (int column = 0; column < 7; column++)
                ThemeText("weekday-" + column, "Weekday " + (column + 1), days[column], "Calendar: Weekday",
                    grid.X + column * width, grid.Y, width, header, deco ? 9 : 10, deco, true);
            for (int index = 0; index < 42; index++)
                ThemeText("date-" + index, "Date Cell " + (index + 1),
                    index >= 2 && index <= 31 ? (index - 1).ToString() : "",
                    "Calendar: Date", grid.X + index % 7 * width, grid.Y + header + index / 7 * height,
                    width, height, deco ? 10 : 10.5F, false);
            for (int column = 0; column <= 7; column++)
                ThemeRule("grid-column-" + column, grid.X + column * width,
                    grid.Y + (deco ? 0 : header), 1, grid.Height - (deco ? 0 : header));
            for (int row = 0; row <= 6; row++)
                ThemeRule("grid-row-" + row, grid.X, grid.Y + header + row * height, grid.Width, 1);
            foreach (DesignerElement item in _layout.Elements)
            {
                if (item.Kind == DesignerElementKind.Text) item.Alignment = DesignerTextAlignment.Center;
                else if (item.Kind == DesignerElementKind.Divider)
                {
                    if (_weatherTheme == "Modern") item.ColorArgb = ModernThemePainter.Border.ToArgb();
                    if (_weatherTheme == "Vintage") item.ColorArgb = VintageThemePainter.DarkBrass.ToArgb();
                    item.Opacity = (deco ? 80F : _weatherTheme == "Modern" ? 45F : 54F) / 255F;
                }
            }
        }

        private void SeedEditableClock()
        {
            bool deco = _weatherTheme == "Art Deco", vintage = _weatherTheme == "Vintage";
            float cx = deco ? 180 : 150, cy = deco ? 174 : vintage ? 149 : 140;
            string[] romans = { "XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
            int count = deco || vintage ? 12 : 4;
            for (int index = 0; index < count; index++)
            {
                double angle = index * Math.PI * 2 / count - Math.PI / 2;
                float radius = deco ? 108 : vintage ? 91 : 88;
                float w = vintage ? 38 : 32, h = deco ? 28 : vintage ? 22 : 20;
                string label = vintage ? romans[index] : (index == 0 ? 12 : index * 12 / count).ToString();
                DesignerElement number = ThemeText("clock-number-" + index, "Dial " + label, label, "None",
                    cx + (float)Math.Cos(angle) * radius - w / 2, cy + (float)Math.Sin(angle) * radius - h / 2,
                    w, h, deco ? 13 : vintage ? 11.5F : 10, deco || vintage, !deco && !vintage);
                number.FontName = deco || vintage ? "Georgia" : "Segoe UI Semibold";
                number.Alignment = DesignerTextAlignment.Center;
            }
            string[] names = { "Hour", "Minute", "Second" };
            float[] lengths = deco ? new[] { 48.64F, 76.8F, 108F } : vintage
                ? new[] { 58F, 84F, 96F } : new[] { 57F, 83F, 96F };
            float[] widths = deco ? new[] { 4.8F, 4F, 1.7F } : vintage
                ? new[] { 13F, 9F, 1.4F } : new[] { 14F, 8.4F, 1.5F };
            float[] rotations = { 302.4F, 61.2F, 129.6F };
            for (int i = 0; i < 3; i++)
            {
                DesignerElement hand = CreateImageElement("clock-" + names[i].ToLowerInvariant() + "-hand",
                    names[i] + " Hand", "Clock: " + names[i] + " Hand",
                    cx - widths[i] / 2, cy - lengths[i], widths[i], lengths[i] + (i == 2 ? 17 : 13));
                hand.PreviewRotation = rotations[i];
                hand.ColorArgb = (i != 2 ? EditableInk(false) : vintage ? Color.FromArgb(159, 69, 58)
                    : deco ? Color.FromArgb(45, 184, 174) : ModernThemePainter.AccentBright).ToArgb();
                _layout.Elements.Add(hand);
            }
            float diameter = deco ? 11 : vintage ? 12 : 10;
            DesignerElement hub = CreateImageElement("clock-centre-pivot", "Centre Pivot", "Clock: Centre Pivot",
                cx - diameter / 2, cy - diameter / 2, diameter, diameter);
            hub.ColorArgb = (vintage ? VintageThemePainter.Brass : deco
                ? EmilyDeskThemeCatalog.Get(_weatherTheme).Accent : ModernThemePainter.AccentBright).ToArgb();
            _layout.Elements.Add(hub);
            DesignerElement date = ThemeText("clock-date", "Date", "Tuesday, September 1",
                "Clock: Date", deco ? 133 : 36, deco ? 232 : 255, deco ? 94 : 228, deco ? 24 : 20,
                deco ? 9 : 8.5F, false, true);
            date.Alignment = DesignerTextAlignment.Center;
            date.FontName = deco || vintage ? "Georgia" : "Segoe UI";
            date.Italic = vintage;
            date.Visible = deco || vintage;
        }

        private void SeedEditableWeather()
        {
            bool modern = _weatherTheme == "Modern";
            ThemeText("location", "City", "Port Williams", "Weather: Location", 22, 16, 316, 20, 9, false, true);
            ThemeText("temperature", "Temperature", "21\u00b0C", "Weather: Temperature", 138, 42, 200, 56, 36, true);
            ThemeText("condition", "Condition", "Partly Cloudy", "Weather: Condition", 140, 99, 198, 23, 12, true);
            ThemeText("high-low", "High / Low", "24\u00b0C / 15\u00b0C",
                "Weather: Forecast High and Low", 140, 128, 198, 18, 9, false, true);
            _layout.Elements.Add(CreateImageElement("current-icon", "Current Weather Icon",
                "Weather: Current Icon", 22, 50, 96, 92));
            ThemeRule("main-divider", 22, modern ? 150 : 154, 316, 1);
            if (!modern) ThemeRule("main-divider-second", 22, 157, 316, 1);
            string[] ids = { "feels-like", "humidity", "wind" };
            string[] labels = { "Feels Like", "Humidity", "Wind" };
            string[] values = { "20\u00b0C", "68%", "12 km/h" };
            for (int i = 0; i < 3; i++)
            {
                ThemeText(ids[i] + "-label", labels[i] + " Label", labels[i], "None",
                    22 + i * 109, modern ? 153 : 158, 98, 14, 9, false, true);
                ThemeText(ids[i], labels[i], values[i], "Weather: " + labels[i],
                    22 + i * 109, modern ? 167 : 172, 98, 14, 8.625F, true);
            }
            ThemeText("footer", "Updated Time", "Updated 9:30 AM", "Weather: Updated Time",
                22, modern ? 188 : 187, 208, 14, 7.125F, false, true).Italic = true;
            DesignerElement button = CreateImageElement("panel-button", "Weather Details Button",
                "Control: Weather Details Button", 326, 181, 23, 23);
            button.ColorArgb = EditableInk(false).ToArgb();
            button.FontSize = 9.2F;
            _layout.Elements.Add(button);
            DesignerSurface details = DesignerSurface.WeatherDetails;
            ThemeText("details-title", "Panel Title", "4-Day Forecast", "None",
                20, 16, 240, 23, 12, true, false, details);
            ThemeRule("details-header-divider", 20, 43, 242, 1, details);
            if (!modern) ThemeRule("details-header-second", 20, 46, 242, 1, details);
            ThemeRule("details-footer-divider", 20, 198, 242, 1, details);
            for (int i = 0;
                i < NativeWeatherWidget.DefaultForecastDayCount; i++)
            {
                float y = 52 + i * 29;
                ThemeText("forecast-day-" + i, "Forecast " + (i + 1) + " Day", "Today",
                    "Weather: Forecast Day", 20, y, 58, 18, 8.25F, true, false, details);
                DesignerElement icon = CreateImageElement("forecast-icon-" + i, "Forecast " + (i + 1) + " Icon",
                    "Weather: Forecast Icon", 78, y - 3, 26, 24);
                icon.Surface = details;
                _layout.Elements.Add(icon);
                ThemeText("forecast-condition-" + i, "Forecast " + (i + 1) + " Condition", "Cloudy",
                    "Weather: Forecast Condition", 111, y, 91, 18, 7.5F, false, true, details);
                ThemeText("forecast-range-" + i, "Forecast " + (i + 1) + " High / Low", "24\u00b0 / 15\u00b0",
                    "Weather: Forecast High and Low", 194, y, 65, 18, 8.25F, true, false, details)
                    .Alignment = DesignerTextAlignment.Right;
            }
        }
    }
}
