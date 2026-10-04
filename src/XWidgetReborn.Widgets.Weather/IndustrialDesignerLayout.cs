using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;

namespace XWidgetReborn.Widgets.Weather
{
    internal sealed class IndustrialDesignerLayout
    {
        public int EditableLayerVersion { get; set; }
        public int BackgroundLayerVersion { get; set; }
        public List<IndustrialDesignerElement> Elements { get; set; }
        public List<string> DeletedElementIds { get; set; }
        public IndustrialDesignerPanelSettings WeatherDetailsPanel { get; set; }
        public int IndustrialDetailsLayoutVersion { get; set; }
        public string BackgroundImage { get; set; }
        public bool ReplaceDefaultBackground { get; set; }
        public string WeatherIconPack { get; set; }

        public IndustrialDesignerLayout()
        {
            Elements = new List<IndustrialDesignerElement>();
            DeletedElementIds = new List<string>();
        }

        public IndustrialDesignerElement Find(string id)
        {
            if (IsDeleted(id))
                return new IndustrialDesignerElement { Id = id, Visible = false, Scale = 1F };
            return Elements == null ? null : Elements.Find(delegate(IndustrialDesignerElement item)
            {
                return item != null && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase);
            });
        }

        public bool IsDeleted(string id)
        {
            return DeletedElementIds != null &&
                DeletedElementIds.Exists(delegate(string deletedId)
                {
                    return string.Equals(deletedId, id,
                        StringComparison.OrdinalIgnoreCase);
                });
        }

        public static string LivePath
        {
            get { return ResolveLivePath("industrial-weather.layout.json"); }
        }

        private static string ResolveLivePath(string layoutFileName)
        {
            return XWidgetReborn.WidgetSdk.DesignerLayoutFiles.LivePath(layoutFileName);
        }

        private static string LegacyLivePath
        {
            get { return LegacyLivePathFor("industrial-weather.layout.json"); }
        }

        private static string LegacyLivePathFor(string layoutFileName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", layoutFileName);
        }

        public static IndustrialDesignerLayout Current()
        {
            return Current("industrial-weather.layout.json");
        }

        public static IndustrialDesignerLayout Current(string layoutFileName)
        {
            IndustrialDesignerLayout layout = XWidgetReborn.WidgetSdk.DesignerLayoutFiles
                .Load<IndustrialDesignerLayout>(layoutFileName);
            if (layout == null || layout.Elements == null) return null;
            MigrateLegacyDetailsRowOrder(layout);
            return layout;
        }

        public static void Reload()
        {
            XWidgetReborn.WidgetSdk.DesignerLayoutFiles.Reload();
        }

