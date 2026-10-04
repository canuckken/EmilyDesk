using System;
using System.IO;
using XWidgetReborn.WidgetSdk;

namespace EmilyDesk.Designer
{
    internal sealed partial class DesignerForm
    {
        private void LoadRecycleBinLayout()
        {
            _liveLayoutFileName = DesignerLayoutFiles.FileName(_weatherTheme, "recyclebin");
            string path = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
            DesignerLayout layout;
            if (File.Exists(path)) layout = DesignerLayoutStore.Load(path);
            else
            {
                layout = new DesignerLayout { Name = _weatherTheme + " Recycle Bin",
                    SchemaVersion = 7, CanvasWidth = 240, CanvasHeight = 224 };
                string prefix = _weatherTheme == "Art Deco" ? "art-deco" :
                    _weatherTheme == "Botanical Nature" ? "botanical" :
                    _weatherTheme == "Woodland Nature" ? "woodland" : _weatherTheme.ToLowerInvariant();
                foreach (string state in new[] { "empty", "full" })
                {
                    EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_weatherTheme);
                    string themeImage = theme.IsImported
                        ? theme.Asset(state == "full" ? "recycleFull" : "recycleEmpty")
                        : "Assets/Themes/" + _weatherTheme.Replace(" ", "") + "/" +
                            prefix + "-recycle-bin-" + state + ".png";
                    layout.Elements.Add(new DesignerElement { Id = "bin-" + state,
                        Name = state == "full" ? "Full bin artwork" : "Empty bin artwork",
                        BindingDomain = "Recycle Bin", Binding = "Recycle Bin: " + (state == "full" ? "Full" : "Empty"),
                        Kind = DesignerElementKind.Image, Surface = state == "full" ? DesignerSurface.WeatherDetails : DesignerSurface.Main,
                        X = 8, Y = 0, Width = 224, Height = 224,
                        ImagePath = themeImage });
                }
            }
            _surface.Enabled = true;
            SetLayout(layout);
        }
    }
}
