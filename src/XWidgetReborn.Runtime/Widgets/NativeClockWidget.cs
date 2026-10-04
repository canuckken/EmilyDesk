using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Runtime.Widgets
{
    internal sealed class NativeClockWidget : IWidget
    {
        private Action _invalidate;
        private DateTime _displayTime = DateTime.Now;
        private bool _paused;

        public string Id { get { return "native.clock"; } }
        public string Name { get { return "EmilyDesk Clock"; } }
        public Size DefaultSize { get { return new Size(330, 190); } }
        public Point DefaultLocation { get { return new Point(80, 80); } }
        public string Description { get { return "A native clock hosted and managed entirely by EmilyDesk Engine."; } }
        public string Version { get { return "1.0.0"; } }
        public WidgetUpdateRate UpdateRate { get { return WidgetUpdateRate.Second; } }

        public void Start(Action invalidate)
        {
            _invalidate = invalidate;
            _displayTime = DateTime.Now;
        }

        public void Tick(DateTime now)
        {
            if (_paused) return;
            _displayTime = now;
            if (_invalidate != null) _invalidate();
        }

        public void Pause() { _paused = true; if (_invalidate != null) _invalidate(); }
        public void Resume() { _paused = false; _displayTime = DateTime.Now; if (_invalidate != null) _invalidate(); }

        public void Render(Graphics g, Rectangle bounds)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var path = RoundedRectangle(bounds, 14))
            using (var background = new LinearGradientBrush(bounds, Color.FromArgb(31, 42, 56), Color.FromArgb(18, 24, 33), 90F))
            using (var border = new Pen(Color.FromArgb(70, 124, 176, 230)))
            {
                g.FillPath(background, path);
                g.DrawPath(border, path);
            }
            using (var accent = new SolidBrush(Color.FromArgb(54, 149, 226))) g.FillRectangle(accent, 0, 0, 6, bounds.Height);
            string time = _displayTime.ToString("h:mm:ss tt");
            string date = _displayTime.ToString("dddd, MMMM d");
            using (var titleFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold))
            using (var timeFont = new Font("Segoe UI", 30F, FontStyle.Regular))
            using (var dateFont = new Font("Segoe UI", 11F, FontStyle.Regular))
            using (var titleBrush = new SolidBrush(Color.FromArgb(140, 198, 240)))
            using (var mainBrush = new SolidBrush(Color.White))
            using (var secondaryBrush = new SolidBrush(Color.FromArgb(190, 205, 220)))
            {
            g.DrawString("WIDGETWORKS ENGINE", titleFont, titleBrush, 24, 18);
                g.DrawString(time, timeFont, mainBrush, 20, 48);
                g.DrawString(date, dateFont, secondaryBrush, 24, 112);
                g.DrawString(_paused ? "Paused" : "Native widget · Engine online", dateFont, secondaryBrush, 24, 145);
            }
        }

        public void Dispose() { _invalidate = null; }

        private static GraphicsPath RoundedRectangle(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
