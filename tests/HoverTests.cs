using System;
using QuotaOrb;

internal static class HoverTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Physical rectangles from the user's screenshot: the red box is above
        // the ball, outside the card, but inside their combined bounding box.
        var ball = new DeviceRect(8, 175, 80, 80);
        var card = new DeviceRect(95, 12, 504, 382);
        check(!HoverRegion.Contains(49, 72, ball, card), "The screenshot's red empty area must dismiss a hover card.");
        check(!HoverRegion.Contains(49, 345, ball, card), "Empty space below the orb must also dismiss the card.");
        check(HoverRegion.Contains(48, 215, ball, card), "The orb must keep its card open.");
        check(HoverRegion.Contains(200, 72, ball, card), "The card itself must remain interactive.");
        check(!HoverRegion.Contains(200, 72, ball, null), "A hidden card must not retain a hover region.");
        check(HoverRegion.Contains(91, 215, ball, card), "Crossing the narrow gap must not flicker.");
        check(!HoverRegion.Contains(91, 72, ball, card), "The narrow gap must not extend through the card's full height.");
        check(HoverRegion.Contains(91, 215, card, ball), "Crossing the left-facing gap must also work.");

        var lower = new DeviceRect(0, 270, 300, 150);
        check(HoverRegion.Contains(48, 262, ball, lower), "A card below the ball needs a vertical crossing gap.");
        check(HoverRegion.Contains(48, 262, lower, ball), "A card above the ball needs the same crossing gap.");
        check(!HoverRegion.Contains(180, 262, ball, lower), "Empty space beside a vertical gap must dismiss the card.");
        check(!HoverRegion.Contains(100, 260, ball, new DeviceRect(110, 270, 300, 150)), "Diagonal separated rectangles must not invent a broad hover region.");
        check(!HoverRegion.Contains(700, 200, ball, card), "Space past the card must dismiss it.");

        foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            var scaledBall = Transform(ball, scale);
            var scaledCard = Transform(card, scale);
            check(!HoverRegion.Contains(-1920 + 49 * scale, 72 * scale, scaledBall, scaledCard), "Empty space must stay excluded at different DPI and negative monitor coordinates.");
            check(HoverRegion.Contains(-1920 + 91 * scale, 215 * scale, scaledBall, scaledCard), "The transit gap must scale with physical bounds.");
        }
    }

    private static DeviceRect Transform(DeviceRect rect, double scale)
    {
        return new DeviceRect(-1920 + rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
    }
}
