using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmilyDesk.Designer
{
    /// <summary>
    /// A visual HSV/RGBA colour picker for Designer text layers.  The large
    /// field makes lightening and darkening a colour possible without first
    /// knowing its numeric value, while the numeric controls preserve precise
    /// editing for existing layouts.
    /// </summary>
    internal sealed class VisualColorPickerDialog : Form
    {
        private readonly Color _originalColor;
        private readonly SaturationValueControl _colourField;
        private readonly HueControl _hueStrip;
        private readonly NumericUpDown[] _channelNumbers =
            new NumericUpDown[4];
        private readonly TrackBar[] _channelSliders = new TrackBar[4];
        private readonly TextBox _hexValue = new TextBox();
        private readonly ColorComparisonControl _comparison;
        private readonly Label _validation = new Label();
        private readonly ToolTip _toolTip = new ToolTip();
        private float _hue;
        private float _saturation;
        private float _value;
        private int _alpha;
        private bool _updating;

        public VisualColorPickerDialog(Color initialColor)
        {
            _originalColor = initialColor;
            ColorPickerMath.RgbToHsv(initialColor, out _hue,
                out _saturation, out _value);
            _alpha = initialColor.A;

            Text = "Select text colour";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(714, 516);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular,
                GraphicsUnit.Point);

            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold,
                    GraphicsUnit.Point),
                Location = new Point(18, 14),
                Text = "Choose a text colour"
            };
            Controls.Add(title);

            _colourField = new SaturationValueControl
            {
                Location = new Point(18, 43),
                Size = new Size(342, 292),
                Hue = _hue,
                Saturation = _saturation,
                Value = _value,
                AccessibleName = "Colour shade and saturation"
            };
            _colourField.ColorPositionChanged += delegate
            {
                if (_updating) return;
                _saturation = _colourField.Saturation;
                _value = _colourField.Value;
                UpdateFromHsv();
            };
            Controls.Add(_colourField);

            _hueStrip = new HueControl
            {
                Location = new Point(370, 43),
                Size = new Size(30, 292),
                Hue = _hue,
                AccessibleName = "Colour hue"
            };
            _hueStrip.HueChanged += delegate
            {
                if (_updating) return;
                _hue = _hueStrip.Hue;
                _colourField.Hue = _hue;
                UpdateFromHsv();
            };
            Controls.Add(_hueStrip);

            var instruction = new Label
            {
                Location = new Point(18, 340),
                Size = new Size(382, 34),
                ForeColor = SystemColors.GrayText,
                Text = "Drag up for lighter, down for darker. " +
                    "Move left or right to change colour strength."
            };
            Controls.Add(instruction);

            string[] names = { "Red", "Green", "Blue", "Alpha" };
            for (int index = 0; index < names.Length; index++)
                AddChannelRow(names[index], index, 43 + index * 53);

            var exactLabel = new Label
            {
                AutoSize = true,
                Location = new Point(420, 260),
                Text = "Exact value"
            };
            Controls.Add(exactLabel);
            _hexValue.Location = new Point(420, 280);
            _hexValue.Size = new Size(270, 24);
            _hexValue.AccessibleName = "Exact colour value";
            _hexValue.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                ApplyExactValue();
                e.Handled = true;
                e.SuppressKeyPress = true;
            };
            _hexValue.Leave += delegate { ApplyExactValue(); };
            Controls.Add(_hexValue);

            _validation.Location = new Point(420, 307);
            _validation.Size = new Size(270, 28);
            _validation.ForeColor = Color.Firebrick;
            Controls.Add(_validation);

            var originalLabel = new Label
            {
                AutoSize = true,
                Location = new Point(420, 340),
                Text = "Original"
            };
            var selectedLabel = new Label
            {
                AutoSize = true,
                Location = new Point(555, 340),
                Text = "Selected"
            };
            Controls.Add(originalLabel);
            Controls.Add(selectedLabel);
            _comparison = new ColorComparisonControl
            {
                Location = new Point(420, 360),
                Size = new Size(270, 45),
                OriginalColor = _originalColor,
                SelectedColor = SelectedColor,
                AccessibleName = "Original and selected colour preview"
            };
            Controls.Add(_comparison);

            AddSwatches();

            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(88, 30),
                Location = new Point(602, 472)
            };
            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(88, 30),
                Location = new Point(504, 472)
            };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            UpdateControls();
        }

        public Color SelectedColor
        {
            get
            {
                Color rgb = ColorPickerMath.FromHsv(
                    _hue, _saturation, _value);
                return Color.FromArgb(_alpha, rgb.R, rgb.G, rgb.B);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                Color parsed;
                if (!ColorPickerMath.TryParseExactValue(
                    _hexValue.Text, out parsed))
                {
                    _validation.Text =
                        "Use #AARRGGBB or R, G, B, A.";
                    _hexValue.Focus();
                    e.Cancel = true;
                    return;
                }
                SetSelectedColor(parsed);
            }
            base.OnFormClosing(e);
        }

        private void AddChannelRow(string name, int channel, int top)
        {
            var label = new Label
            {
                AutoSize = true,
                Location = new Point(420, top + 5),
                Text = name
            };
            var number = new NumericUpDown
            {
                Location = new Point(464, top),
                Size = new Size(64, 24),
                Minimum = 0,
                Maximum = 255,
                AccessibleName = name + " value"
            };
            var slider = new TrackBar
            {
                Location = new Point(536, top - 4),
                Size = new Size(154, 34),
                Minimum = 0,
                Maximum = 255,
                TickStyle = TickStyle.None,
                SmallChange = 1,
                LargeChange = 10,
                AccessibleName = name + " slider"
            };
            int selectedChannel = channel;
            number.ValueChanged += delegate
            {
                if (_updating) return;
                ApplyChannel(selectedChannel, Decimal.ToInt32(number.Value));
            };
            slider.ValueChanged += delegate
            {
                if (_updating) return;
                ApplyChannel(selectedChannel, slider.Value);
            };
            _channelNumbers[channel] = number;
            _channelSliders[channel] = slider;
            Controls.Add(label);
            Controls.Add(number);
            Controls.Add(slider);
        }

        private void AddSwatches()
        {
            var label = new Label
            {
                AutoSize = true,
                Location = new Point(18, 385),
                Text = "Quick colours"
            };
            Controls.Add(label);
            Color[] colours =
            {
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(232, 232, 232),
                Color.FromArgb(190, 190, 190),
                Color.FromArgb(120, 120, 120),
                Color.FromArgb(54, 54, 54),
                Color.FromArgb(0, 0, 0),
                Color.FromArgb(247, 226, 190),
                Color.FromArgb(224, 157, 39),
                Color.FromArgb(200, 157, 103),
                Color.FromArgb(151, 86, 42),
                Color.FromArgb(104, 70, 31),
                Color.FromArgb(73, 38, 18),
                Color.FromArgb(238, 142, 38),
                Color.FromArgb(210, 126, 139),
                Color.FromArgb(99, 117, 91),
                Color.FromArgb(37, 63, 51),
                Color.FromArgb(45, 184, 174),
                Color.FromArgb(45, 163, 255),
                Color.FromArgb(200, 45, 45),
                Color.FromArgb(45, 160, 75)
            };
            for (int index = 0; index < colours.Length; index++)
            {
                Color colour = colours[index];
                var swatch = new Button
                {
                    BackColor = colour,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(31, 25),
                    Location = new Point(18 + (index % 10) * 35,
                        408 + (index / 10) * 31),
                    TabStop = true,
                    AccessibleName = "Set colour " +
                        ColorPickerMath.ToExactValue(colour)
                };
                swatch.FlatAppearance.BorderColor = Color.FromArgb(92, 92, 92);
                swatch.FlatAppearance.BorderSize = 1;
                swatch.Click += delegate { SetSelectedColor(colour); };
                _toolTip.SetToolTip(swatch,
                    ColorPickerMath.ToExactValue(colour));
                Controls.Add(swatch);
            }
        }

        private void ApplyChannel(int channel, int value)
        {
            Color current = SelectedColor;
            int red = current.R;
            int green = current.G;
            int blue = current.B;
            int alpha = current.A;
            if (channel == 0) red = value;
            else if (channel == 1) green = value;
            else if (channel == 2) blue = value;
            else alpha = value;
            SetSelectedColor(Color.FromArgb(alpha, red, green, blue));
        }

        private void ApplyExactValue()
        {
            if (_updating) return;
            Color parsed;
            if (!ColorPickerMath.TryParseExactValue(
                _hexValue.Text, out parsed))
            {
                _validation.Text = "Use #AARRGGBB or R, G, B, A.";
                return;
            }
            _validation.Text = string.Empty;
            SetSelectedColor(parsed);
        }

        private void SetSelectedColor(Color colour)
        {
            ColorPickerMath.RgbToHsv(colour, out _hue,
                out _saturation, out _value);
            _alpha = colour.A;
            UpdateControls();
        }

        private void UpdateFromHsv()
        {
            UpdateControls();
        }

        private void UpdateControls()
        {
            _updating = true;
            try
            {
                _colourField.Hue = _hue;
                _colourField.Saturation = _saturation;
                _colourField.Value = _value;
                _hueStrip.Hue = _hue;
                Color selected = SelectedColor;
                int[] channels =
                    { selected.R, selected.G, selected.B, selected.A };
                for (int index = 0; index < channels.Length; index++)
                {
                    _channelNumbers[index].Value = channels[index];
                    _channelSliders[index].Value = channels[index];
                }
                _hexValue.Text = ColorPickerMath.ToExactValue(selected);
                _validation.Text = string.Empty;
                _comparison.SelectedColor = selected;
                _comparison.Invalidate();
            }
            finally
            {
                _updating = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class ColorPickerMath
    {
        public static Color FromHsv(float hue, float saturation, float value)
        {
            hue = NormalizeHue(hue);
            saturation = Clamp(saturation);
            value = Clamp(value);
            if (saturation <= 0F)
            {
                int grey = ToByte(value * 255F);
                return Color.FromArgb(grey, grey, grey);
            }

            float sector = hue / 60F;
            int whole = (int)Math.Floor(sector);
            float fraction = sector - whole;
            float p = value * (1F - saturation);
            float q = value * (1F - saturation * fraction);
            float t = value * (1F - saturation * (1F - fraction));
            float red;
            float green;
            float blue;
            switch (whole)
            {
                case 0: red = value; green = t; blue = p; break;
                case 1: red = q; green = value; blue = p; break;
                case 2: red = p; green = value; blue = t; break;
                case 3: red = p; green = q; blue = value; break;
                case 4: red = t; green = p; blue = value; break;
                default: red = value; green = p; blue = q; break;
            }
            return Color.FromArgb(ToByte(red * 255F),
                ToByte(green * 255F), ToByte(blue * 255F));
        }

        public static void RgbToHsv(Color colour, out float hue,
            out float saturation, out float value)
        {
            float red = colour.R / 255F;
            float green = colour.G / 255F;
            float blue = colour.B / 255F;
            float maximum = Math.Max(red, Math.Max(green, blue));
            float minimum = Math.Min(red, Math.Min(green, blue));
            float delta = maximum - minimum;
            value = maximum;
            saturation = maximum <= 0F ? 0F : delta / maximum;
            if (delta <= 0F)
            {
                hue = 0F;
                return;
            }
            if (maximum == red)
                hue = 60F * (((green - blue) / delta) % 6F);
            else if (maximum == green)
                hue = 60F * (((blue - red) / delta) + 2F);
            else
                hue = 60F * (((red - green) / delta) + 4F);
            hue = NormalizeHue(hue);
        }

        public static string ToExactValue(Color colour)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "#{0:X2}{1:X2}{2:X2}{3:X2}", colour.A,
                colour.R, colour.G, colour.B);
        }

        public static bool TryParseExactValue(string text, out Color colour)
        {
            colour = Color.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string value = text.Trim();
            if (value.StartsWith("#", StringComparison.Ordinal))
                value = value.Substring(1);
            uint packed;
            if (value.Length == 6 && uint.TryParse(value,
                NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out packed))
            {
                colour = Color.FromArgb(255, (int)((packed >> 16) & 255),
                    (int)((packed >> 8) & 255), (int)(packed & 255));
                return true;
            }
            if (value.Length == 8 && uint.TryParse(value,
                NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out packed))
            {
                colour = Color.FromArgb((int)((packed >> 24) & 255),
                    (int)((packed >> 16) & 255),
                    (int)((packed >> 8) & 255), (int)(packed & 255));
                return true;
            }

            string[] parts = text.Split(',');
            if (parts.Length != 3 && parts.Length != 4) return false;
            int[] channels = new int[4];
            channels[3] = 255;
            for (int index = 0; index < parts.Length; index++)
                if (!int.TryParse(parts[index].Trim(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out channels[index]) || channels[index] < 0 ||
                    channels[index] > 255)
                    return false;
            colour = Color.FromArgb(channels[3], channels[0],
                channels[1], channels[2]);
            return true;
        }

        private static int ToByte(float value)
        {
            return Math.Max(0, Math.Min(255,
                (int)Math.Round(value)));
        }

        private static float NormalizeHue(float hue)
        {
            hue %= 360F;
            return hue < 0F ? hue + 360F : hue;
        }

        private static float Clamp(float value)
        {
            return Math.Max(0F, Math.Min(1F, value));
        }
    }

    internal sealed class SaturationValueControl : Control
    {
        private Bitmap _gradient;
        private float _hue;
        private float _saturation;
        private float _value;
        private bool _dragging;

        public SaturationValueControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = true;
        }

        public event EventHandler ColorPositionChanged;

        public float Hue
        {
            get { return _hue; }
            set
            {
                float normalized = value % 360F;
                if (normalized < 0F) normalized += 360F;
                if (Math.Abs(_hue - normalized) < .01F) return;
                _hue = normalized;
                DisposeGradient();
                Invalidate();
            }
        }

        public float Saturation
        {
            get { return _saturation; }
            set
            {
                _saturation = Math.Max(0F, Math.Min(1F, value));
                Invalidate();
            }
        }

        public float Value
        {
            get { return _value; }
            set
            {
                _value = Math.Max(0F, Math.Min(1F, value));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            EnsureGradient();
            if (_gradient != null)
                e.Graphics.DrawImageUnscaled(_gradient, 0, 0);
            int x = (int)Math.Round(_saturation *
                Math.Max(0, ClientSize.Width - 1));
            int y = (int)Math.Round((1F - _value) *
                Math.Max(0, ClientSize.Height - 1));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var dark = new Pen(Color.FromArgb(210, Color.Black), 3F))
            using (var light = new Pen(Color.FromArgb(235, Color.White), 1F))
            {
                e.Graphics.DrawEllipse(dark, x - 6, y - 6, 12, 12);
                e.Graphics.DrawEllipse(light, x - 6, y - 6, 12, 12);
            }
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                SystemColors.ControlDark, ButtonBorderStyle.Solid);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            _dragging = true;
            Capture = true;
            SetFromPoint(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) SetFromPoint(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = false;
            Capture = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            float step = e.Shift ? .05F : .01F;
            if (e.KeyCode == Keys.Left) Saturation -= step;
            else if (e.KeyCode == Keys.Right) Saturation += step;
            else if (e.KeyCode == Keys.Up) Value += step;
            else if (e.KeyCode == Keys.Down) Value -= step;
            else { base.OnKeyDown(e); return; }
            OnColorPositionChanged();
            e.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeGradient();
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            DisposeGradient();
            base.OnSizeChanged(e);
        }

        private void SetFromPoint(Point point)
        {
            _saturation = Math.Max(0F, Math.Min(1F,
                point.X / (float)Math.Max(1, ClientSize.Width - 1)));
            _value = 1F - Math.Max(0F, Math.Min(1F,
                point.Y / (float)Math.Max(1, ClientSize.Height - 1)));
            Invalidate();
            OnColorPositionChanged();
        }

        private void OnColorPositionChanged()
        {
            EventHandler handler = ColorPositionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void EnsureGradient()
        {
            if (_gradient != null || ClientSize.Width < 1 ||
                ClientSize.Height < 1) return;
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            _gradient = new Bitmap(width, height,
                PixelFormat.Format32bppArgb);
            int[] pixels = new int[width * height];
            for (int y = 0; y < height; y++)
            {
                float value = 1F - y / (float)Math.Max(1, height - 1);
                for (int x = 0; x < width; x++)
                {
                    float saturation = x /
                        (float)Math.Max(1, width - 1);
                    pixels[y * width + x] = ColorPickerMath.FromHsv(
                        _hue, saturation, value).ToArgb();
                }
            }
            Rectangle bounds = new Rectangle(0, 0, width, height);
            BitmapData data = _gradient.LockBits(bounds,
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            _gradient.UnlockBits(data);
        }

        private void DisposeGradient()
        {
            if (_gradient == null) return;
            _gradient.Dispose();
            _gradient = null;
        }
    }

    internal sealed class HueControl : Control
    {
        private Bitmap _gradient;
        private float _hue;
        private bool _dragging;

        public HueControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = true;
        }

        public event EventHandler HueChanged;

        public float Hue
        {
            get { return _hue; }
            set
            {
                _hue = value % 360F;
                if (_hue < 0F) _hue += 360F;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            EnsureGradient();
            if (_gradient != null)
                e.Graphics.DrawImageUnscaled(_gradient, 0, 0);
            int y = (int)Math.Round(_hue / 360F *
                Math.Max(0, ClientSize.Height - 1));
            using (var dark = new Pen(Color.Black, 3F))
            using (var light = new Pen(Color.White, 1F))
            {
                e.Graphics.DrawRectangle(dark, -1, y - 2,
                    ClientSize.Width + 1, 4);
                e.Graphics.DrawLine(light, 0, y,
                    ClientSize.Width - 1, y);
            }
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                SystemColors.ControlDark, ButtonBorderStyle.Solid);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            _dragging = true;
            Capture = true;
            SetFromY(e.Y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) SetFromY(e.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = false;
            Capture = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            float step = e.Shift ? 10F : 1F;
            if (e.KeyCode == Keys.Up) Hue -= step;
            else if (e.KeyCode == Keys.Down) Hue += step;
            else { base.OnKeyDown(e); return; }
            OnHueChanged();
            e.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _gradient != null) _gradient.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            if (_gradient != null)
            {
                _gradient.Dispose();
                _gradient = null;
            }
            base.OnSizeChanged(e);
        }

        private void SetFromY(int y)
        {
            _hue = Math.Max(0F, Math.Min(359.999F,
                y / (float)Math.Max(1, ClientSize.Height - 1) * 360F));
            Invalidate();
            OnHueChanged();
        }

        private void OnHueChanged()
        {
            EventHandler handler = HueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void EnsureGradient()
        {
            if (_gradient != null || ClientSize.Width < 1 ||
                ClientSize.Height < 1) return;
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            _gradient = new Bitmap(width, height,
                PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(_gradient))
                for (int y = 0; y < height; y++)
                {
                    float hue = y /
                        (float)Math.Max(1, height - 1) * 359.999F;
                    using (var pen = new Pen(ColorPickerMath.FromHsv(
                        hue, 1F, 1F)))
                        graphics.DrawLine(pen, 0, y, width - 1, y);
                }
        }
    }

    internal sealed class ColorComparisonControl : Control
    {
        public Color OriginalColor { get; set; }
        public Color SelectedColor { get; set; }

        public ColorComparisonControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int cell = 8;
            for (int y = 0; y < ClientSize.Height; y += cell)
                for (int x = 0; x < ClientSize.Width; x += cell)
                    using (var brush = new SolidBrush(
                        ((x / cell + y / cell) & 1) == 0
                            ? Color.White : Color.LightGray))
                        e.Graphics.FillRectangle(brush, x, y, cell, cell);
            int half = ClientSize.Width / 2;
            using (var original = new SolidBrush(OriginalColor))
            using (var selected = new SolidBrush(SelectedColor))
            {
                e.Graphics.FillRectangle(original, 0, 0,
                    half, ClientSize.Height);
                e.Graphics.FillRectangle(selected, half, 0,
                    ClientSize.Width - half, ClientSize.Height);
            }
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                SystemColors.ControlDark, ButtonBorderStyle.Solid);
            using (var pen = new Pen(SystemColors.ControlDark))
                e.Graphics.DrawLine(pen, half, 0, half,
                    ClientSize.Height);
        }
    }
}
