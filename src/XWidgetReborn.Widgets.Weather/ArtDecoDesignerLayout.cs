using System;
using System.IO;
using System.Web.Script.Serialization;

namespace XWidgetReborn.Widgets.Weather
{
    internal static class ArtDecoDesignerLayout
    {
        public static IndustrialDesignerLayout Current()
        {
            return XWidgetReborn.WidgetSdk.DesignerLayoutFiles
                .Load<IndustrialDesignerLayout>("art-deco-weather.layout.json");
        }

        public static void Reload()
        {
            XWidgetReborn.WidgetSdk.DesignerLayoutFiles.Reload();
        }
    }
}
