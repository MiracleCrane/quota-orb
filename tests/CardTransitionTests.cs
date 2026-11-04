using System;
using System.Windows;
using System.Windows.Threading;
using QuotaOrb;

internal static class CardTransitionTests
{
    internal static void Run(Action<bool, string> check)
    {
        var card = new Window { Width = 40, Height = 40, Left = -10000, Top = -10000,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        int hides = 0;
        var transition = new CardTransition(card, delegate { hides++; });
        try
        {
            transition.Show(false, delegate { }); Pump(30);
            transition.Close(true);
            check(card.IsVisible && transition.IsClosing, "Closing with motion must fade before hiding the card.");
            Pump(55);
            check(card.IsVisible && card.Opacity > 0 && card.Opacity < 1, "A real WPF card must pass through intermediate opacity while closing.");
            double interruptedOpacity = card.Opacity;
            transition.Show(true, delegate { });
            check(!transition.IsClosing && Math.Abs(card.Opacity - interruptedOpacity) < .02, "Pointer re-entry must reverse from current opacity without jumping.");
            Pump(230);
            check(card.IsVisible && card.Opacity > .99 && hides == 0, "An interrupted fade-out must not hide a reopened card later.");
            transition.Close(true); Pump(230);
            check(!card.IsVisible && hides == 1, "An uninterrupted fade-out must finish by hiding the native window.");
            transition.Show(true, delegate { }); transition.HideImmediately(); Pump(200);
            check(!card.IsVisible && card.Opacity == 0, "A host switch must cancel even a queued fade-in.");
            transition.Show(false, delegate { }); Pump(30);
            transition.Close(false);
            check(!card.IsVisible && !transition.IsClosing, "Reduced motion must still close immediately.");
        }
        finally { transition.HideImmediately(); card.Close(); }
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
    }
}
