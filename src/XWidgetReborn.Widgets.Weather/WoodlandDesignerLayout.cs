using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace XWidgetReborn.Widgets.Weather
{
    // Only the dedicated nature layout is loaded. Its complete layer document
    // is shared with the Designer; the original renderer remains the no-save default.
    internal sealed class WoodlandDesignerLayout
    {
        private const int SupportedVersion = 1;
        private static readonly object Sync = new object();
        private static IndustrialDesignerLayout _cached;
        private static WoodlandDesignerLayout _saved;

        public int WoodlandLayoutVersion { get; set; }
        public int BackgroundLayerVersion { get; set; }
        public IndustrialDesignerPanelSettings WeatherDetailsPanel { get; set; }
        public List<string> WoodlandChangedElementIds { get; set; }
        public List<IndustrialDesignerElement> Elements { get; set; }
        public string WeatherIconPack { get; set; }
        public string BackgroundImage { get; set; }
        public List<string> DeletedElementIds { get; set; }

        public static IndustrialDesignerLayout Current()
        {
            WoodlandDesignerLayout saved = XWidgetReborn.WidgetSdk.DesignerLayoutFiles
                .Load<WoodlandDesignerLayout>("woodland-weather.v2.layout.json");
            if (saved == null || saved.WoodlandLayoutVersion < SupportedVersion ||
                saved.Elements == null) return null;
            if (ReferenceEquals(saved, _saved) && _cached != null) return _cached;
            // The saved document is authoritative, including new layers and
            // details-panel edits. A changed-ID cache must not discard edits.
            var active = new IndustrialDesignerLayout {
                Elements = saved.Elements,
                EditableLayerVersion = 1,
                BackgroundLayerVersion = saved.BackgroundLayerVersion,
                IndustrialDetailsLayoutVersion = 4,
                WeatherDetailsPanel = saved.WeatherDetailsPanel,
                DeletedElementIds = saved.DeletedElementIds ?? new List<string>(),
                WeatherIconPack = saved.WeatherIconPack,
                BackgroundImage = saved.BackgroundImage
            };
            _saved = saved;
            _cached = active;
            return active;
        }

        public static void Reload()
        {
            _saved = null; _cached = null;
            XWidgetReborn.WidgetSdk.DesignerLayoutFiles.Reload();
        }
    }
}
