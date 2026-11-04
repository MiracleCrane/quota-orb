using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace QuotaOrb
{
    internal static class PositionMenu
    {
        internal static ContextMenu Create(Settings settings, Palette palette, Action changed, Action showDetails)
        {
            var menu = new ContextMenu { FontFamily = new FontFamily("Segoe UI"), FontSize = 12, StaysOpen = false,
                Background = palette.Surface, Foreground = palette.Text, BorderBrush = palette.Border,
                MinWidth = 152, Padding = new Thickness(4), BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true, OverridesDefaultStyle = true, Template = MenuTemplate() };
            menu.Resources.Add(typeof(MenuItem), ItemStyle(palette));
            menu.Resources.Add(typeof(Separator), SeparatorStyle(palette));
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

        private static ControlTemplate MenuTemplate()
        {
            var template = new ControlTemplate(typeof(ContextMenu));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            template.VisualTree = border;
            return template;
        }

        private static Style ItemStyle(Palette palette)
        {
            var style = new Style(typeof(MenuItem));
            style.Setters.Add(new Setter(Control.OverridesDefaultStyleProperty, true));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.Text));
            var template = new ControlTemplate(typeof(MenuItem));
            var row = new FrameworkElementFactory(typeof(Border), "ItemRow");
            row.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            row.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            row.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            row.AppendChild(content);
            template.VisualTree = row;
            var hover = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty,
                SystemParameters.HighContrast ? SystemColors.HighlightBrush : palette.Track, "ItemRow"));
            if (SystemParameters.HighContrast) hover.Setters.Add(new Setter(Control.ForegroundProperty, SystemColors.HighlightTextBrush));
            template.Triggers.Add(hover);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            template.Triggers.Add(disabled);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static Style SeparatorStyle(Palette palette)
        {
            var style = new Style(typeof(Separator));
            var template = new ControlTemplate(typeof(Separator));
            var line = new FrameworkElementFactory(typeof(Border));
            line.SetValue(Border.BackgroundProperty, palette.Border);
            line.SetValue(FrameworkElement.HeightProperty, 1.0);
            line.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 4, 8, 4));
            template.VisualTree = line;
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }
    }
}
