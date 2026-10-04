using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal static class OptionalWidgetPreviewPublisher
    {
        // Render the same default editable layers that Designer shows. The
        // resulting package icon and preview contain the full widget, rather
        // than a copy of its bare background skin.
        public static void Publish(string assemblyPath, string typeName,
            string packageFolder)
        {
            Assembly assembly = Assembly.LoadFrom(
                Path.GetFullPath(assemblyPath));
            Type type = assembly.GetType(typeName, true);
            IWidgetDesignerProvider widget =
                (IWidgetDesignerProvider)Activator.CreateInstance(type);
            try
            {
                SavedDesignerLayout layout = widget.CreateDesignerLayout();
                if (layout == null || layout.Elements == null ||
                    layout.CanvasWidth < 1 || layout.CanvasHeight < 1)
                    throw new InvalidDataException(
                        "The optional widget has no preview layout.");
                using (var snapshot = new Bitmap(layout.CanvasWidth,
                    layout.CanvasHeight, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(snapshot))
                {
                    Setup(graphics);
                    graphics.Clear(Color.Transparent);
                    foreach (DesignerLayer layer in layout.Elements)
                    {
                        if (layer == null || !layer.Visible ||
                            layer.Surface != 0) continue;
                        RectangleF bounds = layer.Bounds;
                        string path = layer.ImagePath;
                        if (!string.IsNullOrWhiteSpace(path) &&
                            !Path.IsPathRooted(path))
                            path = Path.Combine(packageFolder, path);
                        if (layer.Id == "main-background")
                        {
                            var background = widget as
                                IWidgetDesignerBackgroundLayerProvider;
                            if (background != null)
                                background.RenderDesignerBackgroundLayer(
                                    graphics, Rectangle.Round(bounds),
                                    path, layer.Opacity);
                            else widget.RenderDesignerBackground(graphics,
                                Rectangle.Round(bounds));
                        }
                        else if (layer.Kind == 1)
                            DesignerLayerPainter.DrawImage(graphics,
                                path, bounds, layer.Opacity);
                        else
                        {
                            var textBackground = widget as
                                IWidgetDesignerTextBackgroundProvider;
                            if (textBackground != null)
                                textBackground.RenderDesignerTextBackground(
                                    graphics, layer);
                            DesignerLayerPainter.Draw(graphics, layer,
                                widget.ResolveDesignerText);
                        }
                    }
                    Save(snapshot, 640, 400, false,
                        Path.Combine(packageFolder, "preview.png"));
                    Save(snapshot, 256, 256, true,
                        Path.Combine(packageFolder, "icon.png"));
                }
            }
            finally
            {
                IDisposable disposable = widget as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
        }

        private static void Save(Image snapshot, int width, int height,
            bool transparent, string path)
        {
            using (var output = new Bitmap(width, height,
                PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(output))
            {
                Setup(graphics);
                graphics.Clear(transparent ? Color.Transparent :
                    Color.FromArgb(232, 236, 241));
                Rectangle available = transparent
                    ? new Rectangle(8, 8, 240, 240)
                    : new Rectangle(18, 12, 604, 376);
                float scale = Math.Min(
                    available.Width / (float)snapshot.Width,
                    available.Height / (float)snapshot.Height);
                int drawnWidth = (int)Math.Round(snapshot.Width * scale);
                int drawnHeight = (int)Math.Round(snapshot.Height * scale);
                graphics.DrawImage(snapshot, new Rectangle(
                    available.X + (available.Width - drawnWidth) / 2,
                    available.Y + (available.Height - drawnHeight) / 2,
                    drawnWidth, drawnHeight));
                output.Save(path, ImageFormat.Png);
            }
        }

        private static void Setup(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }
    }
}
