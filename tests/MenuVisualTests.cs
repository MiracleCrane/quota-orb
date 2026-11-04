using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaOrb;

internal static class MenuVisualTests
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(bool dark in new[]{true,false}){
            var palette=new Palette(dark);
            var menu=PositionMenu.Create(new Settings(),palette,delegate{},delegate{});
            check(menu.OverridesDefaultStyle&&menu.Template!=null,"The popup must replace the system template that adds an icon gutter.");
            var stack=new StackPanel();
            foreach(object value in menu.Items.Cast<object>().ToArray()){
                menu.Items.Remove(value);
                var item=(Control)value;
                item.Style=(Style)menu.Resources[item is MenuItem?typeof(MenuItem):typeof(Separator)];
                stack.Children.Add(item);
            }
            var surface=new Border{Child=stack,Width=menu.MinWidth,Padding=menu.Padding,Background=menu.Background,
                BorderBrush=menu.BorderBrush,BorderThickness=menu.BorderThickness,CornerRadius=new CornerRadius(7)};
            surface.SetValue(TextElement.FontFamilyProperty,menu.FontFamily);
            surface.SetValue(TextElement.FontSizeProperty,menu.FontSize);
            surface.Measure(new Size(menu.MinWidth,Double.PositiveInfinity));
            surface.Arrange(new Rect(new Point(),surface.DesiredSize));surface.UpdateLayout();
            var first=(MenuItem)stack.Children[0];
            var row=(Border)first.Template.FindName("ItemRow",first);
            var text=(ContentPresenter)row.Child;
            double textLeft=text.TransformToAncestor(surface).Transform(new Point()).X;
            check(textLeft>=10&&textLeft<=18,"Menu labels must start at the text padding without an empty icon column.");
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth*1.5),(int)Math.Ceiling(surface.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
            using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,dark?"menu-dark.png":"menu-light.png")))png.Save(file);
        }
    }
}
