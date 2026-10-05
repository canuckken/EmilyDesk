using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Text;
using System.Web.Script.Serialization;

namespace EmilyDesk.Designer
{
    public enum DesignerElementKind { Text, Image, Divider }
    public enum DesignerTextAlignment { Left, Center, Right }
    public enum DesignerTextTrimming
    {
        None,
        Character,
        Word,
        EllipsisCharacter,
        EllipsisWord,
        EllipsisPath
    }
    public enum DesignerImageHorizontalPlacement { Center, Left, Right }
    public enum DesignerImageVerticalPlacement { Center, Top, Bottom }
    public enum DesignerSurface { Main, WeatherDetails }
    public enum DesignerPanelDirection { Left, Right, Top, Bottom }
    public enum DesignerPanelState { Collapsed, Open }
    public enum DesignerPanelAnimation { Instant, Quick, Smooth, Bouncy }

    public sealed class DesignerLayout
    {
        public int SchemaVersion { get; set; }
        public int EditableLayerVersion { get; set; }
        public int ClockHandEditVersion { get; set; }
        public int BackgroundLayerVersion { get; set; }
        public int CalendarMoveGroupVersion { get; set; }
        public string Name { get; set; }
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
        // Kept separate from the general schema number so a narrowly scoped
        // Industrial Weather correction never changes another widget type.
        public int IndustrialDetailsLayoutVersion { get; set; }
        // A separate version and changed-layer list protect Woodland's
        // hand-tuned presentation from legacy Industrial layout files.
        public int WoodlandLayoutVersion { get; set; }
        public List<string> WoodlandChangedElementIds { get; set; }
        // Built-in layouts are migrated as EmilyDesk evolves.  Keep an
        // explicit record of layers the user intentionally removed so a
        // completeness check or migration cannot recreate them later.
        public List<string> DeletedElementIds { get; set; }
        public string BackgroundImage { get; set; }
        public bool ReplaceDefaultBackground { get; set; }
        public string WeatherIconPack { get; set; }
        public string PackagePreviewImage { get; set; }
        public string PackageIconImage { get; set; }
        public bool GridEnabled { get; set; }
        public int GridSize { get; set; }
        public List<DesignerElement> Elements { get; set; }
        public DesignerPanelSettings WeatherDetailsPanel { get; set; }

        public DesignerLayout()
        {
            SchemaVersion = 2;
            Name = "Untitled Widget";
            CanvasWidth = 750;
            CanvasHeight = 500;
            GridSize = 10;
            Elements = new List<DesignerElement>();
            WoodlandChangedElementIds = new List<string>();
            DeletedElementIds = new List<string>();
            WeatherDetailsPanel = new DesignerPanelSettings();
        }

