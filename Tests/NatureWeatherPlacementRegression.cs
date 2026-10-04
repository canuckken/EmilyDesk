using System;
using System.Drawing;
using XWidgetReborn.Runtime.Core;

namespace EmilyDesk.Tests
{
    public static class NatureWeatherPlacementRegression
    {
        private static int _checks;

        private static void Equal(Point expected, Point actual, string name)
        {
            if (expected != actual)
                throw new InvalidOperationException(name + ": expected " + expected +
                    ", received " + actual);
            _checks++;
        }

        private static Point Move(Point host, int dx, int dy, Rectangle anchor,
            Rectangle area, bool snap, bool keep, bool cancelled)
        {
            Point pointer = new Point(area.Left + 420, area.Top + 250);
            return NatureWeatherPlacement.EndDrag(host, pointer,
                new Point(pointer.X + dx, pointer.Y + dy), new Size(4, 4),
                anchor, area, snap, keep, cancelled);
        }

        public static int Run()
        {
            _checks = 0;
            foreach (double scale in new double[] { .5, .75, 1, 1.25, 1.5, 1.75, 2 })
            foreach (double dpi in new double[] { 1, 1.25, 1.5, 2 })
            foreach (Point origin in new Point[] {
                new Point(0, 0), new Point(-2560, 0), new Point(0, -2160) })
            {
                Rectangle area = new Rectangle(origin.X, origin.Y,
                    (int)(1920 * dpi), (int)(1040 * dpi));
                Rectangle anchor = new Rectangle((int)(520 * scale * dpi),
                    (int)(150 * scale * dpi), (int)(550 * scale * dpi),
                    (int)(330 * scale * dpi));
                Point atTop = new Point(area.Left + 100 - anchor.X, area.Top - anchor.Y);
                Point nearTop = new Point(atTop.X, atTop.Y + 8);
                Equal(nearTop, Move(atTop, 0, 8, anchor, area, true, true, false), "short detach");
                Equal(nearTop, Move(nearTop, 0, 0, anchor, area, true, true, false), "second click");
                Equal(nearTop, Move(nearTop, 1, 1, anchor, area, true, true, false), "click jitter");
                Equal(new Point(nearTop.X, nearTop.Y + 8),
                    Move(nearTop, 0, 8, anchor, area, true, true, false), "second downward drag");
                Equal(new Point(nearTop.X + 30, nearTop.Y),
                    Move(nearTop, 30, 0, anchor, area, true, true, false), "horizontal drag preserves gap");
                Equal(atTop, Move(nearTop, 0, -4, anchor, area, true, true, false), "intentional reattach");
                Equal(nearTop, NatureWeatherPlacement.Stationary(nearTop, anchor, area,
                    false, true), "reload preserves near-edge gap");
                Equal(atTop, NatureWeatherPlacement.Stationary(nearTop, anchor, area,
                    true, true), "explicit snap near edge");

                Point far = new Point(atTop.X, atTop.Y + 70);
                Equal(far, NatureWeatherPlacement.Stationary(far, anchor, area,
                    true, true), "negative host is not a screen edge");
                Equal(far, Move(far, 0, 0, anchor, area, true, true, false), "far second click");
                Equal(new Point(far.X, atTop.Y + 40),
                    Move(far, 0, -30, anchor, area, true, true, false), "approach outside band");
                Equal(atTop, Move(far, 0, -60, anchor, area, true, true, false), "approach within band");
                Equal(new Point(far.X, atTop.Y + 10),
                    Move(far, 0, -60, anchor, area, false, true, false), "snap disabled");
                Equal(far, Move(far, 80, 80, anchor, area, true, true, true), "cancel restores position");
                Equal(atTop, Move(atTop, 0, -40, anchor, area, false, true, false), "visible top clamp");
                Equal(new Point(atTop.X, atTop.Y - 40),
                    Move(atTop, 0, -40, anchor, area, false, false, false), "both options disabled");

                Point atLeft = new Point(area.Left - anchor.X, area.Top + 100 - anchor.Y);
                Point nearLeft = new Point(atLeft.X + 8, atLeft.Y);
                Equal(nearLeft, Move(atLeft, 8, 0, anchor, area, true, true, false), "left detach");
                Equal(nearLeft, Move(nearLeft, 0, 0, anchor, area, true, true, false), "left click");
                Equal(atLeft, Move(nearLeft, -4, 0, anchor, area, true, true, false), "left reattach");
                Point atRight = new Point(area.Right - anchor.Right, atLeft.Y);
                Point nearRight = new Point(atRight.X - 8, atRight.Y);
                Equal(nearRight, Move(atRight, -8, 0, anchor, area, true, true, false), "right detach");
                Equal(nearRight, Move(nearRight, 0, 0, anchor, area, true, true, false), "right click");
                Equal(atRight, Move(nearRight, 4, 0, anchor, area, true, true, false), "right reattach");
                Point atBottom = new Point(atTop.X, area.Bottom - anchor.Bottom);
                Point nearBottom = new Point(atBottom.X, atBottom.Y - 8);
                Equal(nearBottom, Move(atBottom, 0, -8, anchor, area, true, true, false), "bottom detach");
                Equal(nearBottom, Move(nearBottom, 0, 0, anchor, area, true, true, false), "bottom click");
                Equal(atBottom, Move(nearBottom, 0, 4, anchor, area, true, true, false), "bottom reattach");
                Point corner = new Point(atRight.X, atTop.Y);
                Equal(new Point(corner.X - 8, corner.Y + 8),
                    Move(corner, -8, 8, anchor, area, true, true, false), "corner detach");
            }
            return _checks;
        }
    }
}
