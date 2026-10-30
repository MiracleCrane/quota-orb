using System;

namespace QuotaOrb
{
    public static class HoverRegion
    {
        public static bool Contains(double x, double y, DeviceRect ball, DeviceRect? panel)
        {
            if (Inside(x, y, ball)) return true;
            if (!panel.HasValue) return false;
            var card = panel.Value;
            if (Inside(x, y, card)) return true;
            // Keep only the gap between facing edges, restricted to their
            // shared span. Empty corners of the bounding box are outside.
            double top = Math.Max(ball.Y, card.Y), bottom = Math.Min(ball.Bottom, card.Bottom);
            if (ball.Right <= card.X && Inside(x, y, new DeviceRect(ball.Right, top, card.X - ball.Right, bottom - top))) return true;
            if (card.Right <= ball.X && Inside(x, y, new DeviceRect(card.Right, top, ball.X - card.Right, bottom - top))) return true;
            double left = Math.Max(ball.X, card.X), right = Math.Min(ball.Right, card.Right);
            if (ball.Bottom <= card.Y && Inside(x, y, new DeviceRect(left, ball.Bottom, right - left, card.Y - ball.Bottom))) return true;
            return card.Bottom <= ball.Y && Inside(x, y, new DeviceRect(left, card.Bottom, right - left, ball.Y - card.Bottom));
        }

        private static bool Inside(double x, double y, DeviceRect rect)
        {
            return rect.Width > 0 && rect.Height > 0 && x >= rect.X && x <= rect.Right && y >= rect.Y && y <= rect.Bottom;
        }
    }
}