        public DesignerLayout Clone()
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Deserialize<DesignerLayout>(serializer.Serialize(this));
        }
    }

    public sealed class DesignerPanelSettings
    {
        [Category("Panel Position"), DisplayName("Horizontal adjustment")]
        [Description("Moves the entire panel. Positive values move it right; negative values move it left where space permits.")]
        public float PositionX { get; set; }
        [Category("Panel Position"), DisplayName("Vertical adjustment")]
        [Description("Moves the entire panel. Positive values move it down; negative values move it up where space permits.")]
        public float PositionY { get; set; }
        [Category("Slide Panel")]
        public DesignerPanelDirection Direction { get; set; }
        [Category("Slide Panel"), DisplayName("Visible overlap")]
        public float Offset { get; set; }
        [Category("Preview"), DisplayName("Preview state")]
        public DesignerPanelState State { get; set; }
        [Category("Animation")]
        public DesignerPanelAnimation Animation { get; set; }
        [Category("Animation"), DisplayName("Duration (milliseconds)")]
        public int DurationMilliseconds { get; set; }

        public DesignerPanelSettings()
        {
            Direction = DesignerPanelDirection.Left;
            Offset = 52F;
            State = DesignerPanelState.Open;
            Animation = DesignerPanelAnimation.Smooth;
            DurationMilliseconds = 340;
        }
    }

    public sealed class DesignerElement
    {
        [Browsable(false)]
        public string Id { get; set; }
        [Category("Layer")]
        public string Name { get; set; }
        [Category("Layer"), ReadOnly(true)]
        public DesignerElementKind Kind { get; set; }
        [Browsable(false)]
        public DesignerSurface Surface { get; set; }
        [Category("Position")]
        public float X { get; set; }
        [Category("Position")]
        public float Y { get; set; }
        [Category("Size")]
        public float Width { get; set; }
        [Category("Size")]
        public float Height { get; set; }
        [Browsable(false)]
        public bool Visible { get; set; }
        [Browsable(false), DisplayName("Lock cursor movement")]
        [Description("Prevents accidental mouse movement and resizing. Arrow-key positioning remains available.")]
        public bool MouseLocked { get; set; }
        [Browsable(false)]
        public string MoveGroup { get; set; }
        [Browsable(false)]
        public bool MoveWithGroup { get; set; }
        [Category("Appearance")]
        public float Opacity { get; set; }
        [Category("Appearance")]
        public float Scale { get; set; }
        [Browsable(false)]
        public float PreviewRotation { get; set; }
        [Category("Clock hand"), DisplayName("Image pivot X")]
        [Description("Rotation point inside the PNG, from 0 at the left edge to 1 at the right edge. Empty uses the older tail pivot.")]
        public float? HandPivotX { get; set; }
        [Category("Clock hand"), DisplayName("Image pivot Y")]
        [Description("Rotation point inside the PNG, from 0 at the top edge to 1 at the bottom edge. Use 0.5 for an XWidget-style centered canvas.")]
        public float? HandPivotY { get; set; }
        [Browsable(false), ScriptIgnore]
        public float PivotX
        {
            get
            {
                float width = Width * Scale;
                return X + (HandPivotX.HasValue
                    ? width * ClampPivot(HandPivotX.Value)
                    : width / 2F);
            }
            set
            {
                float width = Width * Scale;
                X = value - (HandPivotX.HasValue
                    ? width * ClampPivot(HandPivotX.Value)
                    : width / 2F);
            }
        }
        [Browsable(false), ScriptIgnore]
        public float PivotY
        {
            get
            {
                float height = Height * Scale;
                return Y + (HandPivotY.HasValue
                    ? height * ClampPivot(HandPivotY.Value)
                    : height - ClockTail);
            }
            set
            {
                float height = Height * Scale;
                Y = value - (HandPivotY.HasValue
                    ? height * ClampPivot(HandPivotY.Value)
                    : height - ClockTail);
            }
        }

        private static float ClampPivot(float value)
        {
            return Math.Max(0F, Math.Min(1F, value));
        }
        [Browsable(false), ScriptIgnore]
        public float HandLength
        {
            get { return Math.Max(0F, Height * Scale - ClockTail); }
            set { Height = (Math.Max(1F, value) + ClockTail) /
                    Math.Max(.1F, Scale); }
        }
        [Browsable(false), ScriptIgnore]
        public float Thickness
        {
            get { return Width * Scale; }
            set { Width = Math.Max(1F, value) / Math.Max(.1F, Scale); }
        }
        [Browsable(false)]
        private float ClockTail
        {
            get
            {
                return string.Equals(Binding, "Clock: Second Hand",
                    StringComparison.OrdinalIgnoreCase) ? 17F : 13F;
            }
        }
        [Category("Text")]
        public string Text { get; set; }
        [Category("Content"), DisplayName("Displayed information"),
         TypeConverter(typeof(WeatherBindingConverter))]
        public string Binding { get; set; }
        [Browsable(false)]
        public string BindingDomain { get; set; }
        [Browsable(false)]
        public string AnchorId { get; set; }
        [Category("Text"), DisplayName("Font"),
         TypeConverter(typeof(DesignerFontNameConverter))]
        public string FontName { get; set; }
        [Category("Text"), DisplayName("Widget font file")]
        [Description("Optional TTF or OTF file stored with this widget theme.")]
        public string FontFile { get; set; }
        [Category("Text"), DisplayName("Font size")]
        public float FontSize { get; set; }
        [Browsable(false)]
        public bool Bold { get; set; }
        [Browsable(false)]
        public bool Italic { get; set; }
        [Browsable(false)]
        public int ColorArgb { get; set; }
        [Browsable(false), DisplayName("Colour"), ScriptIgnore]
        public Color DisplayColor
        {
            get { return Color.FromArgb(ColorArgb); }
            set { ColorArgb = value.ToArgb(); }
        }
        [Category("Text")]
        public DesignerTextAlignment Alignment { get; set; }
        [Browsable(false), DisplayName("Word wrap")]
        [Description("Allows text to continue on another line inside its box.")]
        public bool WordWrap { get; set; }
        [Category("Text Format")]
        [Description("Controls how text that is too long for its box is shortened.")]
        public DesignerTextTrimming Trimming { get; set; }
        [Category("Image"), DisplayName("PNG file")]
        public string ImagePath { get; set; }
        [Category("Image"), DisplayName("Horizontal placement")]
        public DesignerImageHorizontalPlacement ImageHorizontalPlacement { get; set; }
        [Category("Image"), DisplayName("Vertical placement")]
        public DesignerImageVerticalPlacement ImageVerticalPlacement { get; set; }
        [Category("Image"), DisplayName("Fine adjustment X")]
        public float ImageOffsetX { get; set; }
        [Category("Image"), DisplayName("Fine adjustment Y")]
        public float ImageOffsetY { get; set; }

        public DesignerElement()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Element";
            Width = 160;
            Height = 34;
            Visible = true;
            MouseLocked = false;
            Opacity = 1F;
            Scale = 1F;
            Text = "Text";
            FontName = "Segoe UI";
            FontSize = 14F;
            WordWrap = true;
            Trimming = DesignerTextTrimming.None;
            ColorArgb = Color.White.ToArgb();
        }

        public override string ToString()
        {
            return (Visible ? "● " : "○ ") +
                (MouseLocked ? "[Locked] " : string.Empty) + Name;
        }
    }

    public sealed class DesignerFontNameConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(
            ITypeDescriptorContext context) { return true; }
        public override bool GetStandardValuesExclusive(
            ITypeDescriptorContext context) { return false; }
        public override StandardValuesCollection GetStandardValues(
            ITypeDescriptorContext context)
        {
            var names = new List<string>();
            using (var fonts = new InstalledFontCollection())
                foreach (FontFamily family in fonts.Families)
                    names.Add(family.Name);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return new StandardValuesCollection(names);
        }
    }

    public sealed class WeatherBindingConverter : StringConverter
    {
        private static readonly string[] WeatherValues =
        {
            "None", "Weather: Location", "Weather: Temperature",
            "Weather: Feels Like", "Weather: Condition", "Weather: Humidity",
            "Weather: Wind", "Weather: Pressure", "Weather: Visibility",
            "Weather: Dew Point", "Weather: UV Index",
            "Weather: Sunrise and Sunset", "Weather: Current Icon",
            "Weather: Forecast Day", "Weather: Forecast Icon",
            "Weather: Forecast High and Low", "Weather: Forecast High",
            "Weather: Forecast Low", "Weather: Forecast Condition",
            "Weather: Updated Time",
            "Layout: Divider", "Layout: Main Background",
            "Layout: Weather Details Background",
            "Control: Weather Details Button"
        };
        private static readonly string[] ClockValues =
        {
            "None", "Clock: Hour Hand", "Clock: Minute Hand",
            "Clock: Second Hand", "Clock: Centre Pivot", "Clock: Date"
        };
        private static readonly string[] CalendarValues =
        {
            "None", "Calendar: Month and Year", "Calendar: Previous Month",
            "Calendar: Next Month", "Calendar: Today",
            "Calendar: Weekday", "Calendar: Date", "Calendar: Full Date"
        };
        private static readonly string[] RecycleBinValues =
        { "None", "Recycle Bin: Empty", "Recycle Bin: Full" };

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) { return true; }
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) { return true; }
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            DesignerElement element = context == null
                ? null : context.Instance as DesignerElement;
            string currentBinding = element == null ? null : element.Binding;
            if (currentBinding == null && context != null &&
                context.Instance != null)
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(
                    context.Instance)["Binding"];
                if (property != null)
                    currentBinding = Convert.ToString(
                        property.GetValue(context.Instance));
            }
            bool clock = (element != null &&
                string.Equals(element.BindingDomain, "Clock",
                    StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrEmpty(currentBinding) &&
                  currentBinding.StartsWith("Clock:",
                    StringComparison.OrdinalIgnoreCase)));
            bool calendar = (element != null &&
                string.Equals(element.BindingDomain, "Calendar",
                    StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrEmpty(currentBinding) &&
                  currentBinding.StartsWith("Calendar:",
                    StringComparison.OrdinalIgnoreCase)));
            if (element != null && element.BindingDomain == "Recycle Bin")
                return new StandardValuesCollection(RecycleBinValues);
            return new StandardValuesCollection(clock
                ? ClockValues : calendar ? CalendarValues : WeatherValues);
        }
    }
}
