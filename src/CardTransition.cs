using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace QuotaOrb
{
    internal sealed class CardTransition
    {
        private readonly Window card;
        private readonly Action hidden;
        private int generation;
        internal bool IsClosing { get; private set; }

        internal CardTransition(Window card, Action hidden) { this.card = card; this.hidden = hidden; }

        internal void Show(bool animate, Action position)
        {
            int request = ++generation;
            IsClosing = false;
            FreezeOpacity();
            if (!card.IsVisible) { card.Opacity = 0; card.Show(); }
            position();
            card.Dispatcher.BeginInvoke(new Action(delegate {
                if (request != generation || !card.IsVisible) return;
                position();
                FadeTo(1, animate, null);
            }), DispatcherPriority.Render);
        }

        internal void Close(bool animate)
        {
            if (!card.IsVisible || IsClosing) return;
            IsClosing = true;
            int request = ++generation;
            FadeTo(0, animate, delegate {
                if (request == generation && IsClosing) HideImmediately();
            });
        }

        internal void HideImmediately()
        {
            ++generation;
            IsClosing = false;
            card.BeginAnimation(UIElement.OpacityProperty, null);
            card.Opacity = 0;
            bool visible = card.IsVisible;
            card.Hide();
            if (visible && hidden != null) hidden();
        }

        private void FreezeOpacity()
        {
            double current = card.Opacity;
            card.BeginAnimation(UIElement.OpacityProperty, null);
            card.Opacity = current;
        }

        private void FadeTo(double opacity, bool animate, Action completed)
        {
            FreezeOpacity();
            if (!animate) { card.Opacity = opacity; if (completed != null) completed(); return; }
            var fade = new DoubleAnimation(card.Opacity, opacity, TimeSpan.FromMilliseconds(150)) {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            if (completed != null) fade.Completed += delegate { completed(); };
            card.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }
}
