using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace XWidgetReborn.WidgetSdk
{
    public static class VintageThemePainter
    {
        public const string PaperTexturePath =
            @"Assets\Themes\Vintage\vintage-paper-texture.png";

        public static readonly Color PaperLight =
            Color.FromArgb(244, 231, 198);
        public static readonly Color PaperDark =
            Color.FromArgb(211, 184, 132);
        public static readonly Color Ink =
            Color.FromArgb(55, 37, 23);
        public static readonly Color MutedInk =
            Color.FromArgb(105, 76, 45);
        public static readonly Color Brass =
            Color.FromArgb(179, 125, 48);
        public static readonly Color DarkBrass =
            Color.FromArgb(104, 70, 31);
        public static readonly Color Weekend =
            Color.FromArgb(142, 65, 57);

        public static void FillPaper(
            Graphics graphics, GraphicsPath path, RectangleF bounds)
        {
            if (graphics == null || path == null) return;
            using (var baseBrush = new LinearGradientBrush(
                bounds, PaperLight, PaperDark, 90F))
                graphics.FillPath(baseBrush, path);

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SetClip(path, CombineMode.Intersect);
                Image texture = ThemeSkinCache.Get(PaperTexturePath);
                using (var attributes = new ImageAttributes())
                {
                    var matrix = new ColorMatrix();
                    matrix.Matrix33 = .34F;
                    attributes.SetColorMatrix(matrix,
                        ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    graphics.DrawImage(texture, Rectangle.Round(bounds),
                        0, 0, texture.Width, texture.Height,
                        GraphicsUnit.Pixel, attributes);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        public static void DrawDoubleBorder(
            Graphics graphics, GraphicsPath outerPath,
            RectangleF innerBounds, float innerRadius)
        {
            using (var outerShadow = new Pen(
                Color.FromArgb(150, 39, 24, 13), 4F))
            using (var outer = new Pen(DarkBrass, 2.2F))
            using (var inner = new Pen(Brass, 1.4F))
            using (var innerDark = new Pen(
                Color.FromArgb(150, DarkBrass), .8F))
            using (GraphicsPath innerPath = RoundedRectangle(
                innerBounds, innerRadius))
            using (GraphicsPath innerInsetPath = RoundedRectangle(
                RectangleF.Inflate(innerBounds, -4F, -4F),
                Math.Max(2F, innerRadius - 4F)))
            {
                graphics.DrawPath(outerShadow, outerPath);
                graphics.DrawPath(outer, outerPath);
                graphics.DrawPath(inner, innerPath);
                graphics.DrawPath(innerDark, innerInsetPath);
            }
        }

        public static GraphicsPath RoundedRectangle(
            RectangleF bounds, float radius)
        {
            float safeRadius = Math.Max(1F, Math.Min(radius,
                Math.Min(bounds.Width, bounds.Height) / 2F));
            float diameter = safeRadius * 2F;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top,
                diameter, diameter, 180F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Top,
                diameter, diameter, 270F, 90F);
            path.AddArc(bounds.Right - diameter,
                bounds.Bottom - diameter,
                diameter, diameter, 0F, 90F);
            path.AddArc(bounds.Left, bounds.Bottom - diameter,
                diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }
    }
}
