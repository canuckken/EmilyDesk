using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal sealed class VirtualWidgetManagerForm : Form
    {
        private readonly TextBox _search;
        private readonly ComboBox _filter;
        private readonly Button _clearSearch;
        private readonly Button _importWidget;
        private readonly Label _summary;
        private readonly Label _status;
        private readonly VirtualWidgetGallerySurface _surface;
        private readonly Timer _stateTimer;
        private BackgroundWorker _previewWorker;
        private BackgroundWorker _stateWorker;
        private int _previewGeneration;
        private bool _previewRefreshReady;
        private static readonly string LegacyGallerySearchStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XWidgetReborn",
            "gallery-search.txt");

        public VirtualWidgetManagerForm(
            IList<WidgetDescriptor> widgets)
        {
            Text = "EmilyDesk Gallery — Build " + AppConstants.BuildId;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(760, 560);
            ClientSize = new Size(1080, 720);
            Font = new Font("Segoe UI", 9F);
            BackColor = EmilyDeskDesignTokens.Canvas;
            AutoScaleMode = AutoScaleMode.Dpi;
            Activated += delegate
            {
                WidgetLayerMenuFactory.RaiseAfterActivation(this);
                if (_previewRefreshReady)
                    RefreshPreviewsFromDisk();
            };
            Shown += delegate
            {
                WidgetLayerMenuFactory.RaiseAfterActivation(this);
                _previewRefreshReady = true;
            };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                Padding = new Padding(22, 15, 22, 12),
                BackColor = EmilyDeskDesignTokens.Surface
            };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                Location = new Point(22, 14),
                Size = new Size(320, 32),
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                Text = "Widget Gallery"
            });
            header.Controls.Add(new Label
            {
                Location = new Point(24, 48),
                Size = new Size(450, 24),
                ForeColor = Color.FromArgb(93, 103, 118),
                Text = "Choose a widget for your desktop"
            });

            _search = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(520, 23),
                Size = new Size(230, 29),
                AccessibleName = "Search widgets"
            };
            header.Controls.Add(_search);
            header.Controls.Add(new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(520, 55),
                Size = new Size(345, 20),
                ForeColor = Color.FromArgb(110, 120, 134),
                Text = "Search widgets"
            });
            _clearSearch = new Button
            {
                Anchor = AnchorStyles.Top |
                    AnchorStyles.Right,
                Location = new Point(758, 23),
                Size = new Size(107, 29),
                Text = "Clear Search",
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = EmilyDeskDesignTokens.Surface,
                ForeColor = EmilyDeskDesignTokens.Accent,
                UseVisualStyleBackColor = false,
                AccessibleName = "Clear Search"
            };
            _clearSearch.FlatAppearance.BorderColor =
                EmilyDeskDesignTokens.Accent;
            _clearSearch.FlatAppearance.BorderSize = 1;
            _clearSearch.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(232, 242, 252);
            _clearSearch.FlatAppearance.MouseDownBackColor =
                Color.FromArgb(210, 229, 248);
            _clearSearch.Click += delegate
            {
                ClearSearch();
            };
            header.Controls.Add(_clearSearch);
            _filter = new ComboBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(880, 23),
                Size = new Size(155, 29),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _filter.Items.AddRange(new object[]
            {
                "All widgets", "Running", "Imported"
            });
            _filter.SelectedIndex = 0;
            header.Controls.Add(_filter);
            _clearSearch.Top = _search.Top;
            _clearSearch.Height = _search.Height;
            _filter.Top = _search.Top;
            _filter.Height = _search.Height;

            var running = new HashSet<string>(
                EngineClient.ListOpenWidgets(),
                StringComparer.OrdinalIgnoreCase);
            List<GalleryWidgetItem> items = BuildGalleryItems(
                widgets,
                running);
            _surface = new VirtualWidgetGallerySurface(items);
            _surface.Dock = DockStyle.Fill;
            _surface.ActivateRequested += ActivateWidget;
            _surface.SelectionChanged += SelectionChanged;
            _surface.ContextMenuRequested += ShowWidgetMenu;
            Controls.Add(_surface);
            _surface.BringToFront();

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                Padding = new Padding(18, 12, 18, 10),
                BackColor = EmilyDeskDesignTokens.Surface
            };
            Controls.Add(footer);
            _importWidget = new Button
            {
                Text = "Import Widget...",
                Location = new Point(18, 13),
                Size = new Size(165, 36),
                BackColor = EmilyDeskDesignTokens.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            _importWidget.FlatAppearance.BorderSize = 0;
            _importWidget.Click += delegate { ImportWidget(); };
            footer.Controls.Add(_importWidget);
            _summary = new Label
            {
                Location = new Point(198, 20),
                Size = new Size(260, 24),
                ForeColor = Color.FromArgb(92, 102, 116)
            };
            footer.Controls.Add(_summary);
            _status = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(465, 20),
                Size = new Size(565, 24),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(63, 75, 91)
            };
            footer.Controls.Add(_status);
            _search.TextChanged += delegate
            {
                ApplyFilter();
            };
            _search.KeyDown += delegate(
                object sender,
                KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Escape ||
                    _search.TextLength == 0)
                    return;
                ClearSearch();
                e.Handled = true;
                e.SuppressKeyPress = true;
            };
            _filter.SelectedIndexChanged +=
                delegate { ApplyFilter(); };
            RemoveLegacySearchState();
            ApplyFilter();
            LoadPreviewsAsync(items);

            _stateTimer = new Timer { Interval = 750 };
            _stateTimer.Tick += delegate
            {
                QueueRunningStateRefresh();
            };
            _stateTimer.Start();
        }


        private static bool IsNativeGalleryWidget(
            WidgetDescriptor descriptor)
        {
            return descriptor != null &&
                descriptor.Id.StartsWith(
                    "native.",
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static List<GalleryWidgetItem> BuildGalleryItems(
            IList<WidgetDescriptor> widgets,
            HashSet<string> running)
        {
            var items = new List<GalleryWidgetItem>();
            foreach (WidgetDescriptor descriptor in widgets)
            {
                if (descriptor == null || descriptor.Id.StartsWith(
                    "legacy.",
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                if (IsNativeGalleryWidget(descriptor) &&
                    descriptor.IsBundled)
                {
                    foreach (string theme in EmilyDeskThemeCatalog.Names)
                        items.Add(new GalleryWidgetItem(descriptor, theme)
                        {
                            IsRunning = running.Contains(descriptor.Id)
                        });
                }
                else
                {
                    items.Add(new GalleryWidgetItem(descriptor)
                    {
                        IsRunning = running.Contains(descriptor.Id)
                    });
                }
            }
            return items;
        }

        private static void RemoveLegacySearchState()
        {
            try
            {
                if (File.Exists(LegacyGallerySearchStatePath))
                    File.Delete(LegacyGallerySearchStatePath);
            }
            catch
            {
                // An obsolete convenience file must never prevent Gallery use.
            }
        }

        private void ClearSearch()
        {
            _search.Text = string.Empty;
            _filter.SelectedIndex = 0;
            ApplyFilter();
            _surface.ClearTransientState();
            _status.Text = string.Empty;
            _search.Focus();
        }

        private void ApplyFilter()
        {
            string filter = _filter.SelectedItem == null
                ? "All widgets"
                : _filter.SelectedItem.ToString();
            _surface.ApplyFilter(_search.Text, filter);
            _clearSearch.Enabled =
                _search.TextLength > 0;
            _summary.Text = _surface.VisibleItemCount +
                " of " + _surface.TotalItemCount + " widgets";
            if (_surface.VisibleItemCount == 0)
                _status.Text = "No widgets match the current search and filter.";
        }

        private void ImportWidget()
        {
            WidgetPackageInstallResult result = WidgetPackageUi.Import(this);
            if (result == null) return;
            RefreshGallery(result.WidgetId, true);
        }

        private void ShowWidgetMenu(
            object sender,
            GalleryWidgetItemEventArgs e)
        {
            GalleryWidgetItem item = e == null ? null : e.Item;
            if (item == null) return;
            var menu = new ContextMenuStrip();
            menu.Items.Add(
                item.IsRunning ? "Show Widget" : "Launch Widget",
                null,
                delegate
                {
                    ActivateWidget(this, e);
                    menu.Dispose();
                });
            if (item.IsImported &&
                WidgetPackageInstaller.CanUninstall(
                    item.Descriptor.Id))
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(
                    "Remove Imported Widget...",
                    null,
                    delegate
                    {
                        string id = item.Descriptor.Id;
                        if (WidgetPackageUi.Uninstall(
                            this,
                            id,
                            item.Descriptor.Name))
                            RefreshGallery(id, false);
                        menu.Dispose();
                    });
            }
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(Cursor.Position);
        }

        private void RefreshGallery(string widgetId, bool shouldExist)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            var timer = new Timer { Interval = 250 };
            timer.Tick += delegate
            {
                IList<WidgetDescriptor> widgets =
                    EngineClient.ListAvailableWidgets();
                bool exists = false;
                foreach (WidgetDescriptor descriptor in widgets)
                    if (string.Equals(
                        descriptor.Id,
                        widgetId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                if (exists != shouldExist &&
                    DateTime.UtcNow < deadline)
                    return;

                timer.Stop();
                timer.Dispose();
                var running = new HashSet<string>(
                    EngineClient.ListOpenWidgets(),
                    StringComparer.OrdinalIgnoreCase);
                List<GalleryWidgetItem> items = BuildGalleryItems(
                    widgets,
                    running);
                VirtualThumbnailCache.Clear();
                _surface.ReplaceItems(items);
                ApplyFilter();
                LoadPreviewsAsync(items);
                _status.Text = shouldExist
                    ? "The imported widget is ready."
                    : "The imported widget was removed.";
            };
            timer.Start();
        }

        private void SelectionChanged(
            object sender,
            GalleryWidgetItemEventArgs e)
        {
            if (e.Item == null) return;
            _status.Text = e.Item.DisplayName +
                " - press Enter or use the card action to " +
                (e.Item.IsRunning
                    ? (string.IsNullOrWhiteSpace(e.Item.Theme)
                        ? "show it"
                        : "apply this theme")
                    : "launch it");
        }

        private void ActivateWidget(
            object sender,
            GalleryWidgetItemEventArgs e)
        {
            if (e.Item == null) return;
            bool sent = EngineClient.OpenWidget(
                e.Item.Descriptor.Id,
                e.Item.Theme);
            _status.Text = sent
                ? "Opening " + e.Item.DisplayName + "..."
                : "The Engine did not accept the launch command.";
            if (sent)
                QueueRunningStateRefresh();
        }

        private void LoadPreviewsAsync(
            IList<GalleryWidgetItem> items)
        {
            int generation = ++_previewGeneration;
            if (_previewWorker != null &&
                _previewWorker.IsBusy)
                _previewWorker.CancelAsync();
            var worker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true
            };
            _previewWorker = worker;
            worker.DoWork += delegate(
                object sender,
                DoWorkEventArgs e)
            {
                var batch = new List<VirtualPreviewResult>();
                foreach (GalleryWidgetItem item in items)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }
                    Image thumbnail =
                        VirtualThumbnailCache.GetOrLoad(item);
                    batch.Add(new VirtualPreviewResult(
                            generation,
                            item.GalleryKey,
                            thumbnail));
                    worker.ReportProgress(0, batch);
                    batch = new List<VirtualPreviewResult>();
                }
                if (batch.Count > 0)
                    worker.ReportProgress(0, batch);
            };
            worker.ProgressChanged += delegate(
                object sender,
                ProgressChangedEventArgs e)
            {
                var results =
                    e.UserState as List<VirtualPreviewResult>;
                if (results == null ||
                    IsDisposed || Disposing)
                    return;
                foreach (VirtualPreviewResult result in results)
                {
                    if (result.Generation != _previewGeneration)
                        continue;
                    _surface.SetPreview(
                        result.WidgetId,
                        result.Image);
                }
            };
            worker.RunWorkerCompleted += delegate
            {
                worker.Dispose();
                if (ReferenceEquals(_previewWorker, worker))
                    _previewWorker = null;
            };
            worker.RunWorkerAsync();
        }

        private void RefreshPreviewsFromDisk()
        {
            if (IsDisposed || Disposing || _surface == null)
                return;
            WidgetPreviewResolver.Clear();
            VirtualThumbnailCache.Clear();
            _surface.ClearPreviews();
            LoadPreviewsAsync(_surface.ItemsSnapshot());
        }

        private void QueueRunningStateRefresh()
        {
            if (IsDisposed || Disposing ||
                (_stateWorker != null && _stateWorker.IsBusy))
                return;
            var worker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            _stateWorker = worker;
            worker.DoWork += delegate(
                object sender,
                DoWorkEventArgs e)
            {
                IList<string> open =
                    EngineClient.ListOpenWidgets();
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                e.Result = new HashSet<string>(
                    open,
                    StringComparer.OrdinalIgnoreCase);
            };
            worker.RunWorkerCompleted += delegate(
                object sender,
                RunWorkerCompletedEventArgs e)
            {
                worker.Dispose();
                if (ReferenceEquals(_stateWorker, worker))
                    _stateWorker = null;
                if (e.Cancelled || e.Error != null ||
                    IsDisposed || Disposing)
                    return;
                _surface.ApplyRunningStates(
                    e.Result as HashSet<string>);
                if (string.Equals(
                    _filter.SelectedItem as string,
                    "Running",
                    StringComparison.Ordinal))
                    ApplyFilter();
            };
            worker.RunWorkerAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _previewGeneration++;
                if (_previewWorker != null &&
                    _previewWorker.IsBusy)
                    _previewWorker.CancelAsync();
                if (_stateWorker != null &&
                    _stateWorker.IsBusy)
                    _stateWorker.CancelAsync();
                _stateTimer.Stop();
                _stateTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class GalleryWidgetItemEventArgs : EventArgs
    {
        public GalleryWidgetItemEventArgs(GalleryWidgetItem item)
        {
            Item = item;
        }

        public GalleryWidgetItem Item { get; private set; }
    }

    internal sealed class VirtualWidgetGallerySurface : Control
    {
        private const int CardWidth = 236;
        private const int CardHeight = 312;
        private const int Gap = 20;
        private const int PaddingSize = 14;
        private readonly List<GalleryWidgetItem> _items;
        private readonly List<int> _visible = new List<int>();
        private readonly Dictionary<string, Image> _previews =
            new Dictionary<string, Image>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Font _nameFont;
        private readonly Brush _cardBrush;
        private readonly Brush _placeholderBrush;
        private readonly Brush _runningBrush;
        private readonly Brush _stoppedBrush;
        private readonly Brush _runningBadgeBrush;
        private readonly Brush _stoppedBadgeBrush;
        private readonly Pen _borderPen;
        private readonly Pen _hoverPen;
        private readonly Pen _selectedPen;
        private readonly VScrollBar _verticalScrollBar;
        private int _columns = 1;
        private int _scrollOffset;
        private bool _updatingScrollBar;
        private int _hovered = -1;
        private int _selected = -1;
        private int _paintedCardCount;

        public VirtualWidgetGallerySurface(
            List<GalleryWidgetItem> items)
        {
            _items = items;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
            BackColor = EmilyDeskDesignTokens.Canvas;
            TabStop = true;
            AccessibleName = "Widgets";
            AccessibleDescription =
                "Use the arrow keys to choose a widget and Enter to launch or show it.";
            _verticalScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                SmallChange = 48,
                TabStop = false
            };
            _verticalScrollBar.ValueChanged += delegate
            {
                if (_updatingScrollBar) return;
                _scrollOffset = _verticalScrollBar.Value;
                Invalidate();
            };
            Controls.Add(_verticalScrollBar);
            _nameFont = new Font(
                "Segoe UI",
                10F,
                FontStyle.Bold);
            _cardBrush = new SolidBrush(EmilyDeskDesignTokens.Surface);
            _placeholderBrush = new SolidBrush(
                Color.FromArgb(238, 241, 245));
            _runningBrush = new SolidBrush(
                EmilyDeskDesignTokens.Success);
            _stoppedBrush = new SolidBrush(
                Color.FromArgb(88, 98, 112));
            _runningBadgeBrush = new SolidBrush(
                EmilyDeskDesignTokens.SuccessSurface);
            _stoppedBadgeBrush = new SolidBrush(
                Color.FromArgb(238, 241, 245));
            _borderPen = new Pen(
                EmilyDeskDesignTokens.Border);
            _hoverPen = new Pen(
                Color.FromArgb(151, 166, 184));
            _selectedPen = new Pen(
                EmilyDeskDesignTokens.Accent,
                2);
            RebuildLayout();
        }

        public event EventHandler<GalleryWidgetItemEventArgs>
            ActivateRequested;
        public event EventHandler<GalleryWidgetItemEventArgs>
            SelectionChanged;
        public event EventHandler<GalleryWidgetItemEventArgs>
            ContextMenuRequested;

        public void ReplaceItems(List<GalleryWidgetItem> items)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            _visible.Clear();
            _selected = -1;
            _hovered = -1;
            _scrollOffset = 0;
            RebuildLayout();
            Invalidate();
        }

        public int TotalItemCount
        {
            get { return _items.Count; }
        }

        public int VisibleItemCount
        {
            get { return _visible.Count; }
        }

        internal bool HasCardActions
        {
            get { return true; }
        }

        public void ApplyFilter(string search, string filter)
        {
            string selectedId = SelectedItem == null
                ? null
                : SelectedItem.GalleryKey;
            _visible.Clear();
            for (int index = 0; index < _items.Count; index++)
            {
                GalleryWidgetItem item = _items[index];
                bool include = item.Matches(search) &&
                    (filter == "All widgets" ||
                    (filter == "Running" && item.IsRunning) ||
                    (filter == "Imported" && item.IsImported) ||
                    (filter == "Legacy" && item.IsLegacy) ||
                    (filter == "EmilyDesk" &&
                        !item.IsLegacy && !item.IsImported));
                if (include) _visible.Add(index);
            }
            _selected = FindVisibleIndex(selectedId);
            if (_selected < 0 && _visible.Count > 0)
                _selected = 0;
            _hovered = -1;
            _scrollOffset = 0;
            RebuildLayout();
            Invalidate();
        }

        public void SetPreview(string widgetId, Image image)
        {
            _previews[widgetId] = image;
            int visibleIndex = FindVisibleIndex(widgetId);
            if (visibleIndex >= 0 &&
                CardBounds(visibleIndex).IntersectsWith(ClientRectangle))
                Invalidate(CardBounds(visibleIndex));
        }

        public List<GalleryWidgetItem> ItemsSnapshot()
        {
            return new List<GalleryWidgetItem>(_items);
        }

        public void ClearPreviews()
        {
            _previews.Clear();
            Invalidate();
        }

        public void ClearTransientState()
        {
            _selected = -1;
            _hovered = -1;
            _scrollOffset = 0;
            Invalidate();
        }

        public void ApplyRunningStates(HashSet<string> running)
        {
            if (running == null) return;
            for (int index = 0; index < _items.Count; index++)
            {
                GalleryWidgetItem item = _items[index];
                bool value = running.Contains(
                    item.Descriptor.Id);
                if (item.IsRunning == value) continue;
                item.IsRunning = value;
                int visibleIndex = _visible.IndexOf(index);
                if (visibleIndex >= 0 &&
                    CardBounds(visibleIndex)
                        .IntersectsWith(ClientRectangle))
                    Invalidate(CardBounds(visibleIndex));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                e.Graphics.Clear(BackColor);
                _paintedCardCount = 0;
                if (_visible.Count == 0) return;
                int rowHeight = CardHeight + Gap;
                int firstRow = Math.Max(
                    0,
                    _scrollOffset / rowHeight);
                int lastRow = Math.Min(
                    RowCount() - 1,
                    (_scrollOffset + ClientSize.Height) /
                        rowHeight + 1);
                int firstIndex = firstRow * _columns;
                int lastIndex = Math.Min(
                    _visible.Count - 1,
                    (lastRow + 1) * _columns - 1);
                int painted = 0;
                for (int visibleIndex = firstIndex;
                    visibleIndex <= lastIndex;
                    visibleIndex++)
                {
                    Rectangle bounds = CardBounds(visibleIndex);
                    if (!e.ClipRectangle.IntersectsWith(bounds))
                        continue;
                    DrawCard(
                        e.Graphics,
                        bounds,
                        _items[_visible[visibleIndex]],
                        visibleIndex);
                    painted++;
                }
                _paintedCardCount = painted;
            }
            finally
            {
                long elapsed = timer.ElapsedMilliseconds;
                if (elapsed > 25)
                {
                    DragDiagnosticTrace.RecordDuration(
                        "Production Virtual Gallery",
                        "virtual gallery paint",
                        elapsed,
                        "visibleCardCount=" + _paintedCardCount +
                        ";totalCardCount=" + _items.Count);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RebuildLayout();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTest(e.Location);
            if (hit == _hovered) return;
            int previous = _hovered;
            _hovered = hit;
            InvalidateCard(previous);
            InvalidateCard(_hovered);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            int previous = _hovered;
            _hovered = -1;
            InvalidateCard(previous);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int hit = HitTest(e.Location);
            SelectVisibleIndex(hit);
            if (e.Button == MouseButtons.Right && hit >= 0)
            {
                EventHandler<GalleryWidgetItemEventArgs> handler =
                    ContextMenuRequested;
                if (handler != null)
                    handler(
                        this,
                        new GalleryWidgetItemEventArgs(SelectedItem));
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (hit >= 0 && ActionBounds(hit).Contains(e.Location))
                RequestActivation();
        }

        protected override void OnMouseDoubleClick(
            MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            int hit = HitTest(e.Location);
            if (hit >= 0)
            {
                SelectVisibleIndex(hit);
                RequestActivation();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int lines = SystemInformation.MouseWheelScrollLines;
            int step = lines < 0
                ? ClientSize.Height
                : Math.Max(48, lines * 24);
            _scrollOffset -= Math.Sign(e.Delta) * step;
            ClampScroll();
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_visible.Count == 0) return;
            int next = _selected < 0 ? 0 : _selected;
            switch (e.KeyCode)
            {
                case Keys.Left:
                    next--;
                    break;
                case Keys.Right:
                    next++;
                    break;
                case Keys.Up:
                    next -= _columns;
                    break;
                case Keys.Down:
                    next += _columns;
                    break;
                case Keys.Home:
                    next = 0;
                    break;
                case Keys.End:
                    next = _visible.Count - 1;
                    break;
                case Keys.PageUp:
                    next -= Math.Max(
                        _columns,
                        VisibleRows() * _columns);
                    break;
                case Keys.PageDown:
                    next += Math.Max(
                        _columns,
                        VisibleRows() * _columns);
                    break;
                case Keys.Enter:
                case Keys.Space:
                    RequestActivation();
                    e.Handled = true;
                    return;
                default:
                    return;
            }
            SelectVisibleIndex(Math.Max(
                0,
                Math.Min(_visible.Count - 1, next)));
            EnsureSelectedVisible();
            e.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nameFont.Dispose();
                _cardBrush.Dispose();
                _placeholderBrush.Dispose();
                _runningBrush.Dispose();
                _stoppedBrush.Dispose();
                _runningBadgeBrush.Dispose();
                _stoppedBadgeBrush.Dispose();
                _borderPen.Dispose();
                _hoverPen.Dispose();
                _selectedPen.Dispose();
            }
            base.Dispose(disposing);
        }

        private GalleryWidgetItem SelectedItem
        {
            get
            {
                return _selected < 0 ||
                    _selected >= _visible.Count
                    ? null
                    : _items[_visible[_selected]];
            }
        }

        private void DrawCard(
            Graphics graphics,
            Rectangle bounds,
            GalleryWidgetItem item,
            int visibleIndex)
        {
            graphics.FillRectangle(_cardBrush, bounds);
            Rectangle preview = new Rectangle(
                bounds.Left + 10,
                bounds.Top + 10,
                216,
                172);
            graphics.FillRectangle(_placeholderBrush, preview);
            Image image;
            if (_previews.TryGetValue(
                item.GalleryKey,
                out image) &&
                image != null)
                graphics.DrawImage(image, preview);

            TextRenderer.DrawText(
                graphics,
                item.DisplayName,
                _nameFont,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 190,
                    216,
                    38),
                ForeColor,
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                graphics,
                item.Descriptor.IsOfficial
                    ? "Official" +
                        (string.IsNullOrWhiteSpace(item.Descriptor.Category)
                            ? string.Empty
                            : " - " + item.Descriptor.Category)
                    : item.TypeName,
                Font,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 236,
                    86,
                    22),
                Color.FromArgb(88, 98, 112),
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
            Rectangle badge = new Rectangle(
                bounds.Left + 98,
                bounds.Top + 234,
                128,
                22);
            graphics.FillRectangle(
                item.IsRunning
                    ? _runningBadgeBrush
                    : _stoppedBadgeBrush,
                badge);
            TextRenderer.DrawText(
                graphics,
                item.IsRunning ? "Running" : "Not running",
                Font,
                badge,
                item.IsRunning
                    ? ((SolidBrush)_runningBrush).Color
                    : ((SolidBrush)_stoppedBrush).Color,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            Rectangle action = ActionBounds(visibleIndex);
            using (var actionBrush = new SolidBrush(
                EmilyDeskDesignTokens.Accent))
                graphics.FillRectangle(actionBrush, action);
            TextRenderer.DrawText(
                graphics,
                item.IsRunning && !string.IsNullOrWhiteSpace(item.Theme)
                    ? "Use this theme"
                    : item.IsRunning ? "Show widget" : "Launch widget",
                Font,
                action,
                Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            Pen border = visibleIndex == _selected
                ? _selectedPen
                : visibleIndex == _hovered
                    ? _hoverPen
                    : _borderPen;
            graphics.DrawRectangle(
                border,
                bounds.Left,
                bounds.Top,
                bounds.Width - 1,
                bounds.Height - 1);
        }

        private int HitTest(Point point)
        {
            int x = point.X - PaddingSize;
            int y = point.Y + _scrollOffset - PaddingSize;
            if (x < 0 || y < 0) return -1;
            int column = x / (CardWidth + Gap);
            int row = y / (CardHeight + Gap);
            if (column >= _columns ||
                x % (CardWidth + Gap) >= CardWidth ||
                y % (CardHeight + Gap) >= CardHeight)
                return -1;
            int index = row * _columns + column;
            return index < _visible.Count ? index : -1;
        }

        private Rectangle CardBounds(int visibleIndex)
        {
            int row = visibleIndex / _columns;
            int column = visibleIndex % _columns;
            return new Rectangle(
                PaddingSize + column * (CardWidth + Gap),
                PaddingSize + row * (CardHeight + Gap) -
                    _scrollOffset,
                CardWidth,
                CardHeight);
        }

        private Rectangle ActionBounds(int visibleIndex)
        {
            Rectangle card = CardBounds(visibleIndex);
            return new Rectangle(
                card.Left + 10,
                card.Top + 268,
                card.Width - 20,
                32);
        }

        private void RebuildLayout()
        {
            int availableWidth = ClientSize.Width -
                _verticalScrollBar.Width;
            _columns = Math.Max(
                1,
                Math.Max(1, availableWidth - PaddingSize * 2) /
                    (CardWidth + Gap));
            ClampScroll();
        }

        private int RowCount()
        {
            return (_visible.Count + _columns - 1) / _columns;
        }

        private int VisibleRows()
        {
            return Math.Max(
                1,
                ClientSize.Height / (CardHeight + Gap));
        }

        private void ClampScroll()
        {
            int contentHeight =
                PaddingSize * 2 +
                RowCount() * (CardHeight + Gap);
            int maximumOffset = Math.Max(
                0,
                contentHeight - ClientSize.Height);
            _scrollOffset = Math.Max(
                0,
                Math.Min(
                    _scrollOffset,
                    maximumOffset));
            _updatingScrollBar = true;
            try
            {
                _verticalScrollBar.LargeChange = Math.Max(
                    1,
                    ClientSize.Height);
                _verticalScrollBar.Maximum = maximumOffset +
                    _verticalScrollBar.LargeChange - 1;
                _verticalScrollBar.Value = Math.Min(
                    maximumOffset,
                    _scrollOffset);
                _verticalScrollBar.Enabled = maximumOffset > 0;
            }
            finally
            {
                _updatingScrollBar = false;
            }
        }

        private void EnsureSelectedVisible()
        {
            if (_selected < 0) return;
            Rectangle bounds = CardBounds(_selected);
            if (bounds.Top < 0)
                _scrollOffset += bounds.Top;
            else if (bounds.Bottom > ClientSize.Height)
                _scrollOffset +=
                    bounds.Bottom - ClientSize.Height;
            ClampScroll();
            Invalidate();
        }

        private void SelectVisibleIndex(int index)
        {
            if (index < 0 || index >= _visible.Count)
                return;
            int previous = _selected;
            _selected = index;
            InvalidateCard(previous);
            InvalidateCard(_selected);
            EventHandler<GalleryWidgetItemEventArgs> handler =
                SelectionChanged;
            if (handler != null)
                handler(
                    this,
                    new GalleryWidgetItemEventArgs(SelectedItem));
        }

        private void RequestActivation()
        {
            GalleryWidgetItem item = SelectedItem;
            if (item == null) return;
            EventHandler<GalleryWidgetItemEventArgs> handler =
                ActivateRequested;
            if (handler != null)
                handler(
                    this,
                    new GalleryWidgetItemEventArgs(item));
        }

        private void InvalidateCard(int visibleIndex)
        {
            if (visibleIndex < 0 ||
                visibleIndex >= _visible.Count)
                return;
            Rectangle bounds = CardBounds(visibleIndex);
            if (bounds.IntersectsWith(ClientRectangle))
                Invalidate(bounds);
        }

        private int FindVisibleIndex(string widgetId)
        {
            if (string.IsNullOrWhiteSpace(widgetId))
                return -1;
            for (int visibleIndex = 0;
                visibleIndex < _visible.Count;
                visibleIndex++)
            {
                if (string.Equals(
                    _items[_visible[visibleIndex]].GalleryKey,
                    widgetId,
                    StringComparison.OrdinalIgnoreCase))
                    return visibleIndex;
            }
            return -1;
        }
    }

    internal sealed class VirtualPreviewResult
    {
        public VirtualPreviewResult(
            int generation,
            string widgetId,
            Image image)
        {
            Generation = generation;
            WidgetId = widgetId;
            Image = image;
        }

        public int Generation { get; private set; }
        public string WidgetId { get; private set; }
        public Image Image { get; private set; }
    }

    internal static class VirtualThumbnailCache
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Image> Images =
            new Dictionary<string, Image>(
                StringComparer.OrdinalIgnoreCase);
        private static int Generation;

        public static Image GetOrLoad(GalleryWidgetItem item)
        {
            int generation;
            lock (Sync) generation = Generation;
            string path = WidgetPreviewResolver.Resolve(
                item,
                null);
            string key = CacheKey(item, path);
            lock (Sync)
            {
                Image cached;
                if (Images.TryGetValue(key, out cached))
                    return cached;
            }

            Image loaded = WidgetPreviewResolver.LoadThumbnail(
                path,
                new Size(216, 172),
                null);
            lock (Sync)
            {
                if (generation != Generation)
                    return loaded;
                Image cached;
                if (Images.TryGetValue(key, out cached))
                {
                    loaded.Dispose();
                    return cached;
                }
                Images[key] = loaded;
                return loaded;
            }
        }

        public static void Clear()
        {
            // A preview worker can still be completing while the Gallery is
            // closing. Drop the cache references without disposing images
            // that an in-flight owner-drawn surface may still paint.
            lock (Sync)
            {
                Generation++;
                Images.Clear();
            }
        }

        private static string CacheKey(
            GalleryWidgetItem item,
            string path)
        {
            string stamp = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(path) &&
                    File.Exists(path))
                {
                    var file = new FileInfo(path);
                    stamp = file.Length + ":" +
                        file.LastWriteTimeUtc.Ticks;
                }
            }
            catch { }
            return item.GalleryKey + "|" + (path ?? string.Empty) + "|" +
                stamp;
        }
    }
}
