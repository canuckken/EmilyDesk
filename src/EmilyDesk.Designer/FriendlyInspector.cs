using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace EmilyDesk.Designer
{
    internal sealed partial class DesignerForm
    {
        private readonly FlowLayoutPanel _friendlyFlow = new FlowLayoutPanel();
        private readonly Label _friendlySelection = new Label();
        private readonly GroupBox _friendlyLayerGroup = new GroupBox();
        private readonly GroupBox _friendlyPositionGroup = new GroupBox();
        private readonly GroupBox _friendlyAppearanceGroup = new GroupBox();
        private readonly GroupBox _friendlyRotationGroup = new GroupBox();
        private readonly GroupBox _friendlyHandGroup = new GroupBox();
        private readonly GroupBox _friendlyTextGroup = new GroupBox();
        private readonly GroupBox _friendlyImageGroup = new GroupBox();
        private readonly GroupBox _friendlyPanelGroup = new GroupBox();
        private readonly TextBox _friendlyName = new TextBox();
        private readonly CheckBox _friendlyVisible = new CheckBox();
        private readonly CheckBox _friendlyLocked = new CheckBox();
        private readonly NumericUpDown _friendlyX = NewNumber(-10000M, 10000M, 1M);
        private readonly NumericUpDown _friendlyY = NewNumber(-10000M, 10000M, 1M);
        private readonly NumericUpDown _friendlyWidth = NewNumber(1M, 10000M, 1M);
        private readonly NumericUpDown _friendlyHeight = NewNumber(1M, 10000M, 1M);
        private readonly Label _friendlyXLabel = new Label();
        private readonly Label _friendlyYLabel = new Label();
        private readonly Label _friendlyWidthLabel = new Label();
        private readonly Label _friendlyHeightLabel = new Label();
        private readonly TrackBar _friendlyOpacitySlider = new TrackBar();
        private readonly NumericUpDown _friendlyOpacity = NewNumber(0M, 100M, 1M);
        private readonly TrackBar _friendlyRotationSlider = new TrackBar();
        private readonly NumericUpDown _friendlyRotation = NewNumber(-359M, 359M, 1M);
        private readonly Label _friendlyRotationLabel = new Label();
        private readonly ComboBox _friendlyClockRole = NewDropDown();
        private readonly Label _friendlyClockRoleLabel = new Label();
        private readonly NumericUpDown _friendlyHandPivotX = NewNumber(0M, 100M, 1M);
        private readonly NumericUpDown _friendlyHandPivotY = NewNumber(0M, 100M, 1M);
        private readonly Button _friendlyCenterPivot = new Button();
        private readonly Button _friendlyEndPivot = new Button();
        private readonly Button _friendlyTailPivot = new Button();
        private readonly Button _friendlyAlignHand = new Button();
        private readonly Button _friendlyAlignAllHands = new Button();
        private readonly ComboBox _friendlyFont = NewDropDown();
        private readonly NumericUpDown _friendlyFontSize = NewNumber(4M, 300M, 1M);
        private readonly CheckBox _friendlyBold = new CheckBox();
        private readonly CheckBox _friendlyItalic = new CheckBox();
        private readonly CheckBox _friendlyWrap = new CheckBox();
        private readonly ComboBox _friendlyAlignment = NewDropDown();
        private readonly Button _friendlyColour = new Button();
        private readonly Button _friendlyWidgetFont = new Button();
        private readonly PictureBox _friendlyImagePreview = new PictureBox();
        private readonly Button _friendlyChangeImage = new Button();
        private readonly Button _friendlyThemeImage = new Button();
        private readonly Button _friendlyClearImage = new Button();
        private readonly Button _friendlyImageFolder = new Button();
        private readonly NumericUpDown _friendlyPanelX = NewNumber(-10000M, 10000M, 1M);
        private readonly NumericUpDown _friendlyPanelY = NewNumber(-10000M, 10000M, 1M);
        private readonly NumericUpDown _friendlyPanelOffset = NewNumber(-10000M, 10000M, 1M);
        private readonly NumericUpDown _friendlyPanelDuration = NewNumber(0M, 10000M, 10M);
        private readonly ComboBox _friendlyPanelDirection = NewDropDown();
        private readonly ComboBox _friendlyPanelState = NewDropDown();
        private readonly ComboBox _friendlyPanelAnimation = NewDropDown();
        private readonly Button _friendlyAdvancedButton = new Button();
        private readonly Panel _friendlyAdvancedPanel = new Panel();
        private bool _friendlyUpdating;
        private bool _friendlySliderActive;

        private static NumericUpDown NewNumber(decimal minimum,
            decimal maximum, decimal increment)
        {
            return new NumericUpDown
            {
                DecimalPlaces = 1,
                Minimum = minimum,
                Maximum = maximum,
                Increment = increment,
                Dock = DockStyle.Fill
            };
        }

        private static ComboBox NewDropDown()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill
            };
        }

        private void ConfigureFriendlyInspector()
        {
            _propertyInspector.Dock = DockStyle.Fill;
            _propertyInspector.Padding = new Padding(0);
            _propertyInspector.BackColor = SystemColors.Control;
            _friendlyFlow.Dock = DockStyle.Fill;
            _friendlyFlow.AutoScroll = true;
            _friendlyFlow.FlowDirection = FlowDirection.TopDown;
            _friendlyFlow.WrapContents = false;
            _friendlyFlow.Padding = new Padding(6);
            _friendlyFlow.SizeChanged += delegate { SizeFriendlyGroups(); };

            _friendlySelection.Height = 34;
            _friendlySelection.Padding = new Padding(7, 5, 4, 0);
            _friendlySelection.Font = new Font(Font, FontStyle.Bold);
            _friendlySelection.AutoEllipsis = true;
            _friendlyFlow.Controls.Add(_friendlySelection);

            ConfigureLayerGroup();
            ConfigurePositionGroup();
            ConfigureAppearanceGroup();
            ConfigureRotationGroup();
            ConfigureHandGroup();
            ConfigureTextGroup();
            ConfigureImageGroup();
            ConfigurePanelGroup();
            ConfigureAdvancedGroup();

            _propertyInspector.Controls.Add(_friendlyFlow);
            SizeFriendlyGroups();
            RefreshFriendlyInspector();
        }

        private void ConfigureLayerGroup()
        {
            _friendlyLayerGroup.Text = "Layer";
            _friendlyLayerGroup.Height = 110;
            TableLayoutPanel grid = NewFriendlyGrid(3);
            AddFriendlyRow(grid, "Name", _friendlyName, 0);
            ConfigureFriendlyCheckBox(_friendlyVisible,
                "Show this layer");
            ConfigureFriendlyCheckBox(_friendlyLocked,
                "Lock mouse movement");
            grid.Controls.Add(_friendlyVisible, 1, 1);
            grid.Controls.Add(_friendlyLocked, 1, 2);
            _friendlyLayerGroup.Controls.Add(grid);
            _friendlyFlow.Controls.Add(_friendlyLayerGroup);

            _friendlyName.Leave += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                if (selected == null || selected.Name == _friendlyName.Text) return;
                FriendlyCommit(delegate { selected.Name = _friendlyName.Text; });
            };
            _friendlyVisible.CheckedChanged += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                if (selected != null)
                    FriendlyCommit(delegate { selected.Visible = _friendlyVisible.Checked; });
            };
            _friendlyLocked.CheckedChanged += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                if (selected != null)
                    FriendlyCommit(delegate { selected.MouseLocked = _friendlyLocked.Checked; });
            };
        }

        private void ConfigurePositionGroup()
        {
            _friendlyPositionGroup.Text = "Position and size";
            _friendlyPositionGroup.Height = 142;
            TableLayoutPanel grid = NewFriendlyGrid(4);
            AddFriendlyRow(grid, _friendlyXLabel, _friendlyX, 0);
            AddFriendlyRow(grid, _friendlyYLabel, _friendlyY, 1);
            AddFriendlyRow(grid, _friendlyWidthLabel, _friendlyWidth, 2);
            AddFriendlyRow(grid, _friendlyHeightLabel, _friendlyHeight, 3);
            SetEqualFriendlyRows(grid, 4);
            CenterFriendlyField(_friendlyX);
            CenterFriendlyField(_friendlyY);
            CenterFriendlyField(_friendlyWidth);
            CenterFriendlyField(_friendlyHeight);
            _friendlyPositionGroup.Controls.Add(grid);
            _friendlyFlow.Controls.Add(_friendlyPositionGroup);
            _friendlyX.ValueChanged += delegate { ApplyFriendlyGeometry(0); };
            _friendlyY.ValueChanged += delegate { ApplyFriendlyGeometry(1); };
            _friendlyWidth.ValueChanged += delegate { ApplyFriendlyGeometry(2); };
            _friendlyHeight.ValueChanged += delegate { ApplyFriendlyGeometry(3); };
        }

        private void ConfigureAppearanceGroup()
        {
            _friendlyAppearanceGroup.Text = "Appearance";
            _friendlyAppearanceGroup.Height = 58;
            TableLayoutPanel grid = NewFriendlyGrid(1);
            var opacityPanel = NewSliderPanel(_friendlyOpacitySlider,
                _friendlyOpacity, 0, 100, 10);
            AddFriendlyRow(grid, "Opacity", opacityPanel, 0);
            _friendlyAppearanceGroup.Controls.Add(grid);
            _friendlyFlow.Controls.Add(_friendlyAppearanceGroup);

            ConfigureSliderEvents(_friendlyOpacitySlider);
            _friendlyOpacitySlider.ValueChanged += delegate
            {
                if (_friendlyUpdating) return;
                SetFriendlyNumber(_friendlyOpacity,
                    _friendlyOpacitySlider.Value);
                ApplyFriendlyOpacity();
            };
            _friendlyOpacity.ValueChanged += delegate
            {
                if (_friendlyUpdating) return;
                SetFriendlyTrack(_friendlyOpacitySlider,
                    Decimal.ToInt32(_friendlyOpacity.Value));
                ApplyFriendlyOpacity();
            };
        }

        private void ConfigureRotationGroup()
        {
            _friendlyRotationGroup.Text = "Clock hand rotation";
            _friendlyRotationGroup.Height = 92;
            TableLayoutPanel grid = NewFriendlyGrid(2);
            _friendlyRotationLabel.Text = "Rotation (- / +)";
            var rotationPanel = NewSliderPanel(_friendlyRotationSlider,
                _friendlyRotation, -359, 359, 30);
            AddFriendlyRow(grid, _friendlyRotationLabel,
                rotationPanel, 0);
            _friendlyClockRoleLabel.Text = "Connected as";
            AddFriendlyRow(grid, _friendlyClockRoleLabel,
                _friendlyClockRole, 1);
            _friendlyRotationGroup.Controls.Add(grid);
            _friendlyFlow.Controls.Add(_friendlyRotationGroup);

            ConfigureSliderEvents(_friendlyRotationSlider);
            _friendlyRotationSlider.ValueChanged += delegate
            {
                if (_friendlyUpdating) return;
                SetFriendlyNumber(_friendlyRotation,
                    _friendlyRotationSlider.Value);
                ApplyFriendlyRotation();
            };
            _friendlyRotation.ValueChanged += delegate
            {
                if (_friendlyUpdating) return;
                SetFriendlyTrack(_friendlyRotationSlider,
                    Decimal.ToInt32(_friendlyRotation.Value));
                ApplyFriendlyRotation();
            };
            _friendlyClockRole.Items.AddRange(new object[]
            {
                "Clock: Hour Hand", "Clock: Minute Hand",
                "Clock: Second Hand"
            });
            _friendlyClockRole.SelectedIndexChanged += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                if (selected == null || _friendlyClockRole.SelectedItem == null)
                    return;
                FriendlyCommit(delegate
                {
                    selected.Binding = _friendlyClockRole.SelectedItem.ToString();
                    NormalizeClockRoleFromProperties();
                });
            };
        }

        private void ConfigureHandGroup()
        {
            _friendlyHandGroup.Text = "Clock hand PNG pivot";
            _friendlyHandGroup.Height = 176;
            TableLayoutPanel grid = NewFriendlyGrid(5);
            AddFriendlyRow(grid, "Pivot across (%)",
                _friendlyHandPivotX, 0);
            AddFriendlyRow(grid, "Pivot down (%)",
                _friendlyHandPivotY, 1);
            var presets = NewFriendlyButtonRow();
            ConfigureInspectorButton(_friendlyCenterPivot,
                "Center (XWidget)");
            ConfigureInspectorButton(_friendlyEndPivot,
                "End of canvas");
            presets.Controls.Add(_friendlyCenterPivot, 0, 0);
            presets.Controls.Add(_friendlyEndPivot, 1, 0);
            grid.Controls.Add(presets, 1, 2);
            ConfigureInspectorButton(_friendlyTailPivot,
                "Legacy tail position");
            grid.Controls.Add(_friendlyTailPivot, 1, 3);
            var alignment = NewFriendlyButtonRow();
            ConfigureInspectorButton(_friendlyAlignHand, "Align this");
            ConfigureInspectorButton(_friendlyAlignAllHands, "Align all");
            alignment.Controls.Add(_friendlyAlignHand, 0, 0);
            alignment.Controls.Add(_friendlyAlignAllHands, 1, 0);
            grid.Controls.Add(alignment, 1, 4);
            _friendlyHandGroup.Controls.Add(grid);
            _friendlyFlow.Controls.Add(_friendlyHandGroup);

            _friendlyHandPivotX.ValueChanged += delegate
                { ApplyFriendlyHandPivot(); };
            _friendlyHandPivotY.ValueChanged += delegate
                { ApplyFriendlyHandPivot(); };
            _friendlyCenterPivot.Click += delegate
                { SetFriendlyHandPivot(.5F, .5F); };
            _friendlyEndPivot.Click += delegate
                { SetFriendlyHandPivot(.5F, 1F); };
            _friendlyTailPivot.Click += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                if (!IsClockHand(selected)) return;
                float height = Math.Max(1F,
                    selected.Height * selected.Scale);
                float tail = string.Equals(selected.Binding,
                    "Clock: Second Hand",
                    StringComparison.OrdinalIgnoreCase) ? 17F : 13F;
                SetFriendlyHandPivot(.5F,
                    Math.Max(0F, Math.Min(1F,
                        (height - tail) / height)));
            };
            _friendlyAlignHand.Click += delegate
                { AlignFriendlyClockHands(false); };
            _friendlyAlignAllHands.Click += delegate
                { AlignFriendlyClockHands(true); };
        }

        private static TableLayoutPanel NewFriendlyButtonRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            row.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent, 50F));
            row.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent, 50F));
            return row;
        }

        private void ConfigureTextGroup()
        {
            _friendlyTextGroup.Text = "Text";
            _friendlyTextGroup.Height = 230;
            TableLayoutPanel grid = NewFriendlyGrid(6);
            SetEqualFriendlyRows(grid, 6);
            AddFriendlyRow(grid, "Font", _friendlyFont, 0);
            AddFriendlyRow(grid, "Font size", _friendlyFontSize, 1);
            ConfigureInspectorButton(_friendlyColour,
                "Choose colour...");
            AddFriendlyRow(grid, "Colour", _friendlyColour, 2);
            var style = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 3, 0, 0),
                Margin = new Padding(0)
            };
            ConfigureFriendlyCheckBox(_friendlyBold, "Bold");
            ConfigureFriendlyCheckBox(_friendlyItalic, "Italic");
            ConfigureFriendlyCheckBox(_friendlyWrap, "Wrap text");
            style.Controls.Add(_friendlyBold);
            style.Controls.Add(_friendlyItalic);
            style.Controls.Add(_friendlyWrap);
            grid.Controls.Add(NewFriendlyLabel("Style"), 0, 3);
            grid.Controls.Add(style, 1, 3);
            AddFriendlyRow(grid, "Alignment", _friendlyAlignment, 4);
            ConfigureInspectorButton(_friendlyWidgetFont,
                "Select widget font...");
            AddFriendlyRow(grid, "Font file", _friendlyWidgetFont, 5);
            _friendlyTextGroup.Controls.Add(grid);
            _friendlyFlow.Controls.Add(_friendlyTextGroup);

            try
            {
                using (var fonts = new InstalledFontCollection())
                    foreach (FontFamily family in fonts.Families)
                        _friendlyFont.Items.Add(family.Name);
            }
            catch { }
            _friendlyAlignment.Items.AddRange(
                Enum.GetNames(typeof(DesignerTextAlignment)));
            _friendlyFont.SelectedIndexChanged += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                if (selected == null || _friendlyFont.SelectedItem == null) return;
                FriendlyCommit(delegate
                {
                    selected.FontName = _friendlyFont.SelectedItem.ToString();
                    selected.FontFile = string.Empty;
                });
            };
            _friendlyFontSize.ValueChanged += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                if (selected != null)
                    FriendlyCommit(delegate { selected.FontSize = (float)_friendlyFontSize.Value; });
            };
            _friendlyBold.CheckedChanged += delegate { ApplyFriendlyTextFlags(); };
            _friendlyItalic.CheckedChanged += delegate { ApplyFriendlyTextFlags(); };
            _friendlyWrap.CheckedChanged += delegate { ApplyFriendlyTextFlags(); };
            _friendlyAlignment.SelectedIndexChanged += delegate
            {
                DesignerElement selected = _canvas.SelectedElement;
                DesignerTextAlignment alignment;
                if (selected != null && Enum.TryParse(
                    _friendlyAlignment.Text, out alignment))
                    FriendlyCommit(delegate { selected.Alignment = alignment; });
            };
            _friendlyColour.Click += delegate { ChooseFriendlyColour(); };
            _friendlyWidgetFont.Click += delegate { ChooseWidgetFont(); };
        }

        private void ConfigureImageGroup()
        {
            _friendlyImageGroup.Text = "Image";
            _friendlyImageGroup.Height = 188;
            _friendlyImagePreview.Dock = DockStyle.Top;
            _friendlyImagePreview.Height = 112;
            _friendlyImagePreview.SizeMode = PictureBoxSizeMode.Zoom;
            _friendlyImagePreview.BackColor = Color.FromArgb(224, 224, 224);
            _friendlyImagePreview.BorderStyle = BorderStyle.FixedSingle;
            var buttons = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 42,
                ColumnCount = 4, RowCount = 1,
                Padding = new Padding(2, 4, 2, 2)
            };
            for (int i = 0; i < 4; i++)
                buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            ConfigureInspectorButton(_friendlyChangeImage, "Change...");
            ConfigureInspectorButton(_friendlyThemeImage, "Theme");
            ConfigureInspectorButton(_friendlyClearImage, "Clear");
            ConfigureInspectorButton(_friendlyImageFolder, "Folder");
            buttons.Controls.Add(_friendlyChangeImage, 0, 0);
            buttons.Controls.Add(_friendlyThemeImage, 1, 0);
            buttons.Controls.Add(_friendlyClearImage, 2, 0);
            buttons.Controls.Add(_friendlyImageFolder, 3, 0);
            _friendlyImageGroup.Controls.Add(_friendlyImagePreview);
            _friendlyImageGroup.Controls.Add(buttons);
            _friendlyFlow.Controls.Add(_friendlyImageGroup);
            _friendlyChangeImage.Click += delegate { ReplaceSelectedImage(); };
            _friendlyThemeImage.Click += delegate { RestoreSelectedThemeImage(); };
            _friendlyClearImage.Click += delegate { ClearSelectedImage(); };
            _friendlyImageFolder.Click += delegate { OpenSelectedImageFolder(); };
        }

        private void ConfigurePanelGroup()
        {
            _friendlyPanelGroup.Text = "Weather details panel";
            _friendlyPanelGroup.Height = 236;
            TableLayoutPanel grid = NewFriendlyGrid(7);
            AddFriendlyRow(grid, "Horizontal", _friendlyPanelX, 0);
            AddFriendlyRow(grid, "Vertical", _friendlyPanelY, 1);
            AddFriendlyRow(grid, "Opens toward", _friendlyPanelDirection, 2);
            AddFriendlyRow(grid, "Visible overlap", _friendlyPanelOffset, 3);
            AddFriendlyRow(grid, "Preview", _friendlyPanelState, 4);
            AddFriendlyRow(grid, "Animation", _friendlyPanelAnimation, 5);
            AddFriendlyRow(grid, "Duration (ms)", _friendlyPanelDuration, 6);
            _friendlyPanelGroup.Controls.Add(grid);
            _friendlyFlow.Controls.Add(_friendlyPanelGroup);
            _friendlyPanelDirection.Items.AddRange(
                Enum.GetNames(typeof(DesignerPanelDirection)));
            _friendlyPanelState.Items.AddRange(
                Enum.GetNames(typeof(DesignerPanelState)));
            _friendlyPanelAnimation.Items.AddRange(
                Enum.GetNames(typeof(DesignerPanelAnimation)));
            _friendlyPanelX.ValueChanged += delegate { ApplyFriendlyPanel(); };
            _friendlyPanelY.ValueChanged += delegate { ApplyFriendlyPanel(); };
            _friendlyPanelOffset.ValueChanged += delegate { ApplyFriendlyPanel(); };
            _friendlyPanelDuration.ValueChanged += delegate { ApplyFriendlyPanel(); };
            _friendlyPanelDirection.SelectedIndexChanged += delegate { ApplyFriendlyPanel(); };
            _friendlyPanelState.SelectedIndexChanged += delegate { ApplyFriendlyPanel(); };
            _friendlyPanelAnimation.SelectedIndexChanged += delegate { ApplyFriendlyPanel(); };
        }

        private void ConfigureAdvancedGroup()
        {
            _friendlyAdvancedButton.Text = "Show advanced settings";
            _friendlyAdvancedButton.Height = 30;
            _friendlyAdvancedButton.Click += delegate
            {
                _friendlyAdvancedPanel.Visible = !_friendlyAdvancedPanel.Visible;
                _friendlyAdvancedButton.Text = _friendlyAdvancedPanel.Visible
                    ? "Hide advanced settings" : "Show advanced settings";
            };
            _friendlyAdvancedPanel.Height = 300;
            _friendlyAdvancedPanel.Visible = false;
            _properties.Dock = DockStyle.Fill;
            _friendlyAdvancedPanel.Controls.Add(_properties);
            _friendlyFlow.Controls.Add(_friendlyAdvancedButton);
            _friendlyFlow.Controls.Add(_friendlyAdvancedPanel);
        }

        private static TableLayoutPanel NewFriendlyGrid(int rows)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(7, 5, 7, 5),
                ColumnCount = 2,
                RowCount = rows
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            return grid;
        }

        private static Label NewFriendlyLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
        }

        private static void ConfigureFriendlyCheckBox(CheckBox checkBox,
            string text)
        {
            checkBox.Text = text;
            checkBox.AutoSize = true;
            checkBox.Dock = DockStyle.None;
            checkBox.Anchor = AnchorStyles.Left;
            checkBox.Margin = new Padding(2, 2, 2, 2);
        }

        private static void SetEqualFriendlyRows(TableLayoutPanel grid,
            int rows)
        {
            grid.RowStyles.Clear();
            for (int row = 0; row < rows; row++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent,
                    100F / rows));
        }

        private static void CenterFriendlyField(Control control)
        {
            control.Dock = DockStyle.None;
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(2, 3, 2, 3);
        }

        private static void AddFriendlyRow(TableLayoutPanel grid,
            string text, Control control, int row)
        {
            AddFriendlyRow(grid, NewFriendlyLabel(text), control, row);
        }

        private static void AddFriendlyRow(TableLayoutPanel grid,
            Label label, Control control, int row)
        {
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(2);
            grid.Controls.Add(label, 0, row);
            grid.Controls.Add(control, 1, row);
        }

        private static Panel NewSliderPanel(TrackBar slider,
            NumericUpDown number, int minimum, int maximum,
            int tickFrequency)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            slider.Minimum = minimum;
            slider.Maximum = maximum;
            slider.TickFrequency = tickFrequency;
            slider.SmallChange = 1;
            slider.LargeChange = tickFrequency;
            slider.TickStyle = TickStyle.None;
            slider.Dock = DockStyle.Fill;
            number.Dock = DockStyle.Right;
            number.Width = 65;
            panel.Controls.Add(slider);
            panel.Controls.Add(number);
            return panel;
        }

        private void ConfigureSliderEvents(TrackBar slider)
        {
            slider.MouseDown += delegate
            {
                if (_friendlyUpdating || _layout == null) return;
                CaptureUndo();
                _friendlySliderActive = true;
            };
            slider.MouseUp += delegate
            {
                _friendlySliderActive = false;
                _propertyBaseline = _layout == null ? null : _layout.Clone();
            };
        }

        private void SizeFriendlyGroups()
        {
            int width = Math.Max(260, _friendlyFlow.ClientSize.Width - 18);
            foreach (Control control in _friendlyFlow.Controls)
                control.Width = width;
        }

        private void RefreshFriendlyInspector()
        {
            if (_friendlyFlow.Parent == null) return;
            _friendlyUpdating = true;
            try
            {
                DesignerElement selected = _canvas.SelectedElement;
                bool panel = selected == null && _layout != null &&
                    _widgetKind != "recyclebin" &&
                    _canvas.ActiveSurface == DesignerSurface.WeatherDetails;
                _friendlySelection.Text = selected == null
                    ? (panel ? "Weather details panel" : "Select a layer to edit")
                    : !string.IsNullOrWhiteSpace(selected.MoveGroup) &&
                        selected.MoveWithGroup
                        ? "Calendar Grid  (linked text group)"
                        : selected.Name + "  (" + FriendlyKind(selected) + ")";
                _friendlyLayerGroup.Visible = selected != null;
                _friendlyPositionGroup.Visible = selected != null;
                _friendlyAppearanceGroup.Visible = selected != null;
                _friendlyRotationGroup.Visible = IsClockHand(selected);
                _friendlyHandGroup.Visible = IsClockHand(selected) &&
                    selected.Kind == DesignerElementKind.Image;
                _friendlyTextGroup.Visible = selected != null &&
                    selected.Kind == DesignerElementKind.Text;
                _friendlyImageGroup.Visible = selected != null &&
                    selected.Kind == DesignerElementKind.Image;
                _friendlyPanelGroup.Visible = panel;
                _friendlyAdvancedButton.Visible = selected != null || panel;
                _friendlyAdvancedPanel.Visible = _friendlyAdvancedPanel.Visible &&
                    (selected != null || panel);
                if (selected != null) RefreshFriendlyElement(selected);
                if (panel) RefreshFriendlyPanel();
            }
            finally { _friendlyUpdating = false; }
            SizeFriendlyGroups();
        }

        private void RefreshFriendlyElement(DesignerElement selected)
        {
            bool hand = IsClockHand(selected);
            bool centre = IsClockCentre(selected);
            bool imagePivot = hand && selected.HandPivotX.HasValue &&
                selected.HandPivotY.HasValue;
            _friendlyName.Text = selected.Name ?? string.Empty;
            _friendlyVisible.Checked = selected.Visible;
            _friendlyLocked.Checked = selected.MouseLocked;
            _friendlyXLabel.Text = centre ? "Centre X" : "X";
            _friendlyYLabel.Text = centre ? "Centre Y" : "Y";
            _friendlyWidthLabel.Text = centre ? "Dot size" : "Width";
            _friendlyHeightLabel.Text = centre ? "Dot size" : "Height";
            SetFriendlyNumber(_friendlyX, centre
                ? selected.X + selected.Width * selected.Scale / 2F
                : selected.X);
            SetFriendlyNumber(_friendlyY, centre
                ? selected.Y + selected.Height * selected.Scale / 2F
                : selected.Y);
            SetFriendlyNumber(_friendlyWidth, centre
                ? selected.Width * selected.Scale : selected.Width);
            SetFriendlyNumber(_friendlyHeight, centre
                ? selected.Height * selected.Scale : selected.Height);
            if (hand)
            {
                float tail = string.Equals(selected.Binding,
                    "Clock: Second Hand",
                    StringComparison.OrdinalIgnoreCase) ? 17F : 13F;
                float legacyY = (selected.Height * selected.Scale - tail) /
                    Math.Max(1F, selected.Height * selected.Scale);
                SetFriendlyNumber(_friendlyHandPivotX,
                    100F * (imagePivot
                        ? selected.HandPivotX.Value : .5F));
                SetFriendlyNumber(_friendlyHandPivotY,
                    100F * (imagePivot
                        ? selected.HandPivotY.Value : legacyY));
            }
            int opacity = (int)Math.Round(Math.Max(0F,
                Math.Min(1F, selected.Opacity)) * 100F);
            SetFriendlyTrack(_friendlyOpacitySlider, opacity);
            SetFriendlyNumber(_friendlyOpacity, opacity);
            int rotation = (int)Math.Round(NormalizeSignedRotation(
                selected.PreviewRotation));
            SetFriendlyTrack(_friendlyRotationSlider, rotation);
            SetFriendlyNumber(_friendlyRotation, rotation);
            if (hand) _friendlyClockRole.SelectedItem = selected.Binding;

            if (selected.Kind == DesignerElementKind.Text)
            {
                string font = string.IsNullOrWhiteSpace(selected.FontName)
                    ? "Segoe UI" : selected.FontName;
                if (!_friendlyFont.Items.Contains(font))
                    _friendlyFont.Items.Add(font);
                _friendlyFont.SelectedItem = font;
                SetFriendlyNumber(_friendlyFontSize, selected.FontSize);
                _friendlyBold.Checked = selected.Bold;
                _friendlyItalic.Checked = selected.Italic;
                _friendlyWrap.Checked = selected.WordWrap;
                _friendlyAlignment.SelectedItem = selected.Alignment.ToString();
                Color selectedColour = Color.FromArgb(selected.ColorArgb);
                _friendlyColour.BackColor = Color.FromArgb(selectedColour.R,
                    selectedColour.G, selectedColour.B);
                _friendlyColour.ForeColor =
                    XWidgetReborn.WidgetSdk.EmilyDeskThemeCatalog.BestTextOn(
                        _friendlyColour.BackColor);
            }
            RefreshFriendlyImage(selected);
        }

        private void RefreshFriendlyImage(DesignerElement selected)
        {
            Image old = _friendlyImagePreview.Image;
            _friendlyImagePreview.Image = null;
            if (old != null) old.Dispose();
            if (selected.Kind != DesignerElementKind.Image) return;
            _friendlyThemeImage.Enabled = !string.IsNullOrWhiteSpace(selected.Binding);
            string path = ResolveLayoutAsset(selected.ImagePath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            try
            {
                Image preview = _canvas.GetPreviewImage(path);
                if (preview != null) _friendlyImagePreview.Image = new Bitmap(preview);
            }
            catch { }
        }

        private void RefreshFriendlyPanel()
        {
            DesignerPanelSettings panel = _layout.WeatherDetailsPanel;
            if (panel == null) return;
            SetFriendlyNumber(_friendlyPanelX, panel.PositionX);
            SetFriendlyNumber(_friendlyPanelY, panel.PositionY);
            SetFriendlyNumber(_friendlyPanelOffset, panel.Offset);
            SetFriendlyNumber(_friendlyPanelDuration,
                panel.DurationMilliseconds);
            _friendlyPanelDirection.SelectedItem = panel.Direction.ToString();
            _friendlyPanelState.SelectedItem = panel.State.ToString();
            _friendlyPanelAnimation.SelectedItem = panel.Animation.ToString();
        }

        private void ApplyFriendlyGeometry(int property)
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            FriendlyCommit(delegate
            {
                if (IsClockHand(selected))
                {
                    if (property == 0)
                        selected.X = (float)_friendlyX.Value;
                    else if (property == 1)
                        selected.Y = (float)_friendlyY.Value;
                    else
                    {
                        PointF pivot = new PointF(
                            selected.PivotX, selected.PivotY);
                        if (property == 2)
                            selected.Width = (float)_friendlyWidth.Value;
                        else
                            selected.Height = (float)_friendlyHeight.Value;
                        selected.PivotX = pivot.X;
                        selected.PivotY = pivot.Y;
                    }
                }
                else if (IsClockCentre(selected))
                {
                    if (property == 0 || property == 1)
                        MoveClockCentreTo(new PointF(
                            (float)_friendlyX.Value,
                            (float)_friendlyY.Value));
                    else
                    {
                        float size = property == 2
                            ? (float)_friendlyWidth.Value
                            : (float)_friendlyHeight.Value;
                        PointF centre = ClockCentre();
                        selected.Width = size / Math.Max(.1F, selected.Scale);
                        selected.Height = selected.Width;
                        MoveClockCentreTo(centre);
                    }
                }
                else if (property == 0) selected.X = (float)_friendlyX.Value;
                else if (property == 1) selected.Y = (float)_friendlyY.Value;
                else if (property == 2) selected.Width = (float)_friendlyWidth.Value;
                else selected.Height = (float)_friendlyHeight.Value;
            });
        }

        private void ApplyFriendlyHandPivot()
        {
            if (_friendlyUpdating) return;
            DesignerElement selected = _canvas.SelectedElement;
            if (!IsClockHand(selected)) return;
            float x = (float)_friendlyHandPivotX.Value / 100F;
            float y = (float)_friendlyHandPivotY.Value / 100F;
            SetFriendlyHandPivot(x, y);
        }

        private void SetFriendlyHandPivot(float x, float y)
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (!IsClockHand(selected)) return;
            FriendlyCommit(delegate
            {
                PointF pivot = new PointF(
                    selected.PivotX, selected.PivotY);
                selected.HandPivotX = Math.Max(0F, Math.Min(1F, x));
                selected.HandPivotY = Math.Max(0F, Math.Min(1F, y));
                selected.PivotX = pivot.X;
                selected.PivotY = pivot.Y;
                _layout.ClockHandEditVersion = 1;
            });
            RefreshFriendlyInspector();
        }

        private void AlignFriendlyClockHands(bool all)
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (!IsClockHand(selected)) return;
            FriendlyCommit(delegate
            {
                PointF centre = ClockCentre();
                if (all)
                    foreach (DesignerElement element in _layout.Elements)
                        if (IsClockHand(element))
                        {
                            element.PivotX = centre.X;
                            element.PivotY = centre.Y;
                        }
                if (!all)
                {
                    selected.PivotX = centre.X;
                    selected.PivotY = centre.Y;
                }
            });
            RefreshFriendlyInspector();
        }

        private void ApplyFriendlyOpacity()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            FriendlySliderCommit(delegate
            {
                selected.Opacity = (float)_friendlyOpacity.Value / 100F;
            });
        }

        private void ApplyFriendlyRotation()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (!IsClockHand(selected)) return;
            FriendlySliderCommit(delegate
            {
                selected.PreviewRotation = (float)_friendlyRotation.Value;
                _layout.ClockHandEditVersion = 1;
            });
        }

        private void ApplyFriendlyTextFlags()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Text)
                return;
            FriendlyCommit(delegate
            {
                selected.Bold = _friendlyBold.Checked;
                selected.Italic = _friendlyItalic.Checked;
                selected.WordWrap = _friendlyWrap.Checked;
            });
        }

        private void ChooseFriendlyColour()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            using (var dialog = new VisualColorPickerDialog(
                Color.FromArgb(selected.ColorArgb)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                FriendlyCommit(delegate
                {
                    selected.ColorArgb = dialog.SelectedColor.ToArgb();
                });
                RefreshFriendlyInspector();
            }
        }

        private void ApplyFriendlyPanel()
        {
            if (_layout == null || _layout.WeatherDetailsPanel == null) return;
            DesignerPanelDirection direction;
            DesignerPanelState state;
            DesignerPanelAnimation animation;
            if (!Enum.TryParse(_friendlyPanelDirection.Text, out direction) ||
                !Enum.TryParse(_friendlyPanelState.Text, out state) ||
                !Enum.TryParse(_friendlyPanelAnimation.Text, out animation))
                return;
            DesignerPanelSettings panel = _layout.WeatherDetailsPanel;
            FriendlyCommit(delegate
            {
                panel.PositionX = (float)_friendlyPanelX.Value;
                panel.PositionY = (float)_friendlyPanelY.Value;
                panel.Offset = (float)_friendlyPanelOffset.Value;
                panel.DurationMilliseconds = Decimal.ToInt32(
                    _friendlyPanelDuration.Value);
                panel.Direction = direction;
                panel.State = state;
                panel.Animation = animation;
            });
        }

        private void FriendlyCommit(Action change)
        {
            if (_friendlyUpdating || _layout == null || change == null) return;
            CaptureUndo();
            change();
            _canvas.Invalidate();
            _properties.Refresh();
            RefreshLayers();
            _propertyBaseline = _layout.Clone();
        }

        private void FriendlySliderCommit(Action change)
        {
            if (_friendlyUpdating || _layout == null || change == null) return;
            if (!_friendlySliderActive) CaptureUndo();
            change();
            _canvas.Invalidate();
            _properties.Refresh();
            _propertyBaseline = _layout.Clone();
        }

        private void SetFriendlyNumber(NumericUpDown control, float value)
        {
            decimal bounded = Math.Max(control.Minimum,
                Math.Min(control.Maximum, (decimal)value));
            control.Value = bounded;
        }

        private void SetFriendlyTrack(TrackBar control, int value)
        {
            bool previous = _friendlyUpdating;
            _friendlyUpdating = true;
            control.Value = Math.Max(control.Minimum,
                Math.Min(control.Maximum, value));
            _friendlyUpdating = previous;
        }

        private void SetFriendlyNumber(NumericUpDown control, int value)
        {
            bool previous = _friendlyUpdating;
            _friendlyUpdating = true;
            control.Value = Math.Max(control.Minimum,
                Math.Min(control.Maximum, value));
            _friendlyUpdating = previous;
        }

        private static float NormalizeSignedRotation(float rotation)
        {
            rotation %= 360F;
            return rotation;
        }

        private static string FriendlyKind(DesignerElement element)
        {
            if (IsClockHand(element)) return "Clock hand";
            if (IsClockCentre(element)) return "Clock centre";
            return element.Kind == DesignerElementKind.Image ? "Image" :
                element.Kind == DesignerElementKind.Text ? "Text" : "Divider";
        }

        private void ConfigureFriendlyLayerList()
        {
            _layers.DrawMode = DrawMode.OwnerDrawFixed;
            _layers.ItemHeight = 25;
            _layers.DrawItem += DrawFriendlyLayer;
            _layers.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || e.X >= 49) return;
                int index = _layers.IndexFromPoint(e.Location);
                if (index < 0) return;
                _layers.SelectedIndex = index;
                DesignerElement element = _layers.Items[index] as DesignerElement;
                if (element == null) return;
                CaptureUndo();
                if (e.X < 25) element.Visible = !element.Visible;
                else element.MouseLocked = !element.MouseLocked;
                _canvas.SelectedElement = element;
                _layers.Invalidate();
                _canvas.Invalidate();
                UpdatePropertyInspector();
            };
        }

        private void DrawFriendlyLayer(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= _layers.Items.Count) return;
            DesignerElement element = _layers.Items[e.Index] as DesignerElement;
            if (element == null) return;
            Color colour = (e.State & DrawItemState.Selected) != 0
                ? SystemColors.HighlightText : SystemColors.ControlText;
            using (var pen = new Pen(colour, 1.4F))
            using (var brush = new SolidBrush(colour))
            {
                Rectangle eye = new Rectangle(e.Bounds.Left + 6,
                    e.Bounds.Top + 8, 13, 8);
                e.Graphics.DrawEllipse(pen, eye);
                if (element.Visible)
                    e.Graphics.FillEllipse(brush, eye.Left + 5,
                        eye.Top + 2, 4, 4);
                else
                    e.Graphics.DrawLine(pen, eye.Left,
                        eye.Bottom, eye.Right, eye.Top);
                Rectangle body = new Rectangle(e.Bounds.Left + 31,
                    e.Bounds.Top + 10, 10, 8);
                e.Graphics.DrawRectangle(pen, body);
                if (element.MouseLocked)
                    e.Graphics.DrawArc(pen, body.Left + 2,
                        e.Bounds.Top + 4, 6, 9, 180, -180);
                else
                    e.Graphics.DrawArc(pen, body.Left + 5,
                        e.Bounds.Top + 4, 6, 9, 180, -150);
                e.Graphics.DrawString(element.Name ?? "Layer", e.Font,
                    brush, e.Bounds.Left + 52, e.Bounds.Top + 5);
            }
            e.DrawFocusRectangle();
        }
    }
}
