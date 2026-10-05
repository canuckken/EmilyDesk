using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace XWidgetReborn.WidgetSdk
{
    // The same simple overlay contract used by the Designer's snapshot themes.
    // Full editable calendars and clocks also use its layer painter for additions.
    public sealed class SavedDesignerLayout
    {
        public string Name { get; set; }
        public int EditableLayerVersion { get; set; }
        public int ClockHandEditVersion { get; set; }
        public int BackgroundLayerVersion { get; set; }
        public List<string> DeletedElementIds { get; set; }
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
        public string BackgroundImage { get; set; }
        public bool ReplaceDefaultBackground { get; set; }
        public List<DesignerLayer> Elements { get; set; }

        public static SavedDesignerLayout Current(string theme, string kind)
        {
            return DesignerLayoutFiles.Load<SavedDesignerLayout>(
                DesignerLayoutFiles.FileName(theme, kind));
        }

        public void Draw(Graphics graphics, Func<DesignerLayer, string> text)
        {
            if (Elements == null) return;
            foreach (DesignerLayer layer in Elements)
                DesignerLayerPainter.Draw(graphics, layer, text);
        }
    }

    public class DesignerLayer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Binding { get; set; }
        public string BindingDomain { get; set; }
        public string AnchorId { get; set; }
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
        public string ImagePath { get; set; }
        public int ImageHorizontalPlacement { get; set; }
        public int ImageVerticalPlacement { get; set; }
        public float ImageOffsetX { get; set; }
        public float ImageOffsetY { get; set; }
        public float PreviewRotation { get; set; }
        public float? HandPivotX { get; set; }
        public float? HandPivotY { get; set; }
        public RectangleF Bounds
        {
            get { return new RectangleF(X, Y, Width * Math.Max(.05F, Scale),
                Height * Math.Max(.05F, Scale)); }
        }
        public DesignerLayer()
        {
            Visible = true; Opacity = Scale = 1F; FontSize = 12F;
            FontName = "Segoe UI"; WordWrap = true;
            ColorArgb = Color.White.ToArgb();
        }
    }

    public static class DesignerLayerPainter
    {
        public static void Draw(Graphics graphics, DesignerLayer layer,
            Func<DesignerLayer, string> resolveText)
        {
            Draw(graphics, layer, resolveText, null);
        }

        public static void Draw(Graphics graphics, DesignerLayer layer,
            Func<DesignerLayer, string> resolveText, Color? overrideColor)
        {
            DrawOnSurface(graphics, layer, resolveText, overrideColor, 0);
        }

        public static void DrawOnSurface(Graphics graphics, DesignerLayer layer,
            Func<DesignerLayer, string> resolveText, Color? overrideColor, int surface)
        {
            if (layer == null || !layer.Visible || layer.Surface != surface ||
                layer.Width <= 0 || layer.Height <= 0) return;
            RectangleF bounds = layer.Bounds;
            float opacity = Math.Max(0F, Math.Min(1F, layer.Opacity));
            if (layer.Kind == 1)
            {
                DrawImage(graphics, layer.ImagePath, bounds, opacity);
                return;
            }
            Color color = Color.FromArgb((int)(255 * opacity),
                overrideColor ?? Color.FromArgb(layer.ColorArgb));
            if (layer.Kind == 2)
            {
                using (var pen = new Pen(color,
                    Math.Max(1F, Math.Min(bounds.Width, bounds.Height))))
                {
                    if (bounds.Width >= bounds.Height)
                        graphics.DrawLine(pen, bounds.Left, bounds.Top + bounds.Height / 2,
                            bounds.Right, bounds.Top + bounds.Height / 2);
                    else graphics.DrawLine(pen, bounds.Left + bounds.Width / 2, bounds.Top,
                        bounds.Left + bounds.Width / 2, bounds.Bottom);
                }
                return;
            }
            FontStyle style = (layer.Bold ? FontStyle.Bold : FontStyle.Regular) |
                (layer.Italic ? FontStyle.Italic : FontStyle.Regular);
            using (var font = DesignerFontResolver.Create(
                layer.FontName, layer.FontFile,
                Math.Max(4F, layer.FontSize * Math.Max(.05F, layer.Scale)),
                style))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.Alignment = layer.Alignment == 1 ? StringAlignment.Center :
                    layer.Alignment == 2 ? StringAlignment.Far : StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                if (layer.WordWrap.HasValue && !layer.WordWrap.Value)
                    format.FormatFlags |= StringFormatFlags.NoWrap;
                format.Trimming = (StringTrimming)Math.Max(0, Math.Min(5, layer.Trimming));
                graphics.DrawString((resolveText == null ? layer.Text : resolveText(layer))
                    ?? string.Empty, font, brush, bounds, format);
            }
        }

        public static bool DrawImage(Graphics graphics, string path,
            RectangleF bounds, float opacity)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                Image image = ThemeSkinCache.Get(path);
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix {
                        Matrix33 = Math.Max(0F, Math.Min(1F, opacity)) });
                    graphics.DrawImage(image, Rectangle.Round(bounds), 0, 0,
                        image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool DrawImageAlphaCropped(Graphics graphics,
            string path, RectangleF bounds, float opacity)
        {
            if (string.IsNullOrWhiteSpace(path) || bounds.Width <= 0F ||
                bounds.Height <= 0F) return false;
            try
            {
                Image image = ThemeSkinCache.Get(path);
                Rectangle source = ThemeSkinCache.GetAlphaBounds(path);
                if (source.Width <= 0 || source.Height <= 0)
                    source = new Rectangle(0, 0, image.Width, image.Height);
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix {
                        Matrix33 = Math.Max(0F, Math.Min(1F, opacity)) });
                    graphics.DrawImage(image, Rectangle.Round(bounds),
                        source.X, source.Y, source.Width, source.Height,
                        GraphicsUnit.Pixel, attributes);
                }
                return true;
            }
            catch { return false; }
        }

        public static RectangleF AlphaMappedBounds(string path,
            RectangleF fullImageBounds)
        {
            if (string.IsNullOrWhiteSpace(path)) return fullImageBounds;
            try
            {
                Image image = ThemeSkinCache.Get(path);
                Rectangle alpha = ThemeSkinCache.GetAlphaBounds(path);
                if (image.Width <= 0 || image.Height <= 0 ||
                    alpha.Width <= 0 || alpha.Height <= 0)
                    return fullImageBounds;
                return new RectangleF(
                    fullImageBounds.X + alpha.X * fullImageBounds.Width /
                        image.Width,
                    fullImageBounds.Y + alpha.Y * fullImageBounds.Height /
                        image.Height,
                    alpha.Width * fullImageBounds.Width / image.Width,
                    alpha.Height * fullImageBounds.Height / image.Height);
            }
            catch { return fullImageBounds; }
        }
    }

    public static class DesignerFontResolver
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, PrivateFontCollection> Fonts =
            new Dictionary<string, PrivateFontCollection>(
                StringComparer.OrdinalIgnoreCase);

        public static string FamilyName(string fontFile)
        {
            PrivateFontCollection collection = Load(fontFile);
            return collection == null || collection.Families.Length == 0
                ? string.Empty : collection.Families[0].Name;
        }

        public static Font Create(string fontName, string fontFile,
            float size, FontStyle style)
        {
            try
            {
                PrivateFontCollection collection = Load(fontFile);
                if (collection != null && collection.Families.Length > 0)
                {
                    FontFamily family = collection.Families[0];
                    FontStyle availableStyle = style;
                    if (!family.IsStyleAvailable(availableStyle))
                    {
                        foreach (FontStyle candidate in new[] {
                            FontStyle.Regular, FontStyle.Bold,
                            FontStyle.Italic,
                            FontStyle.Bold | FontStyle.Italic })
                            if (family.IsStyleAvailable(candidate))
                            {
                                availableStyle = candidate;
                                break;
                            }
                    }
                    return new Font(family, size, availableStyle);
                }
            }
            catch
            {
                // A missing or unsupported widget font falls back cleanly.
            }
            return new Font(string.IsNullOrWhiteSpace(fontName)
                ? "Segoe UI" : fontName, size, style);
        }

        private static PrivateFontCollection Load(string fontFile)
        {
            if (string.IsNullOrWhiteSpace(fontFile)) return null;
            string path;
            try
            {
                path = Path.IsPathRooted(fontFile) ? fontFile : Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    fontFile.Replace('/', Path.DirectorySeparatorChar));
                path = Path.GetFullPath(path);
            }
            catch { return null; }
            if (!File.Exists(path)) return null;
            lock (Sync)
            {
                PrivateFontCollection collection;
                if (Fonts.TryGetValue(path, out collection)) return collection;
                collection = new PrivateFontCollection();
                collection.AddFontFile(path);
                Fonts[path] = collection;
                return collection;
            }
        }
    }
}
