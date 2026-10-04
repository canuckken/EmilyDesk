using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal sealed class VirtualGalleryDiagnosticForm : Form
    {
        private readonly VirtualGallerySurface _surface;

        public VirtualGalleryDiagnosticForm(
            IList<WidgetDescriptor> widgets)
        {
            Text = "TEST D3 — Single Virtual Gallery Surface — " +
                "Begin every drag from the actual Windows title bar.";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(760, 560);
            ClientSize = new Size(1080, 720);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(243, 246, 249);
            AutoScaleMode = AutoScaleMode.Dpi;

            Stopwatch stateRead = Stopwatch.StartNew();
            var running = new HashSet<string>(
                EngineClient.ListOpenWidgets(),
                StringComparer.OrdinalIgnoreCase);
            DragDiagnosticTrace.RecordDuration(
                "TEST D3 — Single Virtual Gallery Surface",
                "ListOpenWidgets",
                stateRead.ElapsedMilliseconds,
                "testName=TEST D3 — Single Virtual Gallery Surface");

            _surface = new VirtualGallerySurface(
                widgets,
                running);
            _surface.Dock = DockStyle.Fill;
            Controls.Add(_surface);
        }

        public string DiagnosticSubsystemState
        {
            get
            {
                return _surface.DiagnosticSubsystemState;
            }
        }
    }

    internal sealed class VirtualGallerySurface : Control
    {
        private const int CardWidth = 236;
        private const int CardHeight = 272;
        private const int Gap = 20;
        private const int PaddingSize = 14;
        private readonly List<GalleryWidgetItem> _items =
            new List<GalleryWidgetItem>();
        private readonly Font _nameFont;
        private int _scrollOffset;
        private int _visibleCardCount;
        private bool _paintActive;

        public VirtualGallerySurface(
            IList<WidgetDescriptor> widgets,
            HashSet<string> running)
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.FromArgb(243, 246, 249);
            TabStop = true;
            _nameFont = new Font(
                "Segoe UI",
                10F,
                FontStyle.Bold);
            foreach (WidgetDescriptor descriptor in widgets)
            {
                _items.Add(new GalleryWidgetItem(descriptor)
                {
                    IsRunning = running.Contains(descriptor.Id)
                });
            }
        }

        public string DiagnosticSubsystemState
        {
            get
            {
                return
                    "testName=TEST D3 — Single Virtual Gallery Surface" +
                    ";cardCount=" + _items.Count +
                    ";visibleCardCount=" + _visibleCardCount +
                    ";previewMode=disabled" +
                    ";previewWorkerActive=False" +
                    ";previewResultsQueued=0" +
                    ";previewResultsApplied=0" +
                    ";previewApplyCallbackActive=False" +
                    ";runningStateMode=initial-snapshot-only" +
                    ";runningStateWorkerActive=False" +
                    ";runningStateCallbackActive=False" +
                    ";layoutActive=False" +
                    ";paintActive=" + _paintActive +
                    ";nativeWidgetCardControls=0" +
                    ";virtualSurfaceControls=1";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Stopwatch timer = Stopwatch.StartNew();
            _paintActive = true;
            try
            {
                e.Graphics.Clear(BackColor);
                int columns = ColumnCount();
                int firstRow = Math.Max(
                    0,
                    _scrollOffset / (CardHeight + Gap));
                int lastRow = Math.Min(
                    RowCount(columns) - 1,
                    (_scrollOffset + ClientSize.Height) /
                        (CardHeight + Gap) + 1);
                int visible = 0;
                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = 0;
                        column < columns;
                        column++)
                    {
                        int index = row * columns + column;
                        if (index >= _items.Count) break;
                        Rectangle bounds = CardBounds(
                            row,
                            column);
                        if (!e.ClipRectangle.IntersectsWith(bounds))
                            continue;
                        DrawCard(
                            e.Graphics,
                            bounds,
                            _items[index],
                            _nameFont);
                        visible++;
                    }
                }
                _visibleCardCount = visible;
            }
            finally
            {
                _paintActive = false;
                long elapsed = timer.ElapsedMilliseconds;
                if (elapsed > 25)
                {
                    DragDiagnosticTrace.RecordDuration(
                        "TEST D3 — Single Virtual Gallery Surface",
                        "virtual gallery paint",
                        elapsed,
                        DiagnosticSubsystemState);
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = Math.Max(
                48,
                SystemInformation.MouseWheelScrollLines * 24);
            _scrollOffset -= Math.Sign(e.Delta) * step;
            ClampScroll();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int index = HitTest(e.Location);
            if (index < 0 || index >= _items.Count) return;
            EngineClient.OpenWidget(
                _items[index].Descriptor.Id);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampScroll();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _nameFont.Dispose();
            base.Dispose(disposing);
        }

        private int HitTest(Point point)
        {
            int columns = ColumnCount();
            int contentX = point.X - PaddingSize;
            int contentY =
                point.Y + _scrollOffset - PaddingSize;
            if (contentX < 0 || contentY < 0) return -1;
            int column = contentX / (CardWidth + Gap);
            int row = contentY / (CardHeight + Gap);
            if (column >= columns ||
                contentX % (CardWidth + Gap) >= CardWidth ||
                contentY % (CardHeight + Gap) >= CardHeight)
                return -1;
            return row * columns + column;
        }

        private Rectangle CardBounds(int row, int column)
        {
            return new Rectangle(
                PaddingSize + column * (CardWidth + Gap),
                PaddingSize + row * (CardHeight + Gap) -
                    _scrollOffset,
                CardWidth,
                CardHeight);
        }

        private int ColumnCount()
        {
            return Math.Max(
                1,
                Math.Max(1, ClientSize.Width - PaddingSize * 2) /
                    (CardWidth + Gap));
        }

        private int RowCount(int columns)
        {
            return (_items.Count + columns - 1) / columns;
        }

        private void ClampScroll()
        {
            int contentHeight =
                PaddingSize * 2 +
                RowCount(ColumnCount()) * (CardHeight + Gap);
            _scrollOffset = Math.Max(
                0,
                Math.Min(
                    _scrollOffset,
                    Math.Max(0, contentHeight - ClientSize.Height)));
        }

        private static void DrawCard(
            Graphics graphics,
            Rectangle bounds,
            GalleryWidgetItem item,
            Font nameFont)
        {
            using (var background = new SolidBrush(Color.White))
                graphics.FillRectangle(background, bounds);
            var preview = new Rectangle(
                bounds.Left + 10,
                bounds.Top + 10,
                216,
                172);
            using (var placeholder = new SolidBrush(
                Color.FromArgb(238, 241, 245)))
                graphics.FillRectangle(placeholder, preview);
            using (var border = new Pen(
                Color.FromArgb(211, 217, 225)))
                graphics.DrawRectangle(
                    border,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width - 1,
                    bounds.Height - 1);

            var name = new Rectangle(
                bounds.Left + 10,
                bounds.Top + 190,
                216,
                42);
            TextRenderer.DrawText(
                graphics,
                item.Descriptor.Name,
                nameFont,
                name,
                Color.Black,
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.WordBreak);
            TextRenderer.DrawText(
                graphics,
                item.IsRunning ? "Running" : "Not running",
                SystemFonts.MessageBoxFont,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 238,
                    216,
                    24),
                item.IsRunning
                    ? Color.FromArgb(25, 116, 69)
                    : Color.FromArgb(88, 98, 112),
                TextFormatFlags.VerticalCenter);
        }
    }
}
