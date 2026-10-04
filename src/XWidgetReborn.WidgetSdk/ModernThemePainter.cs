using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace XWidgetReborn.WidgetSdk
{
    public static class ModernThemePainter
    {
        public static readonly Color SurfaceTop =
            Color.FromArgb(35, 51, 69);
        public static readonly Color SurfaceBottom =
            Color.FromArgb(10, 18, 30);
        public static readonly Color Panel =
            Color.FromArgb(66, 45, 66, 88);
        public static readonly Color PrimaryText =
            Color.FromArgb(242, 247, 252);
        public static readonly Color SecondaryText =
            Color.FromArgb(159, 184, 207);
        public static readonly Color Accent =
            Color.FromArgb(45, 163, 255);
        public static readonly Color AccentBright =
            Color.FromArgb(100, 202, 255);
        public static readonly Color Border =
            Color.FromArgb(78, 112, 147);
        public static readonly Color DeepBorder =
            Color.FromArgb(8, 14, 24);

        public static void FillGlassCard(
            Graphics graphics, GraphicsPath path, RectangleF bounds)
        {
            using (var shadow = new Pen(
                Color.FromArgb(150, 2, 7, 13), 6F))
            using (var background = new LinearGradientBrush(
                bounds, SurfaceTop, SurfaceBottom, 90F))
            using (var border = new Pen(Border, 1.4F))
            using (var highlight = new Pen(
                Color.FromArgb(95, 169, 213, 243), .8F))
            using (GraphicsPath inset = RoundedRectangle(
                RectangleF.Inflate(bounds, -4F, -4F),
                Math.Max(4F, Math.Min(14F,
                    Math.Min(bounds.Width, bounds.Height) / 12F))))
            {
                graphics.DrawPath(shadow, path);
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
                graphics.DrawPath(highlight, inset);
            }
        }

        public static void FillGlassCircle(
            Graphics graphics, GraphicsPath path, RectangleF bounds)
        {
            using (var glow = new Pen(
                Color.FromArgb(90, Accent), 6F))
            using (var outer = new Pen(DeepBorder, 4F))
            using (var ring = new Pen(Border, 1.6F))
            using (var inner = new Pen(
                Color.FromArgb(100, AccentBright), .9F))
            using (var fill = new PathGradientBrush(path))
            {
                fill.CenterColor = Color.FromArgb(38, 56, 76);
                fill.SurroundColors = new[] { SurfaceBottom };
                graphics.DrawPath(glow, path);
                graphics.FillPath(fill, path);
                graphics.DrawPath(outer, path);
                graphics.DrawEllipse(ring,
                    RectangleF.Inflate(bounds, -5F, -5F));
                graphics.DrawEllipse(inner,
                    RectangleF.Inflate(bounds, -11F, -11F));
            }
        }

        public static void FillInsetPanel(
            Graphics graphics, RectangleF bounds, float radius)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, radius))
            using (var fill = new SolidBrush(Panel))
            using (var border = new Pen(
                Color.FromArgb(75, Border), .8F))
            using (var top = new Pen(
                Color.FromArgb(45, AccentBright), .8F))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
                graphics.DrawLine(top,
                    bounds.Left + radius, bounds.Top + 1F,
                    bounds.Right - radius, bounds.Top + 1F);
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
