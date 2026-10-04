using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace XWidgetReborn
{
    internal sealed class WidgetGalleryCard : Control
    {
        private static readonly Rectangle PreviewBounds =
            new Rectangle(10, 10, 216, 172);
        private static readonly Rectangle NameBounds =
            new Rectangle(10, 190, 216, 38);
        private static readonly Rectangle TypeBounds =
            new Rectangle(10, 236, 86, 22);
        private static readonly Rectangle RunningBounds =
            new Rectangle(98, 234, 68, 22);
        private static readonly Rectangle LaunchBounds =
            new Rectangle(174, 237, 52, 20);

        private readonly Font _nameFont;
        private Image _preview;
        private bool _selected;
        private bool _hovered;
        private readonly Action<long> _paintCompleted;

        public event EventHandler Selected;
        public event EventHandler ActivateRequested;
        public event EventHandler ContextRequested;

        public WidgetGalleryCard(
            GalleryWidgetItem item,
            Action<long> paintCompleted)
        {
            Item = item;
            _paintCompleted = paintCompleted;
            Size = new Size(236, 272);
            Margin = new Padding(10);
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            _nameFont = new Font(
                "Segoe UI",
                10F,
                FontStyle.Bold);
            UpdateAccessibility();
        }

        public GalleryWidgetItem Item { get; private set; }

        public bool IsSelected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        public void SetRunning(bool running)
        {
            if (Item.IsRunning == running) return;
            Item.IsRunning = running;
            UpdateAccessibility();
            Invalidate(new Rectangle(
                RunningBounds.Left,
                RunningBounds.Top,
                Width - RunningBounds.Left,
                RunningBounds.Height + 2));
        }

        public void SetPreview(Image image)
        {
            if (IsDisposed)
            {
                if (image != null) image.Dispose();
                return;
            }
            Image previous = _preview;
            _preview = image;
            Invalidate(PreviewBounds);
            if (previous != null) previous.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Stopwatch timer = Stopwatch.StartNew();
            if (_paintCompleted != null)
                _paintCompleted(-1);
            try
            {
            e.Graphics.Clear(BackColor);
            using (var previewBrush = new SolidBrush(
                Color.FromArgb(238, 241, 245)))
                e.Graphics.FillRectangle(
                    previewBrush,
                    PreviewBounds);
            if (_preview != null)
                e.Graphics.DrawImage(
                    _preview,
                    PreviewBounds);

            TextRenderer.DrawText(
                e.Graphics,
                Item.Descriptor.Name,
                _nameFont,
                NameBounds,
                ForeColor,
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                e.Graphics,
                Item.TypeName,
                Font,
                TypeBounds,
                Color.FromArgb(88, 98, 112),
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            if (Item.IsRunning)
            {
                using (var badgeBrush = new SolidBrush(
                    Color.FromArgb(223, 245, 232)))
                    e.Graphics.FillRectangle(
                        badgeBrush,
                        RunningBounds);
                TextRenderer.DrawText(
                    e.Graphics,
                    "Running",
                    Font,
                    RunningBounds,
                    Color.FromArgb(25, 116, 69),
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Item.IsRunning ? "Show" : "Launch",
                Font,
                LaunchBounds,
                Color.FromArgb(22, 92, 167),
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            Color border = _selected || Focused
                ? Color.FromArgb(24, 112, 205)
                : _hovered
                    ? Color.FromArgb(151, 166, 184)
                    : Color.FromArgb(211, 217, 225);
            int thickness = _selected || Focused ? 2 : 1;
            using (var pen = new Pen(border, thickness))
                e.Graphics.DrawRectangle(
                    pen,
                    thickness / 2,
                    thickness / 2,
                    Width - thickness,
                    Height - thickness);
            }
            finally
            {
                if (_paintCompleted != null)
                    _paintCompleted(timer.ElapsedMilliseconds);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (_hovered) return;
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_hovered) return;
            _hovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            SelectCard();
            Focus();
            if (e.Button != MouseButtons.Right) return;
            EventHandler handler = ContextRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left &&
                LaunchBounds.Contains(e.Location))
                RequestActivation();
        }

        protected override void OnMouseDoubleClick(
            MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
                RequestActivation();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Enter &&
                e.KeyCode != Keys.Space)
                return;
            e.Handled = true;
            RequestActivation();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nameFont.Dispose();
                if (_preview != null)
                {
                    _preview.Dispose();
                    _preview = null;
                }
            }
            base.Dispose(disposing);
        }

        private void UpdateAccessibility()
        {
            AccessibleName = Item.Descriptor.Name;
            AccessibleDescription =
                Item.TypeName + " widget. " +
                (Item.IsRunning
                    ? "Running. Activate to bring it to front."
                    : "Not running. Activate to launch it.");
        }

        private void SelectCard()
        {
            EventHandler handler = Selected;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void RequestActivation()
        {
            SelectCard();
            EventHandler handler = ActivateRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