        // Match the complete Art Deco detail set.  The original Industrial
        // layout only had six fields across three rows; that left the panel
        // incomplete and made later row-order repairs look misaligned.
        private static void MigrateLegacyDetailsRowOrder(
            IndustrialDesignerLayout layout)
        {
            const int targetVersion = 4;
            if (layout.IndustrialDetailsLayoutVersion >= targetVersion) return;
            if (layout.Find("details-title") == null) return;

            string[] labels = { "Feels Like", "Humidity", "Wind",
                "Pressure", "Dew Point", "Visibility", "UV Index",
                "Sunrise / Sunset" };
            string[] values = { "20\u00B0", "68%", "NW  12 km/h",
                "1013 hPa", "12\u00B0", "16 km", "3  Moderate",
                "6:15 AM / 8:32 PM" };
            string[] bindings = { "Weather: Feels Like", "Weather: Humidity",
                "Weather: Wind", "Weather: Pressure", "Weather: Dew Point",
                "Weather: Visibility", "Weather: UV Index",
                "Weather: Sunrise and Sunset" };

            const float leftColumnX = 58F;
            const float rightColumnX = 404F;
            const float columnWidth = 280F;
            const float rowHeight = 82F;
            for (int index = 0; index < labels.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? leftColumnX : rightColumnX;
                float y = 112F + row * rowHeight;
                IndustrialDesignerElement label = layout.Find(
                    "details-label-" + index);
                if (label == null)
                {
                    label = CreateDetailsText("details-label-" + index,
                        "Detail " + (index + 1) + " Label", "None", x, y,
                        columnWidth, 18F, 13F, false);
                    layout.Elements.Add(label);
                }
                label.Text = labels[index];
                ConfigureDetailsText(label, "Detail " + (index + 1) +
                    " Label", "None", x, y, columnWidth, 18F, 13F, false);
                label.Visible = index >= 2;

                IndustrialDesignerElement value = layout.Find(
                    "details-value-" + index);
                if (value == null)
                {
                    value = CreateDetailsText("details-value-" + index,
                        "Detail " + (index + 1) + " Value", bindings[index],
                        x, y + 19F, columnWidth, 23F, 16F, true);
                    layout.Elements.Add(value);
                }
                value.Text = values[index];
                ConfigureDetailsText(value, "Detail " + (index + 1) +
                    " Value", bindings[index], x, y + 19F, columnWidth,
                    23F, 16F, true);
            }

            for (int index = 0; index < 6; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? leftColumnX : rightColumnX;
                float y = 112F + row * rowHeight + 76F;
                IndustrialDesignerElement divider = layout.Find(
                    "details-row-divider-" + index);
                if (divider == null)
                {
                    divider = new IndustrialDesignerElement();
                    divider.Id = "details-row-divider-" + index;
                    layout.Elements.Add(divider);
                }
                divider.Name = "Detail Row Divider " + (index + 1);
                divider.Kind = 2;
                divider.Surface = 1;
                divider.Binding = "Layout: Divider";
                divider.X = x;
                divider.Y = y;
                divider.Width = columnWidth;
                divider.Height = 2F;
                divider.ColorArgb = Color.FromArgb(224, 157, 39).ToArgb();
                divider.Visible = true;
            }
            foreach (IndustrialDesignerElement element in layout.Elements)
            {
                if (element == null || element.Surface != 1 ||
                    element.Kind != 0 || !string.Equals(element.Name,
                        "New Text", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(element.Binding,
                    "Weather: Sunrise and Sunset",
                    StringComparison.OrdinalIgnoreCase))
                    element.Visible = false;
            }
            layout.IndustrialDetailsLayoutVersion = targetVersion;
        }

        private static IndustrialDesignerElement CreateDetailsText(string id,
            string name, string binding, float x, float y, float width,
            float height, float fontSize, bool bold)
        {
            var element = new IndustrialDesignerElement();
            element.Id = id;
            element.Text = string.Empty;
            ConfigureDetailsText(element, name, binding, x, y, width, height,
                fontSize, bold);
            return element;
        }

        private static void ConfigureDetailsText(
            IndustrialDesignerElement element, string name, string binding,
            float x, float y, float width, float height, float fontSize,
            bool bold)
        {
            element.Name = name;
            element.Kind = 0;
            element.Surface = 1;
            element.Binding = binding;
            element.X = x;
            element.Y = y;
            element.Width = width;
            element.Height = height;
            element.Visible = true;
            element.Opacity = 1F;
            element.Scale = 1F;
            element.FontName = "Segoe UI Semibold";
            element.FontSize = fontSize;
            element.Bold = bold;
            element.Italic = false;
            element.ColorArgb = Color.FromArgb(244, 228, 192).ToArgb();
            element.Alignment = 0;
            element.WordWrap = false;
            element.Trimming = 0;
        }
    }

    internal sealed class IndustrialDesignerPanelSettings
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public int Direction { get; set; }
        public float Offset { get; set; }
        public int State { get; set; }
        public int Animation { get; set; }
        public int DurationMilliseconds { get; set; }
    }

    internal sealed class IndustrialDesignerElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Binding { get; set; }
        public int Kind { get; set; }
        public int Surface { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool Visible { get; set; }
        public bool MouseLocked { get; set; }
        public float Opacity { get; set; }
        public float Scale { get; set; }
        public string FontName { get; set; }
        public string FontFile { get; set; }
        public float FontSize { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public int ColorArgb { get; set; }
        public int Alignment { get; set; }
        public bool? WordWrap { get; set; }
        public int Trimming { get; set; }
        public int ImageHorizontalPlacement { get; set; }
        public int ImageVerticalPlacement { get; set; }
        public float ImageOffsetX { get; set; }
        public float ImageOffsetY { get; set; }
        public string ImagePath { get; set; }

        public RectangleF Bounds
        {
            get
            {
                float scale = Math.Max(.05F, Scale);
                return new RectangleF(X, Y, Width * scale, Height * scale);
            }
        }
    }
}
