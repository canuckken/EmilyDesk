using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;

namespace XWidgetReborn.Widgets.Calendar
{
    internal sealed class IndustrialCalendarDesignerLayout
    {
        public int SchemaVersion { get; set; }
        public string BackgroundImage { get; set; }
        public List<string> DeletedElementIds { get; set; }
        public List<IndustrialCalendarDesignerElement> Elements { get; set; }

        public IndustrialCalendarDesignerLayout()
        {
            Elements = new List<IndustrialCalendarDesignerElement>();
            DeletedElementIds = new List<string>();
        }

        public bool IsDeleted(string id)
        {
            return DeletedElementIds != null &&
                DeletedElementIds.Exists(delegate(string deleted)
                {
                    return string.Equals(deleted, id,
                        StringComparison.OrdinalIgnoreCase);
                });
        }

        public IndustrialCalendarDesignerElement Find(string id)
        {
            return Elements == null ? null : Elements.Find(
                delegate(IndustrialCalendarDesignerElement element)
                {
                    return element != null && string.Equals(element.Id, id,
                        StringComparison.OrdinalIgnoreCase);
                });
        }

        public static IndustrialCalendarDesignerLayout Current()
        {
            return Current("industrial-calendar.layout.json");
        }

        public static IndustrialCalendarDesignerLayout Current(string layoutFileName)
        {
            IndustrialCalendarDesignerLayout layout = XWidgetReborn.WidgetSdk.DesignerLayoutFiles
                .Load<IndustrialCalendarDesignerLayout>(layoutFileName);
            if (layout == null || layout.Elements == null) return null;
            if (layout.SchemaVersion < 5) MigrateProductionDefaults(layout);
            return layout;
        }

        private static void MigrateProductionDefaults(
            IndustrialCalendarDesignerLayout layout)
        {
            MigrateElement(layout, "month-title",
                135F, 59F, 270F, 31F,
                135F, 61F, 270F, 31F);
            MigrateElement(layout, "today-button",
                410F, 291F, 78F, 23F,
                410F, 281F, 78F, 19F);
            MigrateElement(layout, "previous-button",
                52F, 61F, 38F, 30F,
                52F, 70F, 38F, 30F);
            MigrateElement(layout, "next-button",
                450F, 61F, 38F, 30F,
                450F, 70F, 38F, 30F);
            MigrateElement(layout, "today-button",
                410F, 281F, 78F, 19F,
                410F, 300F, 78F, 18F);
            MigrateElement(layout, "footer",
                70F, 292F, 325F, 20F,
                70F, 282F, 325F, 17F);
            MigrateElement(layout, "footer",
                70F, 282F, 325F, 17F,
                65F, 300F, 275F, 18F);
            MigrateColour(layout, "today-button",
                410F, 300F, 78F, 18F,
                Color.FromArgb(244, 228, 192).ToArgb(),
                Color.FromArgb(192, 192, 192).ToArgb());
            MigrateElement(layout, "footer",
                65F, 300F, 275F, 18F,
                50F, 300F, 275F, 18F);
            MigrateColour(layout, "footer",
                50F, 300F, 275F, 18F,
                Color.FromArgb(157, 161, 160).ToArgb(),
                Color.FromArgb(192, 192, 192).ToArgb());
            layout.SchemaVersion = Math.Max(layout.SchemaVersion, 5);
        }

        private static void MigrateElement(
            IndustrialCalendarDesignerLayout layout, string id,
            float oldX, float oldY, float oldWidth, float oldHeight,
            float newX, float newY, float newWidth, float newHeight)
        {
            IndustrialCalendarDesignerElement element = layout.Find(id);
            if (element == null ||
                Math.Abs(element.X - oldX) > .01F ||
                Math.Abs(element.Y - oldY) > .01F ||
                Math.Abs(element.Width - oldWidth) > .01F ||
                Math.Abs(element.Height - oldHeight) > .01F) return;
            element.X = newX;
            element.Y = newY;
            element.Width = newWidth;
            element.Height = newHeight;
        }

        private static void MigrateColour(
            IndustrialCalendarDesignerLayout layout, string id,
            float x, float y, float width, float height,
            int oldArgb, int newArgb)
        {
            IndustrialCalendarDesignerElement element = layout.Find(id);
            if (element == null || element.ColorArgb != oldArgb ||
                Math.Abs(element.X - x) > .01F ||
                Math.Abs(element.Y - y) > .01F ||
                Math.Abs(element.Width - width) > .01F ||
                Math.Abs(element.Height - height) > .01F) return;
            element.ColorArgb = newArgb;
        }


    }

    internal sealed class IndustrialCalendarDesignerElement : XWidgetReborn.WidgetSdk.DesignerLayer
    {
    }
}
