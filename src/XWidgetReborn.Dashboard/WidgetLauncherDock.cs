using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal sealed class WidgetLauncherDockForm : Form
    {
        private const int HeaderHeight = 58;
        private const int CardWidth = 158;
        private const int CardHeight = 142;
        private const int CardGap = 10;

        private readonly Action _openGallery;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly TextBox _search;
        private readonly ComboBox _category;
        private readonly Button _gallery;
        private readonly Button _close;
        private readonly Button _previous;
        private readonly Button _next;
        private readonly BufferedPanel _cardViewport;
        private readonly Timer _animation;
        private readonly Timer _deactivate;
        private readonly List<LauncherCard> _allCards =
            new List<LauncherCard>();
        private readonly List<LauncherCard> _visibleCards =
            new List<LauncherCard>();

        private int _targetTop;
        private int _scrollOffset;
        private bool _hiding;
        private int _loadGeneration;

        public WidgetLauncherDockForm(Action openGallery)
        {
            _openGallery = openGallery;
            Text = "EmilyDesk Widget Dock";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(30, 33, 36);
            ClientSize = new Size(1440, 218);

            _title = new Label
            {
                AutoSize = false,
                Text = "EMILYDESK WIDGETS",
                Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(225, 184, 105),
                BackColor = Color.Transparent,
                Location = new Point(24, 10),
                Size = new Size(245, 24)
            };
            Controls.Add(_title);

            _subtitle = new Label
            {
                AutoSize = false,
                Text = "Every installed widget and theme",
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.FromArgb(184, 188, 191),
                BackColor = Color.Transparent,
                Location = new Point(25, 34),
                Size = new Size(245, 18)
            };
            Controls.Add(_subtitle);

            _search = new TextBox
            {
                Location = new Point(286, 16),
                Size = new Size(235, 24),
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_search);

            _category = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(531, 15),
                Size = new Size(158, 25),
                Font = new Font("Segoe UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            _category.Items.AddRange(new object[]
            {
                "All widgets", "Weather", "Clock", "Calendar",
                "Recycle Bin", "Optional"
            });
            _category.SelectedIndex = 0;
            Controls.Add(_category);

            _gallery = HeaderButton("Open Gallery", 703, 14, 118);
            _gallery.Click += delegate
            {
                HideDock();
                if (_openGallery != null) _openGallery();
            };
            Controls.Add(_gallery);

            _close = HeaderButton("×", 0, 12, 38);
            _close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _close.Left = ClientSize.Width - 54;
            _close.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _close.BackColor = Color.FromArgb(54, 59, 64);
            _close.Click += delegate { HideDock(); };
            Controls.Add(_close);

            _previous = NavigationButton("‹");
            _previous.Location = new Point(10, 100);
            _previous.Click += delegate { ScrollCards(-504); };
            Controls.Add(_previous);

            _next = NavigationButton("›");
            _next.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _next.Location = new Point(ClientSize.Width - 39, 100);
            _next.Click += delegate { ScrollCards(504); };
            Controls.Add(_next);

            _cardViewport = new BufferedPanel
            {
                Location = new Point(45, HeaderHeight + 3),
                Size = new Size(ClientSize.Width - 90, CardHeight + 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Left |
                    AnchorStyles.Right,
                BackColor = Color.FromArgb(38, 42, 46)
            };
            _cardViewport.MouseWheel += CardViewportMouseWheel;
            Controls.Add(_cardViewport);

            _search.TextChanged += delegate { ApplyFilter(); };
            _category.SelectedIndexChanged += delegate { ApplyFilter(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) HideDock();
                if (e.KeyCode == Keys.Left) ScrollCards(-CardWidth - CardGap);
                if (e.KeyCode == Keys.Right) ScrollCards(CardWidth + CardGap);
            };
            Resize += delegate
            {
                UpdateWindowRegion();
                LayoutCards();
            };

            _animation = new Timer { Interval = 15 };
            _animation.Tick += Animate;
            _deactivate = new Timer { Interval = 160 };
            _deactivate.Tick += delegate
            {
                _deactivate.Stop();
                if (!ContainsFocus) HideDock();
            };
            Deactivate += delegate
            {
                _deactivate.Stop();
                _deactivate.Start();
            };
            FormClosed += delegate
            {
                _animation.Dispose();
                _deactivate.Dispose();
            };

            UpdateWindowRegion();
        }

        private static Button HeaderButton(
            string text, int left, int top, int width)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 29),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 119, 176),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(83, 157, 207);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(56, 136, 193);
            return button;
        }

        private static Button NavigationButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(29, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(48, 53, 58),
                ForeColor = Color.FromArgb(225, 184, 105),
                Font = new Font("Georgia", 18F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(118, 82, 43);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(61, 68, 74);
            return button;
        }

        public void ToggleDock()
        {
            if (Visible && !_hiding) HideDock();
            else ShowDock();
        }

        public void ShowDock()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle work = screen.WorkingArea;
            Width = Math.Min(1450, Math.Max(860, work.Width - 72));
            Height = 218;
            Left = work.Left + (work.Width - Width) / 2;
            _targetTop = work.Bottom - Height - 8;
            Top = work.Bottom;
            _hiding = false;
            if (!Visible) Show();
            ReloadCards();
            BringToFront();
            Activate();
            _animation.Start();
        }

        public void HideDock()
        {
            if (!Visible) return;
            _hiding = true;
            _animation.Start();
        }

        private void Animate(object sender, EventArgs e)
        {
            int destination = _hiding
                ? Screen.FromControl(this).WorkingArea.Bottom
                : _targetTop;
            int delta = destination - Top;
            if (Math.Abs(delta) <= 3)
            {
                Top = destination;
                _animation.Stop();
                if (_hiding) Hide();
                return;
            }
            Top += Math.Sign(delta) * Math.Max(3, Math.Abs(delta) / 3);
        }

        private void ReloadCards()
        {
            int generation = ++_loadGeneration;
            // Designer saves can replace a preview while EmilyDesk remains
            // open. Re-resolve and decode every dock thumbnail so the dock
            // never keeps an earlier set of clock hands or other artwork.
            WidgetPreviewResolver.Clear();
            VirtualThumbnailCache.Clear();
            IList<WidgetDescriptor> widgets =
                EngineClient.ListAvailableWidgets();
            var running = new HashSet<string>(
                EngineClient.ListOpenWidgets(),
                StringComparer.OrdinalIgnoreCase);
            List<GalleryWidgetItem> items =
                VirtualWidgetManagerForm.BuildGalleryItems(widgets, running);

            _cardViewport.SuspendLayout();
            while (_cardViewport.Controls.Count > 0)
                _cardViewport.Controls[0].Dispose();
            _allCards.Clear();
            _visibleCards.Clear();
            _scrollOffset = 0;

            foreach (GalleryWidgetItem item in items)
            {
                GalleryWidgetItem selectedItem = item;
                var card = new LauncherCard(selectedItem);
                card.ActivateRequested += delegate
                {
                    if (EngineClient.OpenWidget(
                        selectedItem.Descriptor.Id, selectedItem.Theme))
                        HideDock();
                };
                card.WheelRequested += CardViewportMouseWheel;
                _allCards.Add(card);
                _cardViewport.Controls.Add(card);

                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    Image image = VirtualThumbnailCache.GetOrLoad(
                        selectedItem);
                    try
                    {
                        if (!IsDisposed && generation == _loadGeneration)
                            BeginInvoke(new MethodInvoker(delegate
                            {
                                if (!card.IsDisposed)
                                    card.SetPreview(image);
                            }));
                    }
                    catch { }
                });
            }
            _cardViewport.ResumeLayout();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = _search.Text.Trim();
            string category = Convert.ToString(_category.SelectedItem);
            _visibleCards.Clear();
            foreach (LauncherCard card in _allCards)
            {
                bool visible = CategoryMatches(card.Item, category) &&
                    card.Item.Matches(query);
                card.Visible = visible;
                if (visible) _visibleCards.Add(card);
            }
            _scrollOffset = 0;
            LayoutCards();
        }

        private static bool CategoryMatches(
            GalleryWidgetItem item, string category)
        {
            if (category == "All widgets") return true;
            if (category == "Optional") return item.IsImported;
            string id = item.Descriptor.Id;
            if (category == "Recycle Bin")
                return id.IndexOf("recycle",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            return id.IndexOf(category,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CardViewportMouseWheel(
            object sender, MouseEventArgs e)
        {
            ScrollCards(e.Delta > 0
                ? -(CardWidth + CardGap)
                : CardWidth + CardGap);
        }

        private void ScrollCards(int amount)
        {
            _scrollOffset = Math.Max(0, Math.Min(
                MaximumScrollOffset(), _scrollOffset + amount));
            LayoutCards();
        }

        private int MaximumScrollOffset()
        {
            int contentWidth = _visibleCards.Count *
                (CardWidth + CardGap) + CardGap;
            return Math.Max(0, contentWidth - _cardViewport.ClientSize.Width);
        }

        private void LayoutCards()
        {
            if (_cardViewport == null || _cardViewport.IsDisposed) return;
            int maximum = MaximumScrollOffset();
            if (_scrollOffset > maximum) _scrollOffset = maximum;

            _cardViewport.SuspendLayout();
            int left = CardGap - _scrollOffset;
            foreach (LauncherCard card in _visibleCards)
            {
                card.Bounds = new Rectangle(left, 4,
                    CardWidth, CardHeight);
                left += CardWidth + CardGap;
            }
            _cardViewport.ResumeLayout();
            _previous.Enabled = _scrollOffset > 0;
            _next.Enabled = _scrollOffset < maximum;
            _cardViewport.Invalidate();
        }

        private void UpdateWindowRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = RoundedRectangle(
                new Rectangle(0, 0, Width, Height), 13))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = ClientRectangle;
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(48, 52, 56),
                Color.FromArgb(27, 30, 33), 90F))
                e.Graphics.FillRectangle(brush, bounds);

            using (var headerBrush = new SolidBrush(
                Color.FromArgb(43, 47, 51)))
                e.Graphics.FillRectangle(headerBrush,
                    2, 2, Width - 4, HeaderHeight - 1);

            using (var separator = new Pen(
                Color.FromArgb(105, 73, 39)))
                e.Graphics.DrawLine(separator,
                    12, HeaderHeight, Width - 13, HeaderHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath outer = RoundedRectangle(
                new Rectangle(1, 1, Width - 3, Height - 3), 12))
            using (var border = new Pen(
                Color.FromArgb(175, 124, 62), 2F))
                e.Graphics.DrawPath(border, outer);

            using (GraphicsPath inner = RoundedRectangle(
                new Rectangle(4, 4, Width - 9, Height - 9), 9))
            using (var highlight = new Pen(
                Color.FromArgb(95, 225, 184, 105)))
                e.Graphics.DrawPath(highlight, inner);
        }

        private static GraphicsPath RoundedRectangle(
            Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(
                rectangle.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class LauncherCard : Panel
    {
        private readonly PictureBox _preview;
        private bool _hovered;

        public event EventHandler ActivateRequested;
        public event MouseEventHandler WheelRequested;
        public GalleryWidgetItem Item { get; private set; }

        public LauncherCard(GalleryWidgetItem item)
        {
            Item = item;
            Size = new Size(158, 142);
            BackColor = Color.FromArgb(246, 242, 232);
            Cursor = Cursors.Hand;

            _preview = new PictureBox
            {
                Location = new Point(7, 7),
                Size = new Size(144, 86),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(222, 225, 226)
            };
            Controls.Add(_preview);

            var name = new Label
            {
                Text = item.DisplayName,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 45, 39),
                BackColor = Color.Transparent,
                Location = new Point(8, 96),
                Size = new Size(142, 22),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(name);

            var action = new Label
            {
                Text = item.IsRunning ? "●  Show" : "Open",
                ForeColor = item.IsRunning
                    ? Color.FromArgb(47, 139, 85)
                    : Color.FromArgb(45, 119, 176),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                Location = new Point(8, 117),
                Size = new Size(142, 19),
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(action);

            WireEvents(this);
            UpdateRegion();
            Resize += delegate { UpdateRegion(); };
        }

        private void WireEvents(Control control)
        {
            control.Click += delegate
            {
                EventHandler handler = ActivateRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            control.MouseEnter += delegate
            {
                _hovered = true;
                Invalidate();
            };
            control.MouseLeave += delegate
            {
                Point point = PointToClient(Cursor.Position);
                if (!ClientRectangle.Contains(point))
                {
                    _hovered = false;
                    Invalidate();
                }
            };
            control.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                MouseEventHandler handler = WheelRequested;
                if (handler != null) handler(this, e);
            };
            foreach (Control child in control.Controls)
                WireEvents(child);
        }

        public void SetPreview(Image image)
        {
            _preview.Image = image;
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = RoundedRectangle(
                new Rectangle(0, 0, Width, Height), 7))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath borderPath = RoundedRectangle(
                new Rectangle(0, 0, Width - 1, Height - 1), 7))
            using (var border = new Pen(_hovered
                ? Color.FromArgb(57, 137, 193)
                : Color.FromArgb(175, 132, 69),
                _hovered ? 2F : 1F))
                e.Graphics.DrawPath(border, borderPath);
        }

        private static GraphicsPath RoundedRectangle(
            Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(
                rectangle.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
