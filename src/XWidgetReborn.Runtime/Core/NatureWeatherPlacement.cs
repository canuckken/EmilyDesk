using System;
using System.Drawing;

namespace XWidgetReborn.Runtime.Core
{
    // Pure screen-coordinate rules, shared by the live drag and build tests.
    // Transparent host margins must never become magnetic screen-edge zones.
    internal static class NatureWeatherPlacement
    {
        internal const int SnapDistance = 18;

        internal static Point PointerLocation(Point startHost,
            Point startPointer, Point pointer)
        {
            return new Point(startHost.X + pointer.X - startPointer.X,
                startHost.Y + pointer.Y - startPointer.Y);
        }

        internal static bool IsDrag(Point startPointer, Point pointer, Size dragSize)
        {
            return Math.Abs(pointer.X - startPointer.X) >= Math.Max(2, dragSize.Width) ||
                Math.Abs(pointer.Y - startPointer.Y) >= Math.Max(2, dragSize.Height);
        }

        internal static Point EndDrag(Point startHost, Point startPointer,
            Point pointer, Size dragSize, Rectangle anchor, Rectangle workArea,
            bool snap, bool keepOnScreen, bool cancelled)
        {
            // A click (including the next click after a short detach) is not
            // a placement request. Preserve it exactly, even inside the band.
            if (cancelled || !IsDrag(startPointer, pointer, dragSize))
                return startHost;

            Point requested = PointerLocation(startHost, startPointer, pointer);
            int dx = pointer.X - startPointer.X;
            int dy = pointer.Y - startPointer.Y;
            return AlignVisible(requested, anchor, workArea,
                snap && dx < 0, snap && dx > 0,
                snap && dy < 0, snap && dy > 0, keepOnScreen);
        }

        internal static Point Stationary(Point host, Rectangle anchor,
            Rectangle workArea, bool explicitSnap, bool keepOnScreen)
        {
            // Launch and scale changes preserve a saved near-edge gap.
            // Only explicitly enabling Snap requests stationary attraction.
            return AlignVisible(host, anchor, workArea,
                explicitSnap, explicitSnap, explicitSnap, explicitSnap, keepOnScreen);
        }

        private static Point AlignVisible(Point host, Rectangle anchor,
            Rectangle workArea, bool left, bool right, bool top, bool bottom,
            bool keepOnScreen)
        {
            Rectangle visible = new Rectangle(host.X + anchor.X, host.Y + anchor.Y,
                anchor.Width, anchor.Height);
            int x = host.X;
            int y = host.Y;
            if ((keepOnScreen && visible.Left < workArea.Left) ||
                (left && Math.Abs(visible.Left - workArea.Left) <= SnapDistance))
                x += workArea.Left - visible.Left;
            else if ((keepOnScreen && visible.Right > workArea.Right) ||
                (right && Math.Abs(visible.Right - workArea.Right) <= SnapDistance))
                x += workArea.Right - visible.Right;
            if ((keepOnScreen && visible.Top < workArea.Top) ||
                (top && Math.Abs(visible.Top - workArea.Top) <= SnapDistance))
                y += workArea.Top - visible.Top;
            else if ((keepOnScreen && visible.Bottom > workArea.Bottom) ||
                (bottom && Math.Abs(visible.Bottom - workArea.Bottom) <= SnapDistance))
                y += workArea.Bottom - visible.Bottom;
            return new Point(x, y);
        }
    }
}
