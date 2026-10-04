using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Widgets.Calendar;
using XWidgetReborn.Widgets.Weather;

namespace EmilyDesk.Designer
{
    internal static partial class DesignerSaveVerification
    {
        // No host, live weather fetches or real user layouts are involved.
        // This runs under Run's isolated Designer-layout directory.
        private static object PresentationValue(object widget, string method)
        {
            return widget.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(widget, null);
        }

        private static void CheckWoodlandPresentationSize()
        {
            using (var calendar = new NativeCalendarWidget())
            using (var weather = new NativeWeatherWidget())
            {
                calendar.Theme = "Woodland Nature";
                Rectangle calendarFrame = (Rectangle)PresentationValue(calendar, "IndustrialVisibleBounds");
                foreach (string theme in new[] { "Woodland Nature", "Industrial", "Art Deco",
                    "Steampunk", "Botanical Nature", "Woodland Nature" })
                {
                    weather.Theme = theme;
                    Size surface = (Size)PresentationValue(weather, "FixedCompositionSurfaceSize");
                    Rectangle anchor = (Rectangle)PresentationValue(weather, "FixedCompositionAnchorBounds");
                    Size preferred = (Size)PresentationValue(weather, "CurrentPreferredSize");
                    Require(preferred == surface, theme + " preferred size differs from its fixed surface.");
                    if (theme != "Woodland Nature")
                    {
                        Require(surface == new Size(1081, 494), theme + " presentation size changed.");
                        RectangleF designAnchor = (RectangleF)PresentationValue(weather, "CompositionAnchorBounds");
                        Rectangle original = Rectangle.Round(new RectangleF(designAnchor.X * .75F,
                            designAnchor.Y * .75F, designAnchor.Width * .75F, designAnchor.Height * .75F));
                        Require(anchor == original, theme + " edge anchor changed.");
                        continue;
                    }
                    Require(surface == new Size(1141, 524), "Woodland weather did not use the enlarged surface.");
                    Require(Math.Abs(anchor.Width - calendarFrame.Width) <= 1 &&
                        Math.Abs(anchor.Height - calendarFrame.Height) <= 1,
                        "Woodland weather/calendar visible frames differ at 100%.");
                    foreach (float scale in new[] { .5F, .75F, 1F, 1.25F, 1.5F, 2F })
                    {
                        Require(Math.Abs(Math.Round(anchor.Width * scale) -
                            Math.Round(calendarFrame.Width * scale)) <= 2 &&
                            Math.Abs(Math.Round(anchor.Height * scale) -
                            Math.Round(calendarFrame.Height * scale)) <= 2,
                            "Woodland size parity was lost when changing scale.");
                    }
                    string file = DesignerLayoutFiles.LivePath(
                        DesignerLayoutFiles.FileName(theme, "weather"));
                    byte[] saved = File.ReadAllBytes(file);
                    Render(weather, surface);
                    SetWeatherPanel(weather, true);
                    Render(weather, surface);
                    Require((Size)PresentationValue(weather, "FixedCompositionSurfaceSize") == surface,
                        "Opening Woodland details resized the fixed surface.");
                    SetWeatherPanel(weather, false);
                    Require(Equal(saved, File.ReadAllBytes(file)),
                        "Woodland presentation resizing rewrote saved Designer edits.");
                }
            }
        }
    }
}
