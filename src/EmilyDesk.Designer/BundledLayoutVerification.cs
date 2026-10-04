using System;
using System.Drawing;
using System.IO;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Widgets.Clock;
using XWidgetReborn.Widgets.Calendar;
using XWidgetReborn.Widgets.Weather;

namespace EmilyDesk.Designer
{
    internal static partial class DesignerSaveVerification
    {
        private static void CheckBundledLayouts(string directory)
        {
            string shared = Path.Combine(directory, "bundle-shared.json");
            string local = Path.Combine(directory, "bundle-local.json");
            string bundled = Path.Combine(directory, "bundle-default.json");
            File.WriteAllText(bundled, "{\"CanvasWidth\":300}");
            Require(DesignerLayoutFiles.PreferredPath(shared, local, bundled) == bundled,
                "A clean installation did not select its bundled layout.");
            File.WriteAllText(shared, "{\"CanvasWidth\":100}");
            File.SetLastWriteTimeUtc(shared, DateTime.UtcNow.AddDays(-2));
            Require(DesignerLayoutFiles.PreferredPath(shared, local, bundled) == shared,
                "A newer package timestamp displaced a personal shared layout.");
            File.WriteAllText(local, "{\"CanvasWidth\":200}");
            File.SetLastWriteTimeUtc(local, DateTime.UtcNow.AddDays(-1));
            Require(DesignerLayoutFiles.PreferredPath(shared, local, bundled) == local,
                "A newer personal edit did not take precedence over the bundle.");
            File.SetLastWriteTimeUtc(shared, File.GetLastWriteTimeUtc(local));
            Require(DesignerLayoutFiles.PreferredPath(shared, local, bundled) == local,
                "Local/shared timestamp ties no longer prefer the local save.");
            File.SetLastWriteTimeUtc(shared, DateTime.UtcNow);
            Require(DesignerLayoutFiles.PreferredPath(shared, local, bundled) == shared,
                "A newer shared edit was ignored.");

            string[,] routes = {
                { "Art Deco", "weather" }, { "Botanical Nature", "weather" },
                { "Industrial", "calendar" }, { "Industrial", "clock" },
                { "Industrial", "weather" }, { "Steampunk", "calendar" },
                { "Steampunk", "weather" }, { "Vintage", "calendar" },
                { "Woodland Nature", "calendar" }, { "Woodland Nature", "clock" },
                { "Woodland Nature", "weather" }
            };
            for (int index = 0; index < routes.GetLength(0); index++)
            {
                string theme = routes[index, 0], kind = routes[index, 1];
                string name = DesignerLayoutFiles.FileName(theme, kind);
                string source = DesignerLayoutFiles.BundledPath(name);
                Require(!string.IsNullOrEmpty(source) && File.Exists(source),
                    "Bundled layout is missing: " + name);
                byte[] original = File.ReadAllBytes(source);
                DesignerLayout imported = DesignerLayoutStore.Load(source);
                Require(imported.CanvasWidth > 0 && imported.CanvasHeight > 0 &&
                    imported.Elements != null && imported.Elements.Count > 0,
                    "Bundled layout has no usable canvas/layers: " + name);
                int savedForecastDays = kind == "weather"
                    ? ForecastLayerCount(imported) : 0;
                string isolated = Path.Combine(directory, "bundle-case-" + index);
                Directory.CreateDirectory(isolated);
                using (DesignerLayoutFiles.UseIsolatedDirectory(isolated))
                {
                    // Isolation must not load real personal or bundled defaults.
                    Require(!File.Exists(DesignerLayoutFiles.LivePath(name)),
                        "Isolated verification leaked a saved or bundled layout.");
                    File.Copy(source, Path.Combine(isolated, name));
                    using (var form = new DesignerForm(kind, theme))
                    using (IWidget widget = kind == "clock" ? (IWidget)new NativeClockWidget() :
                        kind == "calendar" ? (IWidget)new NativeCalendarWidget() : new NativeWeatherWidget())
                    {
                        Require(Field<string>(form, "_liveLayoutFileName") == name,
                            "Bundled Designer/runtime routing differs: " + name);
                        if (kind == "weather")
                            Require(ForecastLayerCount(Field<DesignerLayout>(
                                form, "_layout")) == savedForecastDays,
                                "Opening a weather layout changed its forecast-day count: " +
                                name);
                        ((IWidgetThemeProvider)widget).Theme = theme;
                        // No Start/AttachHost: no network requests or persisted host changes.
                        Size size = kind == "weather" ? (Size)PresentationValue(widget, "CurrentPreferredSize") :
                            new Size(imported.CanvasWidth, imported.CanvasHeight);
                        Render(widget, size);
                        if (kind == "weather")
                        {
                            SetWeatherPanel(widget, true);
                            Render(widget, size);
                            SetWeatherPanel(widget, false);
                        }
                        using (DesignerLayoutFiles.SuspendSavedLayouts())
                            Require(SavedDesignerLayout.Current(theme, kind) == null,
                                "Built-in reference rendering did not suppress saved layouts.");
                        string published = DesignerLayoutStore.PublishLive(name, imported, null);
                        Require(published == Path.Combine(isolated, name) && published != source,
                            "Saving a bundled layout attempted to overwrite the shipped default.");
                    }
                }
                Require(Equal(original, File.ReadAllBytes(source)),
                    "Opening/rendering/saving modified the bundled original: " + name);
            }
        }

        private static int ForecastLayerCount(DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return 0;
            int count = 0;
            foreach (DesignerElement element in layout.Elements)
            {
                if (element == null || !element.Visible || element.Surface !=
                    DesignerSurface.Main) continue;
                string id = element.Id ?? string.Empty;
                string[] prefixes = { "forecast-day-", "forecast-icon-",
                    "forecast-range-", "forecast-high-", "forecast-low-",
                    "forecast-condition-" };
                foreach (string prefix in prefixes)
                {
                    if (!id.StartsWith(prefix,
                        StringComparison.OrdinalIgnoreCase)) continue;
                    int index;
                    if (int.TryParse(id.Substring(prefix.Length), out index))
                        count = Math.Max(count, index + 1);
                    break;
                }
            }
            return count;
        }
    }
}
