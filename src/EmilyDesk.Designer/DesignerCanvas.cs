using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace EmilyDesk.Designer
{
    internal sealed class DesignerCanvas : Control
    {
        private const float DetailsPreviewMarginX = 160F;
        private const float DetailsPreviewMarginY = 80F;
        private const int MaximumPreviewImageDimension = 2048;
        private const long MaximumPreviewCacheBytes = 128L * 1024L * 1024L;
        private const long PreviewFileCheckIntervalTicks = TimeSpan.TicksPerSecond;
        private DesignerLayout _layout;
        private DesignerElement _selected;
        private float _zoom = 1F;
        private bool _moving;
        private bool _resizing;
        private bool _rotating;
        private bool _nudging;
        private Point _mouseStart;
        private RectangleF _elementStart;
        private readonly Dictionary<DesignerElement, PointF> _moveGroupStarts =
            new Dictionary<DesignerElement, PointF>();
        private PointF _elementStartPivot;
        private float _rotationStartAngle;
        private float _rotationStartValue;
        private float _resizeStartDistance;
        private Bitmap _referenceBitmap;
        private float? _verticalGuide;
        private float? _horizontalGuide;
        private DesignerSurface _activeSurface;
        private bool _movingPanel;
        private Point _panelMouseStart;
        private float _panelStartX;
        private float _panelStartY;
        private readonly Dictionary<string, PreviewImageEntry> _previewImages =
            new Dictionary<string, PreviewImageEntry>(
                StringComparer.OrdinalIgnoreCase);
        private long _previewImageBytes;
        private long _previewImageAccess;

        private sealed class PreviewImageEntry
        {
            public Bitmap Image;
            public long FileLength;
            public long LastWriteTicks;
            public long EstimatedBytes;
            public long LastAccess;
            public long NextFileCheckTicks;
        }

        public event EventHandler SelectionChanged;
        public event EventHandler EditBeginning;
        public event EventHandler LayoutChanged;
        public event EventHandler EditCompleted;
        internal bool IsMovingSelection
        { get { return _moving || _nudging; } }
        public string ProjectRoot { get; set; }
        public bool StateSurfaces { get; set; }
        private bool UsesDetailsPanel
        { get { return !StateSurfaces && _activeSurface == DesignerSurface.WeatherDetails; } }
        public Action<Graphics, Rectangle> ReferenceRenderer { get; set; }
        public Action<Graphics, Rectangle> ForegroundRenderer { get; set; }
        public Action<Graphics, DesignerElement, RectangleF> BoundImageRenderer { get; set; }
        // The canvas stays renderer-backed, while this lets the form supply a
        // readable preview value for an element's plain-language binding.
        public Func<DesignerElement, string> BoundTextRenderer { get; set; }
        public Func<DesignerElement, Color?> TextColorRenderer { get; set; }
        public Action<Graphics, DesignerElement, RectangleF> TextBackgroundRenderer { get; set; }
        public DesignerSurface ActiveSurface
        {
            get { return _activeSurface; }
            set
            {
                _activeSurface = value;
                _selected = null;
                ClearReferenceBitmap();
                UpdateCanvasSize();
                Invalidate();
                OnSelectionChanged();
            }
        }

        public DesignerLayout CurrentLayout
        {
            get { return _layout; }
            set
            {
                _layout = value;
                _selected = null;
                ClearReferenceBitmap();
                UpdateCanvasSize();
                Invalidate();
            }
        }

        public DesignerElement SelectedElement
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); OnSelectionChanged(); }
        }

        public float Zoom
        {
            get { return _zoom; }
            set { _zoom = Math.Max(.5F, Math.Min(2F, value)); UpdateCanvasSize(); Invalidate(); }
        }

        public DesignerCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(55, 58, 62);
            SetStyle(ControlStyles.Selectable, true);
        }

        public void RefreshReference()
        {
            ClearReferenceBitmap();
            Invalidate();
        }

        public DesignerElement ElementAtClientPoint(Point point)
        {
            return HitTest(ToCanvas(point));
        }

        public PointF DesignPoint(Point point)
        {
            return ToCanvas(point);
        }

        private void UpdateCanvasSize()
        {
            if (_layout == null) return;
            float extraWidth = UsesDetailsPanel
                ? DetailsPreviewMarginX * 2F : 0F;
            float extraHeight = UsesDetailsPanel
                ? DetailsPreviewMarginY * 2F : 0F;
            Size = new Size(
                (int)Math.Ceiling((_layout.CanvasWidth + extraWidth) * _zoom),
                (int)Math.Ceiling((_layout.CanvasHeight + extraHeight) * _zoom));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_layout == null) return;
            PointF origin = SurfaceOrigin();
            using (var transform = new Matrix(
                _zoom, 0F, 0F, _zoom,
                origin.X * _zoom, origin.Y * _zoom))
                e.Graphics.Transform = transform;
            bool interactive = _moving || _resizing || _rotating ||
                _movingPanel;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = interactive
                ? CompositingQuality.HighSpeed
                : CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = interactive
                ? InterpolationMode.Bilinear
                : InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = interactive
                ? PixelOffsetMode.HighSpeed
                : PixelOffsetMode.HighQuality;
            e.Graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            DrawBackground(e.Graphics);
            if (_layout.GridEnabled) DrawGrid(e.Graphics);
            foreach (DesignerElement element in _layout.Elements)
                if (element.Visible && element.Surface == _activeSurface)
                    DrawElement(e.Graphics, element);
            if (ForegroundRenderer != null)
                ForegroundRenderer(e.Graphics, new Rectangle(0, 0,
                    _layout.CanvasWidth, _layout.CanvasHeight));
            if (_selected != null) DrawSelection(e.Graphics, _selected);
            else if (UsesDetailsPanel)
                DrawPanelSelection(e.Graphics);
        }

        public Bitmap RenderPackageArtwork()
        {
            if (_layout == null) return null;
            var bitmap = new Bitmap(_layout.CanvasWidth, _layout.CanvasHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingQuality =
                    CompositingQuality.HighQuality;
                graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                DrawBackground(graphics);
                foreach (DesignerElement element in _layout.Elements)
                    if (element.Visible && element.Surface == _activeSurface)
                        DrawElement(graphics, element);
                if (ForegroundRenderer != null)
                    ForegroundRenderer(graphics, new Rectangle(0, 0,
                        _layout.CanvasWidth, _layout.CanvasHeight));
            }
            return bitmap;
        }

        private void DrawPanelSelection(Graphics graphics)
        {
            using (var pen = new Pen(Color.FromArgb(190, Color.MediumTurquoise),
                1.5F / _zoom))
            {
                pen.DashStyle = DashStyle.Dash;
                graphics.DrawRectangle(pen, 0F, 0F,
                    _layout.CanvasWidth - 1F, _layout.CanvasHeight - 1F);
            }
        }

        private void DrawBackground(Graphics graphics)
        {
            graphics.FillRectangle(Brushes.Transparent, 0, 0,
                _layout.CanvasWidth, _layout.CanvasHeight);
            string backgroundId = _activeSurface == DesignerSurface.Main
                ? "main-background" : "details-background";
            bool editableBackground = (_layout.DeletedElementIds != null &&
                _layout.DeletedElementIds.Exists(delegate(string id)
                {
                    return string.Equals(id, backgroundId,
                        StringComparison.OrdinalIgnoreCase);
                })) || (_layout.Elements != null &&
                _layout.Elements.Exists(delegate(DesignerElement element)
                {
                    return element != null && string.Equals(element.Id,
                        backgroundId, StringComparison.OrdinalIgnoreCase);
                }));
            if (editableBackground) return;
            if (ReferenceRenderer != null)
            {
                if (_referenceBitmap == null ||
                    _referenceBitmap.Width != _layout.CanvasWidth ||
                    _referenceBitmap.Height != _layout.CanvasHeight)
                {
                    ClearReferenceBitmap();
                    _referenceBitmap = new Bitmap(
                        _layout.CanvasWidth, _layout.CanvasHeight);
                    using (Graphics referenceGraphics =
                        Graphics.FromImage(_referenceBitmap))
                    {
                        referenceGraphics.SmoothingMode =
                            SmoothingMode.AntiAlias;
                        referenceGraphics.CompositingQuality =
                            CompositingQuality.HighQuality;
                        referenceGraphics.InterpolationMode =
                            InterpolationMode.HighQualityBicubic;
                        referenceGraphics.PixelOffsetMode =
                            PixelOffsetMode.HighQuality;
                        referenceGraphics.TextRenderingHint =
                            System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        ReferenceRenderer(referenceGraphics,
                            new Rectangle(0, 0,
                                _layout.CanvasWidth, _layout.CanvasHeight));
                    }
                }
                graphics.DrawImageUnscaled(_referenceBitmap, 0, 0);
                return;
            }
            string path = ResolvePath(_layout.BackgroundImage);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                Image image = GetPreviewImage(path);
                graphics.DrawImage(image, new Rectangle(0, 0,
                    _layout.CanvasWidth, _layout.CanvasHeight));
            }
        }

        private void DrawGrid(Graphics graphics)
        {
            int size = Math.Max(2, _layout.GridSize);
            using (var pen = new Pen(Color.FromArgb(45, Color.White), 1F / _zoom))
            {
                for (int x = 0; x <= _layout.CanvasWidth; x += size)
                    graphics.DrawLine(pen, x, 0, x, _layout.CanvasHeight);
                for (int y = 0; y <= _layout.CanvasHeight; y += size)
                    graphics.DrawLine(pen, 0, y, _layout.CanvasWidth, y);
            }
        }

        private void DrawElement(Graphics graphics, DesignerElement element)
        {
            RectangleF bounds = BoundsOf(element);
            if (element.Kind == DesignerElementKind.Divider)
            {
                Color color = Color.FromArgb(element.ColorArgb);
                color = Color.FromArgb((int)(255 * Clamp(element.Opacity)), color);
                float thickness = Math.Max(1F,
                    Math.Min(bounds.Width, bounds.Height));
                using (var pen = new Pen(color, thickness))
                {
                    if (bounds.Width >= bounds.Height)
                        graphics.DrawLine(pen, bounds.Left,
                            bounds.Top + bounds.Height / 2F,
                            bounds.Right, bounds.Top + bounds.Height / 2F);
                    else
                        graphics.DrawLine(pen,
                            bounds.Left + bounds.Width / 2F, bounds.Top,
                            bounds.Left + bounds.Width / 2F, bounds.Bottom);
                }
                return;
            }
            if (element.Kind == DesignerElementKind.Image)
            {
                bool editableBackground = string.Equals(element.Id,
                        "main-background", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(element.Id, "details-background",
                        StringComparison.OrdinalIgnoreCase);
                if (BoundImageRenderer != null &&
                    (editableBackground ||
                     (!string.IsNullOrWhiteSpace(element.Binding) &&
                     (string.IsNullOrWhiteSpace(element.ImagePath) ||
                     (element.Binding.StartsWith("Clock:",
                        StringComparison.OrdinalIgnoreCase) ||
                      element.Binding.StartsWith("Calculator: LCD",
                        StringComparison.OrdinalIgnoreCase) ||
                      element.Binding.StartsWith("Currency: LCD",
                        StringComparison.OrdinalIgnoreCase))))))
                {
                    BoundImageRenderer(graphics, element, bounds);
                    return;
                }
                string path = ResolvePath(element.ImagePath);
                if (File.Exists(path))
                {
                    using (var attributes = new ImageAttributes())
                    {
                        Image image = GetPreviewImage(path);
                        var matrix = new ColorMatrix { Matrix33 = Clamp(element.Opacity) };
                        attributes.SetColorMatrix(matrix);
                        graphics.DrawImage(image, Rectangle.Round(bounds), 0, 0,
                            image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }
                }
                return;
            }

            if (TextBackgroundRenderer != null) TextBackgroundRenderer(graphics, element, bounds);
            FontStyle style = FontStyle.Regular;
            if (element.Bold) style |= FontStyle.Bold;
            if (element.Italic) style |= FontStyle.Italic;
            using (var font = XWidgetReborn.WidgetSdk.DesignerFontResolver.Create(
                element.FontName, element.FontFile,
                Math.Max(4F, element.FontSize * element.Scale), style))
            using (var brush = new SolidBrush(Color.FromArgb(
                (int)(255 * Clamp(element.Opacity)), TextColorRenderer == null
                    ? Color.FromArgb(element.ColorArgb)
                    : TextColorRenderer(element) ?? Color.FromArgb(element.ColorArgb))))
            using (var format = new StringFormat { LineAlignment = StringAlignment.Center })
            {
                format.Alignment = element.Alignment == DesignerTextAlignment.Center ?
                    StringAlignment.Center : element.Alignment == DesignerTextAlignment.Right ?
                    StringAlignment.Far : StringAlignment.Near;
                if (!element.WordWrap)
                    format.FormatFlags |= StringFormatFlags.NoWrap;
                format.Trimming = ToStringTrimming(element.Trimming);
                string text = BoundTextRenderer == null
                    ? element.Text : BoundTextRenderer(element);
                graphics.DrawString(text ?? string.Empty, font, brush, bounds, format);
            }
        }

        private static StringTrimming ToStringTrimming(
            DesignerTextTrimming trimming)
        {
            switch (trimming)
            {
                case DesignerTextTrimming.Character:
                    return StringTrimming.Character;
                case DesignerTextTrimming.Word:
                    return StringTrimming.Word;
                case DesignerTextTrimming.EllipsisCharacter:
                    return StringTrimming.EllipsisCharacter;
                case DesignerTextTrimming.EllipsisWord:
                    return StringTrimming.EllipsisWord;
                case DesignerTextTrimming.EllipsisPath:
                    return StringTrimming.EllipsisPath;
                default:
                    return StringTrimming.None;
            }
        }

        private void DrawSelection(Graphics graphics, DesignerElement element)
        {
            bool linkedGroup = IsLinkedMoveGroup(element);
            RectangleF bounds = linkedGroup
                ? LinkedMoveGroupBounds(element)
                : SelectionBounds(element);
            Color selectionColor = element.MouseLocked
                ? Color.Goldenrod : Color.DeepSkyBlue;
            if (IsClockHandElement(element))
            {
                DrawClockHandSelection(graphics, element, bounds,
                    selectionColor);
                return;
            }
            using (var pen = new Pen(selectionColor, 1.5F / _zoom))
            {
                pen.DashStyle = DashStyle.Dash;
                graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
            if (element.MouseLocked || linkedGroup)
            {
                DrawGuides(graphics, bounds);
                return;
            }
            float handle = 8F / _zoom;
            graphics.FillRectangle(Brushes.White, bounds.Right - handle,
                bounds.Bottom - handle, handle, handle);
            using (var pen = new Pen(Color.DeepSkyBlue, 1F / _zoom))
                graphics.DrawRectangle(pen, bounds.Right - handle,
                    bounds.Bottom - handle, handle, handle);
            DrawGuides(graphics, bounds);
        }

        private void DrawClockHandSelection(Graphics graphics,
            DesignerElement element, RectangleF bounds, Color colour)
        {
            PointF pivot = new PointF(element.PivotX, element.PivotY);
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(pivot.X, pivot.Y);
                graphics.RotateTransform(element.PreviewRotation);
                graphics.TranslateTransform(-pivot.X, -pivot.Y);
                using (var pen = new Pen(colour, 1.5F / _zoom))
                {
                    pen.DashStyle = DashStyle.Dash;
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y,
                        bounds.Width, bounds.Height);
                }
            }
            finally { graphics.Restore(state); }
            if (element.MouseLocked) return;
            float size = 9F / _zoom;
            PointF resize = ClockHandResizeHandle(element);
            graphics.FillRectangle(Brushes.White,
                resize.X - size / 2F, resize.Y - size / 2F,
                size, size);
            using (var pen = new Pen(Color.DeepSkyBlue, 1F / _zoom))
                graphics.DrawRectangle(pen,
                    resize.X - size / 2F, resize.Y - size / 2F,
                    size, size);
            PointF rotate = ClockHandRotationHandle(element);
            PointF corner = RotateAround(new PointF(bounds.Right,
                bounds.Top), pivot, element.PreviewRotation);
            using (var pen = new Pen(Color.DeepSkyBlue, 1F / _zoom))
            {
                graphics.DrawLine(pen, corner, rotate);
                graphics.FillEllipse(Brushes.White,
                    rotate.X - size / 2F, rotate.Y - size / 2F,
                    size, size);
                graphics.DrawEllipse(pen,
                    rotate.X - size / 2F, rotate.Y - size / 2F,
                    size, size);
            }
            using (var pivotBrush = new SolidBrush(
                Color.FromArgb(220, Color.HotPink)))
            {
                float pivotSize = 7F / _zoom;
                graphics.FillEllipse(pivotBrush,
                    pivot.X - pivotSize / 2F,
                    pivot.Y - pivotSize / 2F,
                    pivotSize, pivotSize);
            }
        }

        private void DrawGuides(Graphics graphics, RectangleF bounds)
        {
            using (var pen = new Pen(Color.FromArgb(180, Color.HotPink), 1F / _zoom))
            {
                if (_verticalGuide.HasValue)
                    graphics.DrawLine(pen, _verticalGuide.Value, 0,
                        _verticalGuide.Value, _layout.CanvasHeight);
                if (_horizontalGuide.HasValue)
                    graphics.DrawLine(pen, 0, _horizontalGuide.Value,
                        _layout.CanvasWidth, _horizontalGuide.Value);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            PointF point = ToCanvas(e.Location);
            bool rotationHandle = _selected != null &&
                IsClockHandElement(_selected) &&
                Near(point, ClockHandRotationHandle(_selected),
                    12F / _zoom);
            bool resizeHandle = _selected != null &&
                IsClockHandElement(_selected) &&
                Near(point, ClockHandResizeHandle(_selected),
                    12F / _zoom);
            bool selectedBody = _selected != null &&
                IsClockHandElement(_selected) &&
                _selected.Visible &&
                _selected.Surface == _activeSurface &&
                ElementContains(_selected, point);
            DesignerElement hit = rotationHandle || resizeHandle ||
                selectedBody
                ? _selected : HitTest(point);
            if (hit != _selected) { _selected = hit; OnSelectionChanged(); Invalidate(); }
            if (e.Button != MouseButtons.Left) return;
            if (_selected == null &&
                UsesDetailsPanel &&
                PanelContains(point))
            {
                DesignerPanelSettings panel = _layout.WeatherDetailsPanel;
                if (panel == null) return;
                _movingPanel = true;
                _panelMouseStart = e.Location;
                _panelStartX = panel.PositionX;
                _panelStartY = panel.PositionY;
                Cursor = Cursors.SizeAll;
                if (EditBeginning != null)
                    EditBeginning(this, EventArgs.Empty);
                return;
            }
            if (_selected == null) return;
            if (_selected.MouseLocked) return;
            if (rotationHandle)
            {
                _rotating = true;
                _elementStartPivot = new PointF(
                    _selected.PivotX, _selected.PivotY);
                _rotationStartAngle = AngleFrom(
                    _elementStartPivot, point);
                _rotationStartValue = _selected.PreviewRotation;
                Cursor = Cursors.Cross;
                if (EditBeginning != null)
                    EditBeginning(this, EventArgs.Empty);
                return;
            }
            RectangleF bounds = SelectionBounds(_selected);
            float handle = 12F / _zoom;
            bool linkedGroup = IsLinkedMoveGroup(_selected);
            _resizing = !linkedGroup && (resizeHandle ||
                (!IsClockHandElement(_selected) &&
                new RectangleF(bounds.Right - handle,
                    bounds.Bottom - handle, handle, handle).Contains(point)));
            _moving = !_resizing;
            _mouseStart = e.Location;
            _elementStart = new RectangleF(_selected.X, _selected.Y,
                _selected.Width, _selected.Height);
            _moveGroupStarts.Clear();
            if (_moving && _selected.MoveWithGroup &&
                !string.IsNullOrWhiteSpace(_selected.MoveGroup))
                foreach (DesignerElement element in _layout.Elements)
                    if (element != _selected &&
                        element.Surface == _selected.Surface &&
                        string.Equals(element.MoveGroup,
                            _selected.MoveGroup,
                            StringComparison.OrdinalIgnoreCase))
                        _moveGroupStarts[element] = new PointF(
                            element.X, element.Y);
            _elementStartPivot = new PointF(
                _selected.PivotX, _selected.PivotY);
            _resizeStartDistance = Distance(point, _elementStartPivot);
            _verticalGuide = null;
            _horizontalGuide = null;
            if (EditBeginning != null) EditBeginning(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_rotating && _selected != null)
            {
                PointF point = ToCanvas(e.Location);
                float delta = AngleFrom(_elementStartPivot, point) -
                    _rotationStartAngle;
                if (delta > 180F) delta -= 360F;
                if (delta < -180F) delta += 360F;
                _selected.PreviewRotation = NormalizeSignedRotation(
                    _rotationStartValue + delta);
                if (_layout != null) _layout.ClockHandEditVersion = 1;
                Invalidate();
                if (LayoutChanged != null)
                    LayoutChanged(this, EventArgs.Empty);
                return;
            }
            if (_movingPanel)
            {
                float panelDx = (e.X - _panelMouseStart.X) / _zoom;
                float panelDy = (e.Y - _panelMouseStart.Y) / _zoom;
                DesignerPanelSettings panel = _layout.WeatherDetailsPanel;
                panel.PositionX = Snap(_panelStartX + panelDx);
                panel.PositionY = Snap(_panelStartY + panelDy);
                Invalidate();
                if (LayoutChanged != null)
                    LayoutChanged(this, EventArgs.Empty);
                return;
            }
            if (_selected == null || (!_moving && !_resizing)) return;
            float elementDeltaX = (e.X - _mouseStart.X) / _zoom;
            float elementDeltaY = (e.Y - _mouseStart.Y) / _zoom;
            if (_resizing)
            {
                if (IsClockHandElement(_selected))
                {
                    float currentDistance = Distance(
                        ToCanvas(e.Location), _elementStartPivot);
                    float ratio = _resizeStartDistance < 1F ? 1F :
                        Math.Max(.05F, currentDistance /
                            _resizeStartDistance);
                    _selected.Width = Math.Max(10F,
                        _elementStart.Width * ratio);
                    _selected.Height = Math.Max(10F,
                        _elementStart.Height * ratio);
                    _selected.PivotX = _elementStartPivot.X;
                    _selected.PivotY = _elementStartPivot.Y;
                }
                else
                {
                    float width = Math.Max(10F, Snap(
                        _elementStart.Width + elementDeltaX));
                    float height = Math.Max(10F, Snap(
                        _elementStart.Height + elementDeltaY));
                    if (_selected.Kind == DesignerElementKind.Image &&
                        _elementStart.Width > 0F &&
                        _elementStart.Height > 0F)
                    {
                        float ratio = _elementStart.Width /
                            _elementStart.Height;
                        if (Math.Abs(elementDeltaX) >=
                            Math.Abs(elementDeltaY))
                            height = Math.Max(10F, width / ratio);
                        else
                            width = Math.Max(10F, height * ratio);
                    }
                    _selected.Width = width;
                    _selected.Height = height;
                }
            }
            else
            {
                MoveWithSmartGuides(
                    Snap(_elementStart.X + elementDeltaX),
                    Snap(_elementStart.Y + elementDeltaY));
                float groupDeltaX = _selected.X - _elementStart.X;
                float groupDeltaY = _selected.Y - _elementStart.Y;
                foreach (KeyValuePair<DesignerElement, PointF> item in
                    _moveGroupStarts)
                {
                    item.Key.X = item.Value.X + groupDeltaX;
                    item.Key.Y = item.Value.Y + groupDeltaY;
                }
            }
            Invalidate();
            if (LayoutChanged != null) LayoutChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool edited = _moving || _resizing || _rotating || _movingPanel;
            _moving = false;
            _resizing = false;
            _rotating = false;
            _movingPanel = false;
            Cursor = Cursors.Default;
            _verticalGuide = null;
            _horizontalGuide = null;
            _moveGroupStarts.Clear();
            Invalidate();
            if (edited && EditCompleted != null)
                EditCompleted(this, EventArgs.Empty);
        }

        private void MoveWithSmartGuides(float proposedX, float proposedY)
        {
            float width = _selected.Width * _selected.Scale;
            float height = _selected.Height * _selected.Scale;
            float tolerance = 6F / _zoom;
            float bestXDelta = tolerance + 1F;
            float bestYDelta = tolerance + 1F;
            float? bestVertical = null;
            float? bestHorizontal = null;

            var verticalTargets = new System.Collections.Generic.List<float>();
            var horizontalTargets = new System.Collections.Generic.List<float>();

            // Screen edges are explicit snap targets. Without these, a widget
            // can only snap to the center or another widget, which makes the
            // top edge appear to stop short of the actual screen boundary.
            verticalTargets.Add(0F);
            verticalTargets.Add(_layout.CanvasWidth / 2F);
            verticalTargets.Add(_layout.CanvasWidth);
            horizontalTargets.Add(0F);
            horizontalTargets.Add(_layout.CanvasHeight / 2F);
            horizontalTargets.Add(_layout.CanvasHeight);
            foreach (DesignerElement element in _layout.Elements)
            {
                if (element == _selected || !element.Visible ||
                    element.Surface != _activeSurface) continue;
                RectangleF target = BoundsOf(element);
                verticalTargets.Add(target.Left);
                verticalTargets.Add(target.Left + target.Width / 2F);
                verticalTargets.Add(target.Right);
                horizontalTargets.Add(target.Top);
                horizontalTargets.Add(target.Top + target.Height / 2F);
                horizontalTargets.Add(target.Bottom);
            }

            float[] movingX = { proposedX, proposedX + width / 2F, proposedX + width };
            foreach (float target in verticalTargets)
                foreach (float source in movingX)
                {
                    float delta = target - source;
                    if (Math.Abs(delta) <= tolerance &&
                        Math.Abs(delta) < Math.Abs(bestXDelta))
                    {
                        bestXDelta = delta;
                        bestVertical = target;
                    }
                }

            float[] movingY = { proposedY, proposedY + height / 2F, proposedY + height };
            foreach (float target in horizontalTargets)
                foreach (float source in movingY)
                {
                    float delta = target - source;
                    if (Math.Abs(delta) <= tolerance &&
                        Math.Abs(delta) < Math.Abs(bestYDelta))
                    {
                        bestYDelta = delta;
                        bestHorizontal = target;
                    }
                }

            _selected.X = proposedX + (bestVertical.HasValue ? bestXDelta : 0F);
            _selected.Y = proposedY + (bestHorizontal.HasValue ? bestYDelta : 0F);
            _verticalGuide = bestVertical;
            _horizontalGuide = bestHorizontal;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int amount = e.Shift ? 10 : 1;
            if (e.KeyCode == Keys.Left) NudgeSelected(-amount, 0);
            else if (e.KeyCode == Keys.Right) NudgeSelected(amount, 0);
            else if (e.KeyCode == Keys.Up) NudgeSelected(0, -amount);
            else if (e.KeyCode == Keys.Down) NudgeSelected(0, amount);
            else return;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right ||
                key == Keys.Up || key == Keys.Down)
                return true;
            return base.IsInputKey(keyData);
        }

        public void NudgeSelected(int dx, int dy)
        {
            if (_selected == null)
            {
                if (_layout == null ||
                    !UsesDetailsPanel ||
                    _layout.WeatherDetailsPanel == null) return;
                if (EditBeginning != null)
                    EditBeginning(this, EventArgs.Empty);
                _layout.WeatherDetailsPanel.PositionX += dx;
                _layout.WeatherDetailsPanel.PositionY += dy;
                Invalidate();
                if (LayoutChanged != null)
                    LayoutChanged(this, EventArgs.Empty);
                if (EditCompleted != null)
                    EditCompleted(this, EventArgs.Empty);
                return;
            }
            if (EditBeginning != null) EditBeginning(this, EventArgs.Empty);
            _nudging = true;
            try
            {
                _selected.X += dx;
                _selected.Y += dy;
                if (_selected.MoveWithGroup &&
                    !string.IsNullOrWhiteSpace(_selected.MoveGroup))
                    foreach (DesignerElement element in _layout.Elements)
                        if (element != _selected &&
                            element.Surface == _selected.Surface &&
                            string.Equals(element.MoveGroup,
                                _selected.MoveGroup,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            element.X += dx;
                            element.Y += dy;
                        }
                Invalidate();
                if (LayoutChanged != null)
                    LayoutChanged(this, EventArgs.Empty);
            }
            finally { _nudging = false; }
            if (EditCompleted != null) EditCompleted(this, EventArgs.Empty);
        }

        private float Snap(float value)
        {
            if (_layout == null || !_layout.GridEnabled) return value;
            int size = Math.Max(2, _layout.GridSize);
            return (float)Math.Round(value / size) * size;
        }

        private DesignerElement HitTest(PointF point)
        {
            if (_layout == null) return null;
            for (int i = _layout.Elements.Count - 1; i >= 0; i--)
            {
                DesignerElement element = _layout.Elements[i];
                if (element.Visible && element.Surface == _activeSurface &&
                    ElementContains(element, point))
                    return element;
            }
            return null;
        }

        private bool ElementContains(DesignerElement element,
            PointF point)
        {
            if (IsClockHandElement(element))
                return ClockHandImageContains(element, point) ||
                    ClockHandContains(element, point);
            return SelectionBounds(element).Contains(point);
        }

        private static bool ClockHandContains(
            DesignerElement element, PointF point)
        {
            if (element.HandPivotX.HasValue ||
                element.HandPivotY.HasValue) return false;
            string binding = element.Binding ?? string.Empty;
            double turn;
            float tail;
            if (binding.Equals("Clock: Hour Hand",
                StringComparison.OrdinalIgnoreCase))
            { turn = element.PreviewRotation / 360D; tail = 13F; }
            else if (binding.Equals("Clock: Minute Hand",
                StringComparison.OrdinalIgnoreCase))
            { turn = element.PreviewRotation / 360D; tail = 13F; }
            else if (binding.Equals("Clock: Second Hand",
                StringComparison.OrdinalIgnoreCase))
            { turn = element.PreviewRotation / 360D; tail = 17F; }
            else return false;

            float scale = Math.Max(.1F, element.Scale);
            float width = Math.Max(1F, element.Width * scale);
            float height = Math.Max(tail + 3F, element.Height * scale);
            float cx = element.X + width / 2F;
            float cy = element.Y + height - tail;
            float length = height - tail;
            double angle = turn * Math.PI * 2D;
            PointF start = new PointF(
                cx - (float)Math.Sin(angle) * tail,
                cy + (float)Math.Cos(angle) * tail);
            PointF end = new PointF(
                cx + (float)Math.Sin(angle) * length,
                cy - (float)Math.Cos(angle) * length);
            return DistanceToSegment(point, start, end) <=
                Math.Max(8F, width / 2F + 4F);
        }

        private static bool ClockHandImageContains(
            DesignerElement element, PointF point)
        {
            if (!IsClockHandElement(element)) return false;
            PointF pivot = new PointF(element.PivotX, element.PivotY);
            PointF unrotated = RotateAround(point, pivot,
                -element.PreviewRotation);
            return new RectangleF(element.X, element.Y,
                element.Width * element.Scale,
                element.Height * element.Scale).Contains(unrotated);
        }

        private static bool IsClockHandElement(DesignerElement element)
        {
            string binding = element == null
                ? string.Empty : element.Binding ?? string.Empty;
            return binding.Equals("Clock: Hour Hand",
                    StringComparison.OrdinalIgnoreCase) ||
                binding.Equals("Clock: Minute Hand",
                    StringComparison.OrdinalIgnoreCase) ||
                binding.Equals("Clock: Second Hand",
                    StringComparison.OrdinalIgnoreCase);
        }

        private PointF ClockHandResizeHandle(DesignerElement element)
        {
            RectangleF bounds = BoundsOf(element);
            return RotateAround(new PointF(bounds.Right, bounds.Bottom),
                new PointF(element.PivotX, element.PivotY),
                element.PreviewRotation);
        }

        private PointF ClockHandRotationHandle(DesignerElement element)
        {
            RectangleF bounds = BoundsOf(element);
            PointF pivot = new PointF(element.PivotX, element.PivotY);
            PointF corner = RotateAround(new PointF(bounds.Right,
                bounds.Top), pivot, element.PreviewRotation);
            float dx = corner.X - pivot.X;
            float dy = corner.Y - pivot.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 1F) return corner;
            float extension = 18F / _zoom;
            return new PointF(corner.X + dx / length * extension,
                corner.Y + dy / length * extension);
        }

        private static PointF RotateAround(PointF point,
            PointF pivot, float degrees)
        {
            double radians = degrees * Math.PI / 180D;
            float x = point.X - pivot.X;
            float y = point.Y - pivot.Y;
            float cosine = (float)Math.Cos(radians);
            float sine = (float)Math.Sin(radians);
            return new PointF(pivot.X + x * cosine - y * sine,
                pivot.Y + x * sine + y * cosine);
        }

        private static float AngleFrom(PointF pivot, PointF point)
        {
            return (float)(Math.Atan2(point.Y - pivot.Y,
                point.X - pivot.X) * 180D / Math.PI);
        }

        private static float Distance(PointF left, PointF right)
        {
            float x = left.X - right.X;
            float y = left.Y - right.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static bool Near(PointF left, PointF right,
            float tolerance)
        {
            return Distance(left, right) <= tolerance;
        }

        private static float NormalizeSignedRotation(float value)
        {
            value %= 360F;
            return value;
        }

        private static float DistanceToSegment(
            PointF point, PointF start, PointF end)
        {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= .001F)
                return (float)Math.Sqrt(
                    (point.X - start.X) * (point.X - start.X) +
                    (point.Y - start.Y) * (point.Y - start.Y));
            float amount = ((point.X - start.X) * dx +
                (point.Y - start.Y) * dy) / lengthSquared;
            amount = Math.Max(0F, Math.Min(1F, amount));
            float x = start.X + amount * dx;
            float y = start.Y + amount * dy;
            return (float)Math.Sqrt((point.X - x) * (point.X - x) +
                (point.Y - y) * (point.Y - y));
        }

        private RectangleF BoundsOf(DesignerElement element)
        {
            return new RectangleF(element.X, element.Y,
                element.Width * element.Scale, element.Height * element.Scale);
        }

        private RectangleF SelectionBounds(DesignerElement element)
        {
            RectangleF bounds = BoundsOf(element);
            if (element.Kind != DesignerElementKind.Divider) return bounds;
            const float minimumHitSize = 14F;
            if (bounds.Width < minimumHitSize)
                bounds.Inflate((minimumHitSize - bounds.Width) / 2F, 0F);
            if (bounds.Height < minimumHitSize)
                bounds.Inflate(0F, (minimumHitSize - bounds.Height) / 2F);
            return bounds;
        }

        private bool IsLinkedMoveGroup(DesignerElement element)
        {
            return element != null && element.MoveWithGroup &&
                !string.IsNullOrWhiteSpace(element.MoveGroup);
        }

        private RectangleF LinkedMoveGroupBounds(DesignerElement element)
        {
            RectangleF result = BoundsOf(element);
            if (_layout == null) return result;
            foreach (DesignerElement linked in _layout.Elements)
            {
                if (linked == null || !linked.Visible ||
                    linked.Surface != element.Surface ||
                    !string.Equals(linked.MoveGroup, element.MoveGroup,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                result = RectangleF.Union(result, BoundsOf(linked));
            }
            return result;
        }

        private void ClearReferenceBitmap()
        {
            if (_referenceBitmap == null) return;
            _referenceBitmap.Dispose();
            _referenceBitmap = null;
        }

        internal Image GetPreviewImage(string path)
        {
            string fullPath = Path.GetFullPath(path);
            long now = DateTime.UtcNow.Ticks;
            PreviewImageEntry entry;
            if (_previewImages.TryGetValue(fullPath, out entry))
            {
                if (now < entry.NextFileCheckTicks)
                {
                    entry.LastAccess = ++_previewImageAccess;
                    return entry.Image;
                }
                var currentInformation = new FileInfo(fullPath);
                long currentLength = currentInformation.Length;
                long currentWriteTicks =
                    currentInformation.LastWriteTimeUtc.Ticks;
                if (entry.FileLength == currentLength &&
                    entry.LastWriteTicks == currentWriteTicks)
                {
                    entry.LastAccess = ++_previewImageAccess;
                    entry.NextFileCheckTicks = now +
                        PreviewFileCheckIntervalTicks;
                    return entry.Image;
                }
                RemovePreviewImage(fullPath, entry);
            }

            var information = new FileInfo(fullPath);
            long length = information.Length;
            long writeTicks = information.LastWriteTimeUtc.Ticks;
            Bitmap image = LoadPreviewImage(fullPath);
            long estimatedBytes = (long)image.Width * image.Height * 4L;
            MakePreviewCacheRoom(estimatedBytes);
            entry = new PreviewImageEntry
            {
                Image = image,
                FileLength = length,
                LastWriteTicks = writeTicks,
                EstimatedBytes = estimatedBytes,
                LastAccess = ++_previewImageAccess,
                NextFileCheckTicks = now + PreviewFileCheckIntervalTicks
            };
            _previewImages[fullPath] = entry;
            _previewImageBytes += estimatedBytes;
            return image;
        }

        private static Bitmap LoadPreviewImage(string path)
        {
            using (Image source = Image.FromFile(path))
            {
                float scale = Math.Min(1F,
                    MaximumPreviewImageDimension /
                    (float)Math.Max(source.Width, source.Height));
                int width = Math.Max(1,
                    (int)Math.Round(source.Width * scale));
                int height = Math.Max(1,
                    (int)Math.Round(source.Height * scale));
                var preview = new Bitmap(width, height,
                    PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(preview))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality =
                        CompositingQuality.HighQuality;
                    graphics.InterpolationMode =
                        InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(source,
                        new Rectangle(0, 0, width, height),
                        0, 0, source.Width, source.Height,
                        GraphicsUnit.Pixel);
                }
                return preview;
            }
        }

        private void MakePreviewCacheRoom(long requiredBytes)
        {
            while (_previewImages.Count > 0 &&
                _previewImageBytes + requiredBytes >
                    MaximumPreviewCacheBytes)
            {
                string oldestPath = null;
                PreviewImageEntry oldest = null;
                foreach (KeyValuePair<string, PreviewImageEntry> pair
                    in _previewImages)
                    if (oldest == null ||
                        pair.Value.LastAccess < oldest.LastAccess)
                    {
                        oldestPath = pair.Key;
                        oldest = pair.Value;
                    }
                if (oldest == null) break;
                RemovePreviewImage(oldestPath, oldest);
            }
        }

        private void RemovePreviewImage(string path,
            PreviewImageEntry entry)
        {
            _previewImages.Remove(path);
            _previewImageBytes = Math.Max(0L,
                _previewImageBytes - entry.EstimatedBytes);
            if (entry.Image != null) entry.Image.Dispose();
        }

        private void ClearPreviewImages()
        {
            foreach (PreviewImageEntry entry in _previewImages.Values)
                if (entry.Image != null) entry.Image.Dispose();
            _previewImages.Clear();
            _previewImageBytes = 0L;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearReferenceBitmap();
                ClearPreviewImages();
            }
            base.Dispose(disposing);
        }

        private PointF ToCanvas(Point point)
        {
            PointF origin = SurfaceOrigin();
            return new PointF(point.X / _zoom - origin.X,
                point.Y / _zoom - origin.Y);
        }

        private PointF SurfaceOrigin()
        {
            if (_layout == null ||
                !UsesDetailsPanel)
                return PointF.Empty;
            DesignerPanelSettings panel = _layout.WeatherDetailsPanel;
            return new PointF(
                DetailsPreviewMarginX + (panel == null ? 0F : panel.PositionX),
                DetailsPreviewMarginY + (panel == null ? 0F : panel.PositionY));
        }

        private bool PanelContains(PointF point)
        {
            return point.X >= 0F && point.Y >= 0F &&
                point.X <= _layout.CanvasWidth &&
                point.Y <= _layout.CanvasHeight;
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(ProjectRoot ?? string.Empty,
                path.Replace('/', Path.DirectorySeparatorChar));
        }

        private static float Clamp(float value)
        {
            return Math.Max(0F, Math.Min(1F, value));
        }

        private void OnSelectionChanged()
        {
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }
    }
}
