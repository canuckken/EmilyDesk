using System;
using System.Collections.Generic;
using System.IO;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>
    /// Maps separately installed optional-widget packages to the shared theme
    /// choices displayed by the runtime context menu.
    /// </summary>
    public static class OptionalWidgetThemeCatalog
    {
        private static readonly string[] ThemeOrder =
        {
            "Steampunk", "Industrial", "Woodland Nature",
            "Botanical Nature", "Art Deco", "Ember Glow"
        };

        public static IEnumerable<string> AvailableThemes(string widgetId)
        {
            string family = Family(widgetId);
            string current = Theme(widgetId);
            if (family.Length == 0 || current.Length == 0)
                yield break;

            string root = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "EmilyDesk", "Widgets");
            foreach (string theme in ThemeOrder)
            {
                string candidate = WidgetId(family, theme);
                if (string.Equals(theme, current,
                        StringComparison.OrdinalIgnoreCase) ||
                    Directory.Exists(Path.Combine(root, candidate)))
                    yield return theme;
            }
        }

        public static string Theme(string widgetId)
        {
            if (string.IsNullOrWhiteSpace(widgetId))
                return string.Empty;
            if (widgetId.IndexOf(".industrial-",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Industrial";
            if (widgetId.IndexOf(".woodland-",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Woodland Nature";
            if (widgetId.IndexOf(".botanical-",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Botanical Nature";
            if (widgetId.IndexOf(".art-deco-",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Art Deco";
            if (widgetId.IndexOf(".ember-glow-",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ember Glow";
            return widgetId.StartsWith("utility.",
                StringComparison.OrdinalIgnoreCase)
                ? "Steampunk" : string.Empty;
        }

        public static string TargetWidgetId(
            string currentWidgetId,
            string theme)
        {
            string family = Family(currentWidgetId);
            return family.Length == 0
                ? string.Empty : WidgetId(family, theme);
        }

        private static string Family(string widgetId)
        {
            if (string.IsNullOrWhiteSpace(widgetId))
                return string.Empty;
            if (widgetId.EndsWith("calculator",
                    StringComparison.OrdinalIgnoreCase))
                return "calculator";
            if (widgetId.EndsWith("currency-converter",
                    StringComparison.OrdinalIgnoreCase))
                return "currency-converter";
            if (widgetId.EndsWith("system-info",
                    StringComparison.OrdinalIgnoreCase))
                return "system-info";
            return string.Empty;
        }

        private static string WidgetId(string family, string theme)
        {
            string prefix = string.Empty;
            if (string.Equals(theme, "Industrial",
                    StringComparison.OrdinalIgnoreCase))
                prefix = "industrial-";
            else if (string.Equals(theme, "Woodland Nature",
                    StringComparison.OrdinalIgnoreCase))
                prefix = "woodland-";
            else if (string.Equals(theme, "Botanical Nature",
                    StringComparison.OrdinalIgnoreCase))
                prefix = "botanical-";
            else if (string.Equals(theme, "Art Deco",
                    StringComparison.OrdinalIgnoreCase))
                prefix = "art-deco-";
            else if (string.Equals(theme, "Ember Glow",
                    StringComparison.OrdinalIgnoreCase))
                prefix = "ember-glow-";
            else if (!string.Equals(theme, "Steampunk",
                    StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return "utility." + prefix + family;
        }
    }
}
