using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace XWidgetReborn.WidgetSdk
{
    public enum SlidePanelOpenDirection { Left, Right, Top, Bottom }
    public enum SlidePanelEasing
    {
        SmoothEaseInOut,
        Linear,
        BouncyEaseOut
    }

    public sealed class SlidePanelLayout
    {
        public RectangleF CompositionBounds { get; internal set; }
        public RectangleF ParentBounds { get; internal set; }
        public RectangleF PanelBounds { get; internal set; }
        public RectangleF RevealClip { get; internal set; }
        public float Progress { get; internal set; }
    }

    /// <summary>
    /// Reusable child-layer state and geometry for a panel that slides beneath
    /// stationary parent artwork inside one fixed widget composition surface.
    /// </summary>
    public sealed class SlidePanel
    {
        private float _progress;
        private float _startProgress;
        private float _targetProgress;
        private DateTime _startedUtc;

        public SlidePanel()
        {
            Duration = TimeSpan.FromMilliseconds(340);
            Easing = SlidePanelEasing.SmoothEaseInOut;
            OpenDirection = SlidePanelOpenDirection.Left;
            Scale = 1F;
            ZIndex = -1;
            ClipToParent = true;
            RelativePanelSize = .80F;
        }

        public bool IsSlided { get; private set; }
        public bool IsCollapsed { get { return _progress <= 0F && !IsSlided; } }
        public bool IsAnimating { get { return Math.Abs(_progress - _targetProgress) > .0001F; } }
        public SlidePanelOpenDirection OpenDirection { get; set; }
        public float SlideOffset { get; set; }
        public float RelativePanelSize { get; set; }
        public TimeSpan Duration { get; set; }
        public SlidePanelEasing Easing { get; set; }
        public bool AutoSlideOnPointerEnter { get; set; }
        public int ZIndex { get; set; }
        public bool ClipToParent { get; set; }
        public RectangleF CompositionBounds { get; set; }
        public RectangleF ParentBounds { get; set; }
        public RectangleF PanelBounds { get; set; }
        public float OpenPositionOffsetX { get; set; }
        public float OpenPositionOffsetY { get; set; }
        public float Scale { get; set; }
        public float Progress { get { return _progress; } }

        public void FitRelativePanel(float artworkAspectRatio, float desiredOverlap)
        {
            float relative = Math.Max(.05F, Math.Min(1F, RelativePanelSize));
            float width = ParentBounds.Width * relative;
            float height = ParentBounds.Height * relative;
            PanelBounds = new RectangleF(
                ParentBounds.Left,
                ParentBounds.Top + (ParentBounds.Height - height) / 2F,
                width, height);
            float extent = OpenDirection == SlidePanelOpenDirection.Left ||
                OpenDirection == SlidePanelOpenDirection.Right ? width : height;
            SlideOffset = Math.Max(0F, extent - Math.Max(0F, desiredOverlap));
        }

        public void FitRelativeVisiblePanel(
            RectangleF parentVisibleBounds, float desiredOverlap)
        {
            float relative = Math.Max(.05F, Math.Min(1F, RelativePanelSize));
            float width = parentVisibleBounds.Width * relative;
            float height = parentVisibleBounds.Height * relative;
            PanelBounds = new RectangleF(
                parentVisibleBounds.Left,
                parentVisibleBounds.Top +
                    (parentVisibleBounds.Height - height) / 2F,
                width, height);
            float extent = OpenDirection == SlidePanelOpenDirection.Left ||
                OpenDirection == SlidePanelOpenDirection.Right ? width : height;
            SlideOffset = Math.Max(0F, extent - Math.Max(0F, desiredOverlap));
        }

        public void Toggle(DateTime nowUtc) { SetSlided(!IsSlided, nowUtc); }

        public void SetSlided(bool value, DateTime nowUtc)
        {
            IsSlided = value;
            _startProgress = _progress;
            _targetProgress = value ? 1F : 0F;
            _startedUtc = nowUtc;
            if (Duration <= TimeSpan.Zero)
                _progress = _targetProgress;
        }

        public void SetProgress(float progress)
        {
            _progress = Math.Max(0F, Math.Min(1F, progress));
            _startProgress = _progress;
            _targetProgress = _progress;
            IsSlided = _progress >= 1F;
        }

        public bool Advance(DateTime nowUtc)
        {
            if (!IsAnimating) return false;
            double seconds = Math.Max(.001D, Duration.TotalSeconds);
            float t = (float)Math.Max(0D, Math.Min(1D,
                (nowUtc - _startedUtc).TotalSeconds / seconds));
            float eased;
            if (Easing == SlidePanelEasing.Linear)
                eased = t;
            else if (Easing == SlidePanelEasing.BouncyEaseOut)
            {
                const float c1 = 1.70158F;
                const float c3 = c1 + 1F;
                float u = t - 1F;
                eased = 1F + c3 * u * u * u + c1 * u * u;
            }
            else
                eased = t < .5F
                    ? 4F * t * t * t
                    : 1F - (float)Math.Pow(-2F * t + 2F, 3F) / 2F;
            _progress = _startProgress +
                (_targetProgress - _startProgress) * eased;
            if (t >= 1F) _progress = _targetProgress;
            return true;
        }

        public SlidePanelLayout CalculateLayout()
        {
            float travel = Math.Max(0F, SlideOffset) * _progress;
            RectangleF panel = PanelBounds;
            switch (OpenDirection)
            {
                case SlidePanelOpenDirection.Left: panel.X -= travel; break;
                case SlidePanelOpenDirection.Right: panel.X += travel; break;
                case SlidePanelOpenDirection.Top: panel.Y -= travel; break;
                case SlidePanelOpenDirection.Bottom: panel.Y += travel; break;
            }
            panel.X += OpenPositionOffsetX * _progress;
            panel.Y += OpenPositionOffsetY * _progress;
            RectangleF clip = RectangleF.Empty;
            if (_progress > 0F)
            {
                if (!ClipToParent) clip = CompositionBounds;
                else if (OpenDirection == SlidePanelOpenDirection.Left)
                    clip = RectangleF.FromLTRB(
                        ParentBounds.Left - travel, CompositionBounds.Top,
                        ParentBounds.Right, CompositionBounds.Bottom);
                else if (OpenDirection == SlidePanelOpenDirection.Right)
                    clip = RectangleF.FromLTRB(
                        ParentBounds.Left, CompositionBounds.Top,
                        ParentBounds.Right + travel, CompositionBounds.Bottom);
                else if (OpenDirection == SlidePanelOpenDirection.Top)
                    clip = RectangleF.FromLTRB(
                        CompositionBounds.Left, ParentBounds.Top - travel,
                        CompositionBounds.Right, ParentBounds.Bottom);
                else
                    clip = RectangleF.FromLTRB(
                        CompositionBounds.Left, ParentBounds.Top,
                        CompositionBounds.Right, ParentBounds.Bottom + travel);
                clip.Intersect(CompositionBounds);
                RectangleF visiblePanel = panel;
                visiblePanel.Intersect(CompositionBounds);
                if (!visiblePanel.IsEmpty)
                    clip = RectangleF.Union(clip, visiblePanel);
            }
            return new SlidePanelLayout
            {
                CompositionBounds = CompositionBounds,
                ParentBounds = ParentBounds,
                PanelBounds = panel,
                RevealClip = clip,
                Progress = _progress
            };
        }

        public bool PanelAcceptsInput(PointF point)
        {
            if (_progress <= 0F) return false;
            SlidePanelLayout layout = CalculateLayout();
            return layout.RevealClip.Contains(point) &&
                layout.PanelBounds.Contains(point);
        }
    }

    public static class SlidePanelRenderer
    {
        public static void Render(
            Graphics graphics,
            SlidePanel slidePanel,
            Action<Graphics, RectangleF> drawPanel,
            Action<Graphics, RectangleF> drawParent)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            if (slidePanel == null) throw new ArgumentNullException("slidePanel");
            SlidePanelLayout layout = slidePanel.CalculateLayout();
            if (layout.Progress > 0F && drawPanel != null &&
                !layout.RevealClip.IsEmpty)
            {
                GraphicsState state = graphics.Save();
                try
                {
                    graphics.SetClip(layout.RevealClip);
                    drawPanel(graphics, layout.PanelBounds);
                }
                finally { graphics.Restore(state); }
            }
            if (drawParent != null)
                drawParent(graphics, layout.ParentBounds);
        }
    }

    public interface IWidgetInputRegionProvider
    {
        bool AcceptsInput(Point location, Size clientSize);
    }
}
