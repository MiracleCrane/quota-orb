using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuotaOrb
{
    internal static class PositionMenu
    {
        internal static ContextMenu Create(Settings settings, Palette palette, Action changed, Action showDetails)
        {
            var menu = new ContextMenu { FontFamily = new FontFamily("Segoe UI"), FontSize = 12, StaysOpen = false,
                Background = palette.Surface, Foreground = palette.Text, BorderBrush = palette.Border };
            menu.Items.Add(Item(settings.PositionLocked ? "解锁拖动" : "锁定位置", delegate {
                settings.PositionLocked = !settings.PositionLocked; changed();
            }));
            menu.Items.Add(Item("还原默认位置", delegate { settings.RestoreDefaultPosition(); changed(); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("显示额度详情", showDetails));
            return menu;
        }

        private static MenuItem Item(string title, Action action)
        {
            var item = new MenuItem { Header = title };
            item.Click += delegate { action(); };
            return item;
        }
    }
}
