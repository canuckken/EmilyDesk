using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;

namespace XWidgetReborn.Widgets.Clock
{
    internal sealed class IndustrialClockDesignerElement : XWidgetReborn.WidgetSdk.DesignerLayer
    {
    }

    internal sealed class IndustrialClockDesignerDocument
    {
        public int ClockHandEditVersion { get; set; }
        public string BackgroundImage { get; set; }
        public bool ReplaceDefaultBackground { get; set; }
        public List<string> DeletedElementIds { get; set; }
        public List<IndustrialClockDesignerElement> Elements { get; set; }
    }

    internal static class IndustrialClockDesignerLayout
    {
        private static Dictionary<string, IndustrialClockDesignerElement> _elements;
        private static IndustrialClockDesignerDocument _document;
        private static string _fileName = "industrial-clock.layout.json";
        public static bool HasSavedLayout { get { return _document != null; } }
        public static bool ReplaceDefaultBackground
        { get { return _document != null && _document.ReplaceDefaultBackground; } }
        public static int ClockHandEditVersion
        { get { return _document == null ? 0 : _document.ClockHandEditVersion; } }

        public static void Select(string fileName)
        {
            IndustrialClockDesignerDocument document = XWidgetReborn.WidgetSdk.DesignerLayoutFiles
                .Load<IndustrialClockDesignerDocument>(fileName);
            if (document != null && document.Elements == null) document = null;
            if (_elements != null && _fileName == fileName && ReferenceEquals(document, _document)) return;
            _fileName = fileName;
            _document = document;
            var result = Defaults();
            if (document != null && document.Elements != null)
            {
                // Absence in a complete saved clock means deletion, not restore.
                foreach (IndustrialClockDesignerElement item in result.Values) item.Visible = false;
                foreach (IndustrialClockDesignerElement item in document.Elements)
                    if (item != null && !string.IsNullOrEmpty(item.Id)) result[item.Id] = item;
            }
            _elements = result;
        }

        public static string BackgroundImage(string fallback)
        {
            return _document == null ? fallback : _document.BackgroundImage;
        }

        public static IndustrialClockDesignerElement BackgroundElement()
        {
            return _document == null || _document.Elements == null ||
                BackgroundDeleted
                ? null : _document.Elements.Find(delegate(
                    IndustrialClockDesignerElement element)
                {
                    return element != null &&
                        string.Equals(element.Id, "main-background",
                            StringComparison.OrdinalIgnoreCase);
                });
        }

        public static bool BackgroundDeleted
        {
            get
            {
                return _document != null &&
                    _document.DeletedElementIds != null &&
                    _document.DeletedElementIds.Exists(delegate(string id)
                    {
                        return string.Equals(id, "main-background",
                            StringComparison.OrdinalIgnoreCase);
                    });
            }
        }

        public static IndustrialClockDesignerElement Element(string id)
        {
            if (_elements == null) Select(_fileName);
            IndustrialClockDesignerElement element;
            return _elements.TryGetValue(id, out element) ? element : Default(id);
        }

        public static IndustrialClockDesignerElement BoundElement(string binding, string fallbackId)
        {
            if (_elements == null) Select(_fileName);
            // Prefer the built-in ID when duplicate information layers exist.
            IndustrialClockDesignerElement original;
            if (_elements.TryGetValue(fallbackId, out original) && original.Binding == binding)
                return original;
            foreach (IndustrialClockDesignerElement element in _elements.Values)
                if (string.Equals(element.Binding, binding, StringComparison.OrdinalIgnoreCase))
                    return element;
            return Element(fallbackId);
        }

        public static void Reload() { Reload(_fileName); }
        public static void Reload(string fileName)
        {
            XWidgetReborn.WidgetSdk.DesignerLayoutFiles.Reload();
            _elements = null; _document = null;
            Select(fileName);
        }

        private static Dictionary<string, IndustrialClockDesignerElement> Defaults()
        {
            var result = new Dictionary<string, IndustrialClockDesignerElement>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string id in new[] { "clock-hour-hand", "clock-minute-hand",
                "clock-second-hand", "clock-date", "clock-centre-pivot" })
                result[id] = Default(id);
            return result;
        }

        private static IndustrialClockDesignerElement Default(string id)
        {
            var element = new IndustrialClockDesignerElement
            {
                Id = id, Visible = true, Opacity = 1F, Scale = 1F,
                FontName = "Segoe UI", FontSize = 9F,
                Alignment = 1,
                ColorArgb = Color.FromArgb(174, 177, 176).ToArgb()
            };
            if (string.Equals(id, "clock-hour-hand", StringComparison.OrdinalIgnoreCase))
            { element.Binding = "Clock: Hour Hand"; element.X = 174.5F; element.Y = 103.5F; element.Width = 11F; element.Height = 86F; }
            else if (string.Equals(id, "clock-minute-hand", StringComparison.OrdinalIgnoreCase))
            { element.Binding = "Clock: Minute Hand"; element.X = 176F; element.Y = 68.5F; element.Width = 8F; element.Height = 121F; }
            else if (string.Equals(id, "clock-second-hand", StringComparison.OrdinalIgnoreCase))
            { element.Binding = "Clock: Second Hand"; element.X = 179F; element.Y = 60.5F; element.Width = 2F; element.Height = 133F; }
            else if (string.Equals(id, "clock-centre-pivot", StringComparison.OrdinalIgnoreCase))
            { element.Binding = "Clock: Centre Pivot"; element.X = 170F; element.Y = 166.5F; element.Width = 20F; element.Height = 20F; }
            else
            { element.Binding = "Clock: Date"; element.X = 133F; element.Y = 232F; element.Width = 94F; element.Height = 24F; }
            return element;
        }
    }
}
