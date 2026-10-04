using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal sealed class WidgetManagerForm : Form
    {
        private const string DragInstruction =
            "Begin every drag from the actual Windows title bar above this content.";

        private enum DiagnosticShellMode
        {
            None,
            FormOnly,
            HeaderOnly,
            Complete
        }

        internal enum GalleryTestMode
        {
            Normal,
            CardsNoPreviews,
            PreviewsNoState,
            PreloadedPreviews,
            SinglePreviewApply,
            StateSnapshot,
            NativeCards25,
            NativeCards100
        }

        private readonly TextBox _search;
        private readonly ComboBox _filter;
        private readonly FlowLayoutPanel _cardsPanel;
        private readonly Label _summary;
        private readonly Label _status;
        private readonly Button _install;
        private readonly Timer _stateTimer;
        private readonly List<GalleryWidgetItem> _items =
            new List<GalleryWidgetItem>();
        private readonly Dictionary<string, WidgetGalleryCard> _cards =
            new Dictionary<string, WidgetGalleryCard>(
                StringComparer.OrdinalIgnoreCase);
        private readonly List<Timer> _activationTimers =
            new List<Timer>();
        private WidgetGalleryCard _selectedCard;
        private BackgroundWorker _previewWorker;
        private BackgroundWorker _statePollWorker;
        private int _previewGeneration;
        private Timer _refreshTimer;
        private string _pendingWidgetId;
        private string _pendingWidgetName;
        private bool _pendingWasReplacement;
        private DateTime _refreshDeadlineUtc;
        private readonly GalleryTestMode _galleryMode;
        private readonly string _diagnosticTestName;
        private string _previewMode;
        private string _runningStateMode;
        private int _previewResultsQueued;
        private int _previewResultsApplied;
        private bool _previewApplyCallbackActive;
        private bool _runningStateCallbackActive;
        private bool _layoutActive;
        private bool _paintActive;
        private int _visibleCardCount;

        public WidgetManagerForm(
            IList<WidgetDescriptor> widgets)
            : this(
                widgets,
                DiagnosticShellMode.None,
                GalleryTestMode.Normal,
                "TEST C — Normal Gallery")
        {
        }

        private WidgetManagerForm(
            IList<WidgetDescriptor> widgets,
            DiagnosticShellMode diagnosticMode,
            GalleryTestMode galleryMode,
            string diagnosticTestName)
        {
            _search = null;
            _filter = null;
            _cardsPanel = null;
            _summary = null;
            _status = null;
            _install = null;
            _stateTimer = null;
            _galleryMode = galleryMode;
            _diagnosticTestName = diagnosticTestName;
            _previewMode = PreviewModeName(galleryMode);
            _runningStateMode = RunningStateModeName(galleryMode);

            bool diagnostic =
                diagnosticMode != DiagnosticShellMode.None;
            bool galleryDiagnostic =
                galleryMode != GalleryTestMode.Normal;
            Text = diagnostic
                ? "EmilyDesk Drag Test — " +
                    diagnosticMode + " — " + DragInstruction
                : galleryDiagnostic
                    ? diagnosticTestName + " — " + DragInstruction
                : "EmilyDesk Gallery — Normal";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(760, 560);
            ClientSize = new Size(1080, 720);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(243, 246, 249);
            AutoScaleMode = AutoScaleMode.Dpi;
            Activated += delegate
            {
                WidgetLayerMenuFactory.RaiseAfterActivation(this);
            };
            ContextMenuStrip = WidgetLayerMenuFactory.CreateContextMenu(this);

            if (diagnosticMode == DiagnosticShellMode.FormOnly)
                return;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                Padding = new Padding(22, 15, 22, 12),
                BackColor = Color.White
            };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                Location = new Point(22, 14),
                Size = new Size(320, 32),
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                Text = "EmilyDesk Gallery"
            });
            header.Controls.Add(new Label
            {
                Location = new Point(24, 48),
                Size = new Size(560, 40),
                ForeColor = Color.FromArgb(93, 103, 118),
                Text = diagnostic
                    ? DragInstruction
                    : "Browse, launch, and manage installed widgets"
            });

            if (diagnosticMode == DiagnosticShellMode.HeaderOnly)
                return;

            _search = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(620, 23),
                Size = new Size(250, 29)
            };
            if (!diagnostic)
                _search.TextChanged += delegate { ApplyFilter(); };
            header.Controls.Add(_search);
            var searchHint = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(620, 55),
                Size = new Size(250, 20),
                ForeColor = Color.FromArgb(110, 120, 134),
                Text = "Search name, author, description, or version"
            };
            header.Controls.Add(searchHint);

            _filter = new ComboBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(885, 23),
                Size = new Size(150, 29),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _filter.Items.AddRange(new object[]
            {
                "All", "Running", "Legacy", "EmilyDesk"
            });
            _filter.SelectedIndex = 0;
            if (!diagnostic)
                _filter.SelectedIndexChanged +=
                    delegate { ApplyFilter(); };
            header.Controls.Add(_filter);

            _cardsPanel = new DiagnosticFlowLayoutPanel(
                BeginLayoutDiagnostic,
                EndLayoutDiagnostic)
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(14),
                BackColor = Color.FromArgb(243, 246, 249)
            };
            Controls.Add(_cardsPanel);
            _cardsPanel.BringToFront();

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                Padding = new Padding(18, 12, 18, 10),
                BackColor = Color.White
            };
            Controls.Add(footer);

            _install = new Button
            {
                Text = "Import Widget...",
                Location = new Point(18, 13),
                Size = new Size(205, 36)
            };
            if (!diagnostic)
                _install.Click +=
                    delegate { InstallWidgetPackage(); };
            footer.Controls.Add(_install);

            _summary = new Label
            {
                Location = new Point(240, 20),
                Size = new Size(230, 24),
                ForeColor = Color.FromArgb(92, 102, 116)
            };
            footer.Controls.Add(_summary);

            _status = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(480, 20),
                Size = new Size(430, 24),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(63, 75, 91)
            };
            footer.Controls.Add(_status);

            var done = new Button
            {
                Text = "Done",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(930, 13),
                Size = new Size(105, 36)
            };
            done.Click += delegate { Close(); };
            footer.Controls.Add(done);
            AcceptButton = done;
            CancelButton = done;

            if (diagnosticMode == DiagnosticShellMode.Complete)
            {
                _install.Enabled = false;
                _summary.Text = "0 diagnostic cards";
                _status.Text =
                    "No discovery, previews, polling, or package access.";
                return;
            }

            PopulateWidgets(widgets, null);
            if (galleryMode != GalleryTestMode.PreviewsNoState &&
                galleryMode != GalleryTestMode.StateSnapshot &&
                galleryMode != GalleryTestMode.NativeCards25 &&
                galleryMode != GalleryTestMode.NativeCards100)
            {
                _stateTimer = new Timer { Interval = 750 };
                _stateTimer.Tick += delegate
                {
                    QueueRunningStateRefresh();
                };
                _stateTimer.Start();
            }
        }

        public static WidgetManagerForm CreateDiagnosticFormOnly()
        {
            return new WidgetManagerForm(
                new List<WidgetDescriptor>(),
                DiagnosticShellMode.FormOnly,
                GalleryTestMode.Normal,
                "TEST B1 — Form Properties Only");
        }

        public static WidgetManagerForm CreateDiagnosticHeaderOnly()
        {
            return new WidgetManagerForm(
                new List<WidgetDescriptor>(),
                DiagnosticShellMode.HeaderOnly,
                GalleryTestMode.Normal,
                "TEST B2 — Header Only");
        }

        public static WidgetManagerForm CreateDiagnosticCompleteShell()
        {
            return new WidgetManagerForm(
                new List<WidgetDescriptor>(),
                DiagnosticShellMode.Complete,
                GalleryTestMode.Normal,
                "TEST B3 — Complete Empty Shell");
        }

        public static WidgetManagerForm CreateGalleryDiagnostic(
            IList<WidgetDescriptor> widgets,
            GalleryTestMode mode,
            string testName)
        {
            return new WidgetManagerForm(
                widgets,
                DiagnosticShellMode.None,
                mode,
                testName);
        }

        public bool DiagnosticPreviewLoading
        {
            get
            {
                return _previewWorker != null &&
                    _previewWorker.IsBusy;
            }
        }

        public bool DiagnosticStatePolling
        {
            get
            {
                return _statePollWorker != null &&
                    _statePollWorker.IsBusy;
            }
        }

        public string DiagnosticSubsystemState
        {
            get
            {
                return
                    "testName=" + _diagnosticTestName +
                    ";cardCount=" + _cards.Count +
                    ";visibleCardCount=" +
                        _visibleCardCount +
                    ";previewMode=" + _previewMode +
                    ";previewWorkerActive=" +
                        DiagnosticPreviewLoading +
                    ";previewResultsQueued=" +
                        _previewResultsQueued +
                    ";previewResultsApplied=" +
                        _previewResultsApplied +
                    ";previewApplyCallbackActive=" +
                        _previewApplyCallbackActive +
                    ";runningStateMode=" + _runningStateMode +
                    ";runningStateWorkerActive=" +
                        DiagnosticStatePolling +
                    ";runningStateCallbackActive=" +
                        _runningStateCallbackActive +
                    ";layoutActive=" + _layoutActive +
                    ";paintActive=" + _paintActive;
            }
        }

        private WidgetDescriptor SelectedWidget
        {
            get
            {
                return _selectedCard == null
                    ? null
                    : _selectedCard.Item.Descriptor;
            }
        }

        private void PopulateWidgets(
            IList<WidgetDescriptor> widgets,
            string preferredWidgetId)
        {
            Stopwatch operation = Stopwatch.StartNew();
            string selectedId = preferredWidgetId ??
                (SelectedWidget == null ? null : SelectedWidget.Id);
            foreach (WidgetGalleryCard card in _cards.Values)
                card.Dispose();
            _cards.Clear();
            _items.Clear();
            _cardsPanel.Controls.Clear();
            _selectedCard = null;

            var running = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (_galleryMode != GalleryTestMode.PreviewsNoState)
            {
                Stopwatch stateRead = Stopwatch.StartNew();
                running = new HashSet<string>(
                    EngineClient.ListOpenWidgets(),
                    StringComparer.OrdinalIgnoreCase);
                RecordDuration(
                    "ListOpenWidgets",
                    stateRead.ElapsedMilliseconds);
            }
            Stopwatch cardConstruction = Stopwatch.StartNew();
            int cardLimit = CardLimit(_galleryMode);
            int descriptorIndex = 0;
            foreach (WidgetDescriptor descriptor in widgets)
            {
                if (descriptorIndex >= cardLimit)
                    break;
                descriptorIndex++;
                var item = new GalleryWidgetItem(descriptor)
                {
                    IsRunning = running.Contains(descriptor.Id)
                };
                _items.Add(item);
                var card = new WidgetGalleryCard(
                    item,
                    PreviewPaintCompleted);
                card.Selected += CardSelected;
                card.ActivateRequested += delegate
                {
                    ActivateSelectedWidget();
                };
                card.ContextRequested += delegate
                {
                    ShowCardContextMenu(card);
                };
                _cards[descriptor.Id] = card;
                if (string.Equals(
                    descriptor.Id,
                    selectedId,
                    StringComparison.OrdinalIgnoreCase))
                    SelectCard(card);
            }
            RecordDuration(
                "card construction",
                cardConstruction.ElapsedMilliseconds);
            ApplyFilter();
            if (_galleryMode == GalleryTestMode.CardsNoPreviews ||
                _galleryMode == GalleryTestMode.NativeCards25 ||
                _galleryMode == GalleryTestMode.NativeCards100)
            {
                RecordDuration(
                    "widget population",
                    operation.ElapsedMilliseconds);
                return;
            }
            if (_galleryMode == GalleryTestMode.PreloadedPreviews)
                LoadPreviewsBeforeDisplay();
            else
                LoadPreviewsAsync(
                    _galleryMode ==
                        GalleryTestMode.SinglePreviewApply);
            RecordDuration(
                "widget population",
                operation.ElapsedMilliseconds);
        }

        private void ActivateSelectedWidget()
        {
            WidgetDescriptor widget = SelectedWidget;
            if (widget == null) return;

            bool sent = EngineClient.OpenWidget(widget.Id);
            if (!sent)
            {
                MessageBox.Show(
                    this,
                    "The Engine did not accept the command for '" +
                    widget.Name + "'.",
                    "EmilyDesk Gallery",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            _status.Text = "Launching " + widget.Name + "...";
            BeginStateRefresh(widget.Id);
        }

        private void CardSelected(object sender, EventArgs e)
        {
            SelectCard((WidgetGalleryCard)sender);
        }

        private void SelectCard(WidgetGalleryCard card)
        {
            if (_selectedCard != null)
                _selectedCard.IsSelected = false;
            _selectedCard = card;
            if (_selectedCard != null)
            {
                _selectedCard.IsSelected = true;
                _status.Text = _selectedCard.Item.Descriptor.Name +
                    " — double-click the preview to " +
                    (_selectedCard.Item.IsRunning
                        ? "bring it to front"
                        : "launch it");
            }
        }

        private void ApplyFilter()
        {
            Stopwatch total = Stopwatch.StartNew();
            string filter = _filter.SelectedItem == null
                ? "All"
                : _filter.SelectedItem.ToString();
            int visibleCount = 0;
            _cardsPanel.SuspendLayout();
            try
            {
                Stopwatch clear = Stopwatch.StartNew();
                _cardsPanel.Controls.Clear();
                RecordDuration(
                    "ApplyFilter Controls.Clear",
                    clear.ElapsedMilliseconds);
                Stopwatch add = Stopwatch.StartNew();
                foreach (GalleryWidgetItem item in _items)
                {
                    bool include = item.Matches(_search.Text) &&
                        (filter == "All" ||
                        (filter == "Running" && item.IsRunning) ||
                        (filter == "Legacy" && item.IsLegacy) ||
                (filter == "EmilyDesk" && !item.IsLegacy));
                    if (!include) continue;
                    _cardsPanel.Controls.Add(_cards[
                        item.Descriptor.Id]);
                    visibleCount++;
                }
                RecordDuration(
                    "ApplyFilter Controls.Add",
                    add.ElapsedMilliseconds);
            }
            finally
            {
                _cardsPanel.ResumeLayout();
            }
            _summary.Text = visibleCount + " of " +
                _items.Count + " widgets";
            _visibleCardCount = visibleCount;
            if (visibleCount == 0)
                _status.Text = _items.Count == 0
                    ? "No widgets are installed."
                    : "No widgets match the current search and filter.";
            RecordDuration(
                "ApplyFilter",
                total.ElapsedMilliseconds);
        }

        private void QueueRunningStateRefresh()
        {
            if (IsDisposed || Disposing ||
                (_statePollWorker != null &&
                _statePollWorker.IsBusy))
                return;

            var worker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            _statePollWorker = worker;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                Stopwatch timer = Stopwatch.StartNew();
                IList<string> open =
                    EngineClient.ListOpenWidgets();
                RecordDuration(
                    "ListOpenWidgets",
                    timer.ElapsedMilliseconds);
                RecordDuration(
                    "running-state worker",
                    timer.ElapsedMilliseconds);
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
                Stopwatch callback = Stopwatch.StartNew();
                _runningStateCallbackActive = true;
                try
                {
                worker.Dispose();
                if (ReferenceEquals(
                    _statePollWorker,
                    worker))
                    _statePollWorker = null;
                if (e.Cancelled || e.Error != null ||
                    IsDisposed || Disposing)
                    return;
                ApplyRunningStates(
                    e.Result as HashSet<string>);
                }
                finally
                {
                    _runningStateCallbackActive = false;
                    RecordDuration(
                        "running-state completion callback",
                        callback.ElapsedMilliseconds);
                }
            };
            worker.RunWorkerAsync();
        }

        private void ApplyRunningStates(
            HashSet<string> running)
        {
            Stopwatch timer = Stopwatch.StartNew();
            if (running == null) return;
            bool filterNeedsRefresh = string.Equals(
                _filter.SelectedItem as string,
                "Running",
                StringComparison.Ordinal);
            bool changed = false;
            foreach (GalleryWidgetItem item in _items)
            {
                bool isRunning = running.Contains(
                    item.Descriptor.Id);
                if (item.IsRunning == isRunning) continue;
                item.IsRunning = isRunning;
                WidgetGalleryCard card;
                if (_cards.TryGetValue(
                    item.Descriptor.Id,
                    out card))
                {
                    card.SetRunning(isRunning);
                    if (filterNeedsRefresh)
                        UpdateRunningFilterCard(
                            item,
                            card,
                            isRunning);
                }
                changed = true;
            }
            if (changed && filterNeedsRefresh)
                UpdateVisibleSummary();
            RecordDuration(
                "ApplyRunningStates",
                timer.ElapsedMilliseconds);
        }

        private void UpdateRunningFilterCard(
            GalleryWidgetItem item,
            WidgetGalleryCard card,
            bool running)
        {
            bool include = running &&
                item.Matches(_search.Text);
            if (!include)
            {
                _cardsPanel.Controls.Remove(card);
                return;
            }
            if (!_cardsPanel.Controls.Contains(card))
                _cardsPanel.Controls.Add(card);

            int index = 0;
            foreach (GalleryWidgetItem candidate in _items)
            {
                if (ReferenceEquals(candidate, item))
                    break;
                if (candidate.IsRunning &&
                    candidate.Matches(_search.Text))
                    index++;
            }
            _cardsPanel.Controls.SetChildIndex(card, index);
        }

        private void UpdateVisibleSummary()
        {
            int visibleCount = _cardsPanel.Controls.Count;
            _visibleCardCount = visibleCount;
            _summary.Text = visibleCount + " of " +
                _items.Count + " widgets";
            if (visibleCount == 0)
                _status.Text = _items.Count == 0
                    ? "No widgets are installed."
                    : "No widgets match the current search and filter.";
        }

        private void BeginStateRefresh(string widgetId)
        {
            var timer = new Timer { Interval = 250 };
            _activationTimers.Add(timer);
            int attempts = 0;
            timer.Tick += delegate
            {
                attempts++;
                QueueRunningStateRefresh();
                WidgetGalleryCard card;
                bool running = _cards.TryGetValue(
                    widgetId,
                    out card) &&
                    card.Item.IsRunning;
                if (!running && attempts < 20) return;
                timer.Stop();
                _activationTimers.Remove(timer);
                timer.Dispose();
                if (running)
                    _status.Text =
                        card.Item.Descriptor.Name + " is running.";
            };
            timer.Start();
        }

        private void ShowCardContextMenu(
            WidgetGalleryCard card)
        {
            SelectCard(card);
            var menu = new ContextMenuStrip();
            var activate = new ToolStripMenuItem(
                card.Item.IsRunning
                    ? "Bring to Front"
                    : "Open");
            activate.Click += delegate
            {
                menu.Dispose();
                ActivateSelectedWidget();
            };
            menu.Items.Add(activate);

            var close = new ToolStripMenuItem("Close")
            {
                Enabled = card.Item.IsRunning
            };
            close.Click += delegate
            {
                menu.Dispose();
                CloseSelectedWidget();
            };
            menu.Items.Add(close);
            menu.Items.Add(new ToolStripSeparator());

            var details = new ToolStripMenuItem("Details");
            details.Click += delegate
            {
                ShowSelectedDetails();
                menu.Dispose();
            };
            menu.Items.Add(details);

            var folder = new ToolStripMenuItem(
                "Open Widget Folder")
            {
                Enabled = Directory.Exists(
                    ResolveSelectedFolder())
            };
            folder.Click += delegate
            {
                OpenSelectedFolder();
                menu.Dispose();
            };
            menu.Items.Add(folder);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Uninstall")
            {
                Enabled = false,
                ToolTipText =
                    "Safe uninstall transactions are not yet supported."
            });
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(Cursor.Position);
        }

        private void CloseSelectedWidget()
        {
            WidgetDescriptor widget = SelectedWidget;
            if (widget == null) return;
            if (!EngineClient.CloseWidget(widget.Id))
            {
                MessageBox.Show(
                    this,
                    "The Engine did not accept the close command for '" +
                    widget.Name + "'.",
                        "EmilyDesk Gallery",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            _status.Text = "Closing " + widget.Name + "...";
            BeginStateRefresh(widget.Id);
        }

        private void ShowSelectedDetails()
        {
            WidgetDescriptor widget = SelectedWidget;
            if (widget == null) return;
            GalleryWidgetItem item = _selectedCard.Item;
            string version = string.IsNullOrWhiteSpace(widget.Version)
                ? "Not specified"
                : widget.Version;
            string author = string.IsNullOrWhiteSpace(widget.Author)
                ? "Not specified"
                : widget.Author;
            MessageBox.Show(
                this,
                widget.Name + Environment.NewLine + Environment.NewLine +
                "Type: " + item.TypeName + Environment.NewLine +
                "Version: " + version + Environment.NewLine +
                "Author: " + author + Environment.NewLine +
                "State: " +
                (item.IsRunning ? "Running" : "Stopped") +
                Environment.NewLine + Environment.NewLine +
                widget.Description,
                "Widget Details",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private string ResolveSelectedFolder()
        {
            WidgetDescriptor widget = SelectedWidget;
            if (widget == null) return string.Empty;
            string path = !string.IsNullOrWhiteSpace(
                widget.PreviewPath)
                ? widget.PreviewPath
                : widget.IconPath;
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try { return Path.GetDirectoryName(path); }
            catch { return string.Empty; }
        }

        private void OpenSelectedFolder()
        {
            string folder = ResolveSelectedFolder();
            if (!Directory.Exists(folder)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + folder + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Unable to Open Widget Folder",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void LoadPreviewsBeforeDisplay()
        {
            var results = new List<PreviewResult>();
            foreach (GalleryWidgetItem item in _items)
                results.Add(LoadPreview(item));
            ApplyPreviewResults(results);
        }

        private void LoadPreviewsAsync(bool applyOnce)
        {
            int generation = ++_previewGeneration;
            if (_previewWorker != null &&
                _previewWorker.IsBusy)
                _previewWorker.CancelAsync();
            var snapshot = new List<GalleryWidgetItem>(_items);
            var worker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = !applyOnce
            };
            _previewWorker = worker;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                var batch = new List<PreviewResult>();
                foreach (GalleryWidgetItem item in snapshot)
                {
                    if (worker.CancellationPending)
                    {
                        DisposePreviewResults(batch);
                        e.Cancel = true;
                        return;
                    }
                    PreviewResult result = LoadPreview(item);
                    if (worker.CancellationPending)
                    {
                        result.Image.Dispose();
                        DisposePreviewResults(batch);
                        e.Cancel = true;
                        return;
                    }
                    batch.Add(result);
                    if (applyOnce || batch.Count < 4) continue;
                    Stopwatch queue = Stopwatch.StartNew();
                    _previewResultsQueued += batch.Count;
                    worker.ReportProgress(
                        0,
                        new PreviewBatch(
                            generation,
                            batch));
                    RecordDuration(
                        "ReportProgress queueing",
                        queue.ElapsedMilliseconds);
                    batch = new List<PreviewResult>();
                }
                if (applyOnce)
                {
                    e.Result = batch;
                }
                else if (batch.Count > 0)
                {
                    Stopwatch queue = Stopwatch.StartNew();
                    _previewResultsQueued += batch.Count;
                    worker.ReportProgress(
                        0,
                        new PreviewBatch(
                            generation,
                            batch));
                    RecordDuration(
                        "ReportProgress queueing",
                        queue.ElapsedMilliseconds);
                }
            };
            worker.ProgressChanged += delegate(
                object sender,
                ProgressChangedEventArgs e)
            {
                Stopwatch callback = Stopwatch.StartNew();
                _previewApplyCallbackActive = true;
                try
                {
                var batch = e.UserState as PreviewBatch;
                if (batch == null) return;
                if (IsDisposed || Disposing ||
                    batch.Generation !=
                    _previewGeneration)
                {
                    DisposePreviewResults(batch.Results);
                    return;
                }
                ApplyPreviewResults(batch.Results);
                }
                finally
                {
                    _previewApplyCallbackActive = false;
                    RecordDuration(
                        "ProgressChanged callback",
                        callback.ElapsedMilliseconds);
                }
            };
            worker.RunWorkerCompleted += delegate(
                object sender,
                RunWorkerCompletedEventArgs e)
            {
                worker.Dispose();
                if (ReferenceEquals(_previewWorker, worker))
                    _previewWorker = null;
                if (e.Cancelled)
                    return;
                if (IsDisposed || e.Error != null)
                    return;
                if (applyOnce)
                {
                    var results =
                        e.Result as List<PreviewResult>;
                    if (results != null)
                    {
                        _previewResultsQueued += results.Count;
                        _previewApplyCallbackActive = true;
                        Stopwatch apply = Stopwatch.StartNew();
                        try
                        {
                            ApplyPreviewResults(results);
                        }
                        finally
                        {
                            _previewApplyCallbackActive = false;
                            RecordDuration(
                                "single preview application callback",
                                apply.ElapsedMilliseconds);
                        }
                    }
                }
            };
            worker.RunWorkerAsync();
        }

        private PreviewResult LoadPreview(GalleryWidgetItem item)
        {
            string path = WidgetPreviewResolver.Resolve(
                item,
                RecordDuration);
            Image image = WidgetPreviewResolver.LoadThumbnail(
                path,
                new Size(216, 172),
                RecordDuration);
            return new PreviewResult(
                item.Descriptor.Id,
                image);
        }

        private void ApplyPreviewResults(
            IList<PreviewResult> results)
        {
            foreach (PreviewResult result in results)
            {
                WidgetGalleryCard card;
                if (_cards.TryGetValue(
                    result.WidgetId,
                    out card))
                {
                    Stopwatch setPreview = Stopwatch.StartNew();
                    card.SetPreview(result.Image);
                    _previewResultsApplied++;
                    RecordDuration(
                        "SetPreview",
                        setPreview.ElapsedMilliseconds);
                }
                else
                {
                    result.Image.Dispose();
                }
            }
        }

        private static void DisposePreviewResults(object value)
        {
            var results = value as List<PreviewResult>;
            if (results == null) return;
            foreach (PreviewResult result in results)
                result.Image.Dispose();
        }

        private sealed class PreviewResult
        {
            public PreviewResult(string widgetId, Image image)
            {
                WidgetId = widgetId;
                Image = image;
            }

            public string WidgetId { get; private set; }
            public Image Image { get; private set; }
        }

        private sealed class PreviewBatch
        {
            public PreviewBatch(
                int generation,
                List<PreviewResult> results)
            {
                Generation = generation;
                Results = results;
            }

            public int Generation { get; private set; }
            public List<PreviewResult> Results
            {
                get;
                private set;
            }
        }

        private void InstallWidgetPackage()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Import EmilyDesk Widget";
                dialog.Filter =
                    "EmilyDesk widgets (*.emilywidget)|*.emilywidget|" +
                    "Earlier packages (*.xwrwidget;*.xrwwidget)|*.xwrwidget;*.xrwwidget";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                WidgetPackageInspection inspection = null;
                try
                {
                    if (string.Equals(
                        Path.GetExtension(dialog.FileName),
                        ".xwp",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            this,
                            "Legacy XWidget packages are not supported by EmilyDesk V1.",
                            "Unsupported Widget Package",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    inspection = WidgetPackageInstaller.Inspect(
                        dialog.FileName);
                    bool replace = false;
                    if (inspection.IsInstalled)
                    {
                        string installedName =
                            string.IsNullOrWhiteSpace(inspection.InstalledName)
                                ? inspection.Name
                                : inspection.InstalledName;
                        if (inspection.VersionComparison == 0)
                        {
                            MessageBox.Show(
                                this,
                                installedName + " version " +
                                inspection.InstalledVersion +
                                " is already installed.\r\n\r\n" +
                                "No installation is necessary.",
                                "Widget Already Installed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            return;
                        }
                        if (inspection.VersionComparison < 0)
                        {
                            MessageBox.Show(
                                this,
                                installedName + " version " +
                                inspection.InstalledVersion +
                                " is newer than the selected package.\r\n\r\n" +
                                "No changes were made.",
                                "Newer Version Already Installed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            return;
                        }

                        replace = ConfirmWidgetReplacement(
                            installedName,
                            inspection.InstalledVersion,
                            inspection.Version);
                        if (!replace) return;
                    }

                    WidgetPackageInstallResult result = replace
                        ? WidgetPackageInstaller.Replace(dialog.FileName)
                        : WidgetPackageInstaller.Install(dialog.FileName);
                    CompletePackageInstallation(result, replace);
                }
                catch (Exception ex)
                {
                    WritePackageDiagnostic(
                        dialog.FileName,
                        inspection != null
                            ? inspection.WidgetId
                            : null,
                        ex);
                    MessageBox.Show(
                        this,
                        FriendlyInstallError(ex),
                        "Unable to Install Widget",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void CompletePackageInstallation(
            WidgetPackageInstallResult result,
            bool replaced)
        {
            if (!EngineClient.RefreshWidgets())
                throw new InvalidOperationException(
                    "The widget was installed, but EmilyDesk Gallery " +
                    "could not refresh.");

            _pendingWidgetId = result.WidgetId;
            _pendingWidgetName = result.Name;
            _pendingWasReplacement = replaced;
            _refreshDeadlineUtc = DateTime.UtcNow.AddSeconds(5);
            _install.Enabled = false;
            _status.Text = replaced
                ? "Status: Updating " + result.Name + "..."
                : "Status: Installing " + result.Name + "...";
            StartRegistryRefreshPolling();
        }

        private void StartRegistryRefreshPolling()
        {
            StopRegistryRefreshPolling();
            _refreshTimer = new Timer { Interval = 250 };
            _refreshTimer.Tick += RegistryRefreshTick;
            _refreshTimer.Start();
        }

        private void RegistryRefreshTick(object sender, EventArgs e)
        {
            IList<WidgetDescriptor> widgets =
                EngineClient.ListAvailableWidgets();
            WidgetDescriptor rediscovered = null;
            foreach (WidgetDescriptor widget in widgets)
                if (string.Equals(
                    widget.Id,
                    _pendingWidgetId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    rediscovered = widget;
                    break;
                }

            bool metadataMatches = rediscovered != null &&
                string.Equals(
                    rediscovered.Name,
                    _pendingWidgetName,
                    StringComparison.OrdinalIgnoreCase);
            if (metadataMatches)
            {
                string installedId = _pendingWidgetId;
                StopRegistryRefreshPolling();
                PopulateWidgets(widgets, installedId);
                _install.Enabled = true;
                MessageBox.Show(
                    this,
                    _pendingWidgetName +
                    (_pendingWasReplacement
                        ? " was updated successfully."
                        : " was installed successfully."),
                    _pendingWasReplacement
                        ? "Widget Updated"
                        : "Widget Installed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (DateTime.UtcNow < _refreshDeadlineUtc) return;
            StopRegistryRefreshPolling();
            _install.Enabled = true;
            PopulateWidgets(widgets, null);
            MessageBox.Show(
                this,
                rediscovered == null
                    ? _pendingWidgetName +
                        " was copied, but the Runtime could not find it in " +
                    "EmilyDesk Gallery. Installation was not completed."
                    : _pendingWidgetName +
                        " was copied, but the Runtime read its name as \"" +
                        rediscovered.Name +
                        "\". Installation was not completed.",
                "Widget Installation Incomplete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private bool ConfirmWidgetReplacement(
            string name,
            string installedVersion,
            string packageVersion)
        {
            using (var prompt = new Form())
            {
                prompt.Text = "Update Widget";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.ShowInTaskbar = false;
                prompt.ClientSize = new Size(460, 170);
                prompt.Font = Font;

                var message = new Label
                {
                    Location = new Point(18, 18),
                    Size = new Size(424, 78),
                    Text = name + " version " + installedVersion +
                        " is installed.\r\n\r\n" +
                        "The selected package contains version " +
                        packageVersion + ". Would you like to replace it?"
                };
                prompt.Controls.Add(message);

                var replace = new Button
                {
                    Text = "Replace existing widget",
                    Location = new Point(178, 116),
                    Size = new Size(170, 34),
                    DialogResult = DialogResult.OK
                };
                prompt.Controls.Add(replace);

                var cancel = new Button
                {
                    Text = "Cancel",
                    Location = new Point(358, 116),
                    Size = new Size(84, 34),
                    DialogResult = DialogResult.Cancel
                };
                prompt.Controls.Add(cancel);
                prompt.AcceptButton = replace;
                prompt.CancelButton = cancel;
                return prompt.ShowDialog(this) == DialogResult.OK;
            }
        }

        private bool ConfirmLegacyReplacement(string name)
        {
            using (var prompt = new Form())
            {
                prompt.Text = "Replace Legacy Widget";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.ShowInTaskbar = false;
                prompt.ClientSize = new Size(460, 155);
                prompt.Font = Font;

                prompt.Controls.Add(new Label
                {
                    Location = new Point(18, 18),
                    Size = new Size(424, 65),
                    Text = name + " is already installed.\r\n\r\n" +
                        "Would you like to replace the existing widget?"
                });
                var replace = new Button
                {
                    Text = "Replace existing widget",
                    Location = new Point(178, 101),
                    Size = new Size(170, 34),
                    DialogResult = DialogResult.OK
                };
                var cancel = new Button
                {
                    Text = "Cancel",
                    Location = new Point(358, 101),
                    Size = new Size(84, 34),
                    DialogResult = DialogResult.Cancel
                };
                prompt.Controls.Add(replace);
                prompt.Controls.Add(cancel);
                prompt.AcceptButton = replace;
                prompt.CancelButton = cancel;
                return prompt.ShowDialog(this) == DialogResult.OK;
            }
        }

        private static string FriendlyInstallError(Exception ex)
        {
            if (ex is FileNotFoundException)
                return "The selected widget package could not be found. " +
                    "It may have been moved or deleted.";
            if (ex is InvalidDataException)
                return "This widget package is invalid, incomplete, or " +
                    "not compatible with this version of EmilyDesk.";
            if (ex is IOException || ex is UnauthorizedAccessException)
                return "EmilyDesk could not update the widget files. " +
                    "Close the widget if it is running, then try again.";
            return "The widget could not be installed safely. No existing " +
                "widget files were changed. See the EmilyDesk Dashboard diagnostics " +
                "log for technical details.";
        }

        private static void WritePackageDiagnostic(
            string packagePath,
            string widgetId,
            Exception error)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk",
                    "Logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    Path.Combine(directory, "dashboard-diagnostics.log"),
                    DateTime.UtcNow.ToString("o") +
                    " Widget package install failed. Package=" +
                    packagePath + "; WidgetId=" +
                    (widgetId ?? "(unavailable)") + "; Error=" +
                    error + Environment.NewLine);
            }
            catch { }
        }

        private void StopRegistryRefreshPolling()
        {
            if (_refreshTimer == null) return;
            _refreshTimer.Stop();
            _refreshTimer.Tick -= RegistryRefreshTick;
            _refreshTimer.Dispose();
            _refreshTimer = null;
        }

        private void RecordDuration(
            string operation,
            long milliseconds)
        {
            DragDiagnosticTrace.RecordDuration(
                _diagnosticTestName,
                operation,
                milliseconds,
                DiagnosticSubsystemState);
        }

        private void BeginLayoutDiagnostic()
        {
            _layoutActive = true;
        }

        private void EndLayoutDiagnostic(long milliseconds)
        {
            _layoutActive = false;
            if (milliseconds > 25 ||
                _galleryMode == GalleryTestMode.NativeCards25 ||
                _galleryMode == GalleryTestMode.NativeCards100)
                RecordDuration(
                    "layout pass",
                    milliseconds);
        }

        private void PreviewPaintCompleted(long milliseconds)
        {
            if (milliseconds < 0)
            {
                _paintActive = true;
                return;
            }
            _paintActive = false;
            if (milliseconds > 25)
                RecordDuration(
                    "preview-related paint",
                    milliseconds);
        }

        private static string PreviewModeName(
            GalleryTestMode mode)
        {
            switch (mode)
            {
                case GalleryTestMode.CardsNoPreviews:
                case GalleryTestMode.NativeCards25:
                case GalleryTestMode.NativeCards100:
                    return "disabled";
                case GalleryTestMode.PreloadedPreviews:
                    return "preloaded-before-display";
                case GalleryTestMode.SinglePreviewApply:
                    return "background-single-apply";
                default:
                    return "progressive-batches";
            }
        }

        private static string RunningStateModeName(
            GalleryTestMode mode)
        {
            switch (mode)
            {
                case GalleryTestMode.PreviewsNoState:
                    return "disabled";
                case GalleryTestMode.StateSnapshot:
                case GalleryTestMode.NativeCards25:
                case GalleryTestMode.NativeCards100:
                    return "initial-snapshot-only";
                default:
                    return "initial-plus-repeating";
            }
        }

        private static int CardLimit(GalleryTestMode mode)
        {
            if (mode == GalleryTestMode.NativeCards25)
                return 25;
            if (mode == GalleryTestMode.NativeCards100)
                return 100;
            return int.MaxValue;
        }

        private sealed class DiagnosticFlowLayoutPanel :
            FlowLayoutPanel
        {
            private readonly Action _begin;
            private readonly Action<long> _complete;

            public DiagnosticFlowLayoutPanel(
                Action begin,
                Action<long> complete)
            {
                _begin = begin;
                _complete = complete;
            }

            protected override void OnLayout(
                LayoutEventArgs levent)
            {
                Stopwatch timer = Stopwatch.StartNew();
                if (_begin != null) _begin();
                try
                {
                    base.OnLayout(levent);
                }
                finally
                {
                    if (_complete != null)
                        _complete(timer.ElapsedMilliseconds);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _previewGeneration++;
                StopRegistryRefreshPolling();
                if (_statePollWorker != null &&
                    _statePollWorker.IsBusy)
                    _statePollWorker.CancelAsync();
                if (_previewWorker != null &&
                    _previewWorker.IsBusy)
                    _previewWorker.CancelAsync();
                foreach (Timer timer in
                    new List<Timer>(_activationTimers))
                {
                    timer.Stop();
                    timer.Dispose();
                }
                _activationTimers.Clear();
                if (_stateTimer != null)
                {
                    _stateTimer.Stop();
                    _stateTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
