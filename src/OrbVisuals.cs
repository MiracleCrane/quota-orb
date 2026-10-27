using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace QuotaOrb
{
    internal sealed class Palette
    {
        internal Brush Surface, Text, Muted, Border, Track, Ring, Inner, Warning;
        internal bool Dark;
        internal Palette(bool dark)
        {
            Dark = dark;
            Surface = ColorBrush(dark ? "#242628" : "#FAFAFA");
            Text = ColorBrush(dark ? "#E4E6E8" : "#2B2D30");
            Muted = ColorBrush(dark ? "#969BA1" : "#6F747A");
            Border = ColorBrush(dark ? "#36393C" : "#E0E2E5");
            Track = ColorBrush(dark ? "#3C4044" : "#E1E4E7");
            Ring = ColorBrush(dark ? "#CFD3D8" : "#59616A");
            Inner = ColorBrush(dark ? "#89939E" : "#8C98A3");
            Warning = ColorBrush(dark ? "#C6A366" : "#997133");
            if (SystemParameters.HighContrast) {
                Surface = SystemColors.WindowBrush; Text = SystemColors.WindowTextBrush; Muted = Text;
                Border = Text; Track = SystemColors.GrayTextBrush; Ring = Text; Inner = Text; Warning = Text;
            }
        }
        internal static Brush ColorBrush(string value) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); b.Freeze(); return b; }
    }

    internal sealed class OrbFace : FrameworkElement
    {
        private QuotaSnapshot snapshot;
        private Palette palette = new Palette(true);
        private bool stale;
        private readonly DrawingGroup ring = new DrawingGroup();
        private readonly DrawingGroup halo = new DrawingGroup();
        private readonly ScaleTransform haloScale = new ScaleTransform(1, 1, 42, 42);
        private bool motionActive;
        private int diameter = 64;
        private double center = 36;
        internal OrbFace() { Width = Height = Placement.OrbExtent(64); Focusable = false; halo.Transform = haloScale; }
        internal void Update(QuotaSnapshot data, Palette colors, bool failed, bool motion, int size = 64)
        {
            snapshot=data;palette=colors;stale=failed;
            diameter=Placement.EffectiveDiameter(size,data!=null&&data.FiveHour!=null&&data.Week!=null);
            Width=Height=Placement.OrbExtent(diameter);center=Width/2;
            haloScale.CenterX=haloScale.CenterY=center;
            double radius=diameter/2.0-1.3;
            using(var drawing=ring.Open()) {
                double outer=data != null && data.Week != null ? data.Week.Remaining : data != null && data.FiveHour != null ? data.FiveHour.Remaining : 0;
                DrawArc(drawing,radius,outer,palette.Ring,palette.Track,diameter<32?1.0:1.4);
                if(data != null && data.Week != null && data.FiveHour != null) DrawArc(drawing,radius-3.2,data.FiveHour.Remaining,data.FiveHour.Remaining<=20?palette.Warning:palette.Inner,palette.Track,1.0);
            }
            ring.Opacity=stale ? 0.42 : 1;
            using(var glow=halo.Open())glow.DrawEllipse(null,new Pen(palette.Ring,.7),new Point(center,center),diameter/2.0+.6,diameter/2.0+.6);
            bool animate=motion && !stale && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
            if(animate!=motionActive){
                motionActive=animate;
                halo.BeginAnimation(DrawingGroup.OpacityProperty,null);haloScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);haloScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
                halo.Opacity=0;haloScale.ScaleX=haloScale.ScaleY=1;
                if(animate){
                    var easing=new SineEase{EasingMode=EasingMode.EaseInOut};
                    halo.BeginAnimation(DrawingGroup.OpacityProperty,new DoubleAnimation(.06,.26,TimeSpan.FromSeconds(2.4)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=easing});
                    var expansion=new DoubleAnimation(1,1.06,TimeSpan.FromSeconds(2.4)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=easing};
                    haloScale.BeginAnimation(ScaleTransform.ScaleXProperty,expansion);haloScale.BeginAnimation(ScaleTransform.ScaleYProperty,expansion);
                }
            }
            AutomationProperties.SetName(this, data==null?"额度尚未读取":(data.FiveHour!=null?"5h剩余"+data.FiveHour.Percentage+"，":"")+(data.Week!=null?"周剩余"+data.Week.Percentage:""));
            InvalidateVisual();
        }
        private void DrawArc(DrawingContext d,double radius,double remaining,Brush color,Brush track,double thickness)
        {
            var origin=new Point(center,center);d.DrawEllipse(null,new Pen(track,thickness),origin,radius,radius);
            if(remaining<=0)return;
            double sweep=Math.Min(99.999,remaining)*Math.PI*2/100;
            var start=new Point(center,center-radius);var end=new Point(center+radius*Math.Sin(sweep),center-radius*Math.Cos(sweep));
            var geometry=new StreamGeometry();
            using(var g=geometry.Open()){g.BeginFigure(start,false,false);g.ArcTo(end,new Size(radius,radius),0,sweep>Math.PI,SweepDirection.Clockwise,true,false);}
            geometry.Freeze();var pen=new Pen(color,thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
            d.DrawGeometry(null,pen,geometry);
        }
        protected override void OnRender(DrawingContext d)
        {
            d.DrawDrawing(halo);
            double radius=diameter/2.0;
            d.DrawEllipse(new SolidColorBrush(Color.FromArgb(18,0,0,0)),null,new Point(center,center+.6),radius+.6,radius+.6);
            d.DrawEllipse(palette.Surface,null,new Point(center,center),radius,radius);d.DrawDrawing(ring);
            if(snapshot==null){DrawCentered(d,"—",Math.Max(11,diameter*.3),palette.Muted,center,true);return;}
            if(snapshot.FiveHour!=null&&snapshot.Week!=null){DrawRow(d,"5h",snapshot.FiveHour.Percentage,center-7);DrawRow(d,"周",snapshot.Week.Percentage,center+7);}
            else{
                var value=snapshot.Week??snapshot.FiveHour;
                if(diameter<40)DrawCompactPercentage(d,value.Percentage);
                else{DrawCentered(d,value.Label,10.5,palette.Muted,center-12,true);DrawCentered(d,value.Percentage,Math.Min(19,diameter*.38),palette.Text,center+5,true);}
            }
        }
        private void DrawRow(DrawingContext d,string label,string value,double y)
        {
            double small=diameter<48?8.5:10.5,large=diameter<48?10.5:12.5;
            var left=Text(label,small,palette.Muted);var right=Text(value,large,palette.Text);
            double available=diameter-8-left.Width-2;if(right.Width>available)right=Text(value,large*available/right.Width,palette.Text);
            double x=(Width-left.Width-right.Width-2)/2;
            d.DrawText(left,new Point(x,y-left.Height/2));d.DrawText(right,new Point(x+left.Width+2,y-right.Height/2));
        }
        private void DrawCompactPercentage(DrawingContext d,string value)
        {
            double size=Math.Max(11.5,diameter*.4),suffixSize=size*.6;
            var number=Text(value.TrimEnd('%'),size,palette.Text);var suffix=Text("%",suffixSize,palette.Muted);
            double available=diameter-3.5,used=number.Width+suffix.Width+.5;
            if(used>available){double ratio=available/used;number=Text(value.TrimEnd('%'),size*ratio,palette.Text);suffix=Text("%",suffixSize*ratio,palette.Muted);}
            double x=(Width-number.Width-suffix.Width-.5)/2,y=center-number.Height/2;
            d.DrawText(number,new Point(x,y));d.DrawText(suffix,new Point(x+number.Width+.5,y+number.Baseline-suffix.Baseline));
        }
        private void DrawCentered(DrawingContext d,string text,double size,Brush brush,double y,bool middle){var formatted=Text(text,size,brush);d.DrawText(formatted,new Point((Width-formatted.Width)/2,middle?y-formatted.Height/2:y));}
        private FormattedText Text(string text,double size,Brush brush){return new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);}
    }

    internal sealed class DetailView : Border
    {
        internal Action OptionsChanged, RefreshRequested, ExitRequested, SizeChangedByContent;
        private readonly Settings settings;
        private StackPanel body, options;
        private Palette palette;
        private bool optionsOpen;
        private QuotaSnapshot snapshot;
        private string error;
        internal DetailView(Settings settings){this.settings=settings;Width=252;Padding=new Thickness(16,13,16,15);CornerRadius=new CornerRadius(13);BorderThickness=new Thickness(1);}
        internal void Update(QuotaSnapshot data,Palette colors,string failure)
        {
            snapshot=data;palette=colors;error=failure;
            Background=palette.Surface;BorderBrush=palette.Border;body=new StackPanel();Child=body;
            var header=new DockPanel {Margin=new Thickness(0,0,0,14)};
            var menu=ActionButton("···",delegate{optionsOpen=!optionsOpen;RenderOptions();});menu.Width=26;AutomationProperties.SetName(menu,"显示选项");DockPanel.SetDock(menu,Dock.Right);header.Children.Add(menu);
            header.Children.Add(Label("剩余额度",12,palette.Muted));body.Children.Add(header);
            if(data==null)body.Children.Add(Label(String.IsNullOrEmpty(error)?"正在读取…":error,12,palette.Muted));
            else{
                if(data.FiveHour!=null)AddMeter(data.FiveHour,"5 小时");
                if(data.Week!=null)AddMeter(data.Week,"周额度");
                if(settings.ShowCredits){
                    body.Children.Add(Separator());var row=new DockPanel();var count=Label(data.ResetCredits.HasValue?data.ResetCredits+" 张":"暂不可用",13,palette.Text);DockPanel.SetDock(count,Dock.Right);row.Children.Add(count);row.Children.Add(Label("重置券",12,palette.Muted));body.Children.Add(row);
                    if(data.ResetCredits>0)body.Children.Add(Label(data.CreditExpiry.HasValue?(data.PartialCreditDetails?"已知最近到期：":"最近到期：")+data.CreditExpiry.Value.LocalDateTime.ToString("M 月 d 日 HH:mm"):"到期明细暂不可用",11,palette.Muted,new Thickness(0,8,0,0)));
                }
                if(!String.IsNullOrEmpty(error)){body.Children.Add(Separator());body.Children.Add(Label("刷新失败 · 保留上次读数",11,palette.Muted));body.Children.Add(Label("更新于 "+data.ObservedAt.LocalDateTime.ToString("HH:mm:ss"),11,palette.Muted,new Thickness(0,4,0,0)));}
            }
            options=new StackPanel();body.Children.Add(options);RenderOptions();
        }
        private void AddMeter(QuotaWindow value,string caption)
        {
            var block=new StackPanel {Margin=new Thickness(0,0,0,13)};var row=new DockPanel();
            var percent=Label(value.Percentage,14,palette.Text);percent.FontWeight=FontWeights.Medium;DockPanel.SetDock(percent,Dock.Right);row.Children.Add(percent);row.Children.Add(Label(caption,13,palette.Text));block.Children.Add(row);
            var track=new Border{Height=3,CornerRadius=new CornerRadius(2),Background=palette.Track,Margin=new Thickness(0,9,0,0)};
            track.Child=new Border{Width=218*value.Remaining/100,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(2),Background=value.Remaining<=20?palette.Warning:palette.Ring};block.Children.Add(track);
            block.Children.Add(Label(value.ResetText(DateTimeOffset.Now),11.5,palette.Muted,new Thickness(0,8,0,0)));body.Children.Add(block);
        }
        private void RenderOptions()
        {
            if(options==null)return;options.Children.Clear();
            if(optionsOpen){
                options.Children.Add(Separator());
                AddOption("重置券与到期时间",settings.ShowCredits,delegate{settings.ShowCredits=!settings.ShowCredits;Changed();});
                AddOption("轻微呼吸动画",settings.Motion,delegate{settings.Motion=!settings.Motion;Changed();});
                var sizeRow=new DockPanel{Margin=new Thickness(0,5,0,5)};
                var increase=ActionButton("＋",delegate{settings.Diameter=Math.Min(80,settings.Diameter+4);Changed();});increase.Width=28;AutomationProperties.SetName(increase,"放大悬浮球");DockPanel.SetDock(increase,Dock.Right);sizeRow.Children.Add(increase);
                var decrease=ActionButton("−",delegate{settings.Diameter=Math.Max(24,settings.Diameter-4);Changed();});decrease.Width=28;AutomationProperties.SetName(decrease,"缩小悬浮球");DockPanel.SetDock(decrease,Dock.Right);sizeRow.Children.Add(decrease);
                sizeRow.Children.Add(Label("大小："+settings.Diameter+" px",11.5,palette.Muted,new Thickness(3,5,0,0)));options.Children.Add(sizeRow);
                if(settings.Diameter<40&&snapshot!=null&&snapshot.FiveHour!=null&&snapshot.Week!=null)options.Children.Add(Label("双额度自动放大到 40px",11,palette.Muted,new Thickness(3,0,0,5)));
                options.Children.Add(ActionButton("刷新："+settings.RefreshSeconds+" 秒",delegate{settings.RefreshSeconds=settings.RefreshSeconds==15?30:settings.RefreshSeconds==30?60:15;Changed();}));
                var theme=ActionButton("外观："+(settings.Theme=="auto"?"跟随 Codex":settings.Theme=="dark"?"深色":"浅色"),delegate{settings.Theme=settings.Theme=="auto"?"dark":settings.Theme=="dark"?"light":"auto";Changed();});options.Children.Add(theme);
                var actions=new DockPanel {Margin=new Thickness(0,8,0,0)};var quit=ActionButton("退出",delegate{if(ExitRequested!=null)ExitRequested();});DockPanel.SetDock(quit,Dock.Right);actions.Children.Add(quit);actions.Children.Add(ActionButton("立即刷新",delegate{if(RefreshRequested!=null)RefreshRequested();}));options.Children.Add(actions);
            }
            if(SizeChangedByContent!=null)SizeChangedByContent();
        }
        private void Changed(){settings.Save();if(OptionsChanged!=null)OptionsChanged();}
        private void AddOption(string text,bool on,Action action)
        {
            var button=ActionButton(text+"    "+(on?"开":"关"),action);button.HorizontalContentAlignment=HorizontalAlignment.Left;AutomationProperties.SetName(button,text+(on?"，已开启":"，已关闭"));options.Children.Add(button);
        }
        private Button ActionButton(string text,Action action)
        {
            var button=new Button {Content=text,Foreground=palette.Text,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(3,5,3,5),FontSize=11.5,FontFamily=new FontFamily("Segoe UI"),Cursor=System.Windows.Input.Cursors.Hand};
            var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Button.BackgroundProperty));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Button.PaddingProperty));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));border.AppendChild(presenter);template.VisualTree=border;button.Template=template;
            button.MouseEnter+=delegate{button.Background=palette.Track;};button.MouseLeave+=delegate{button.Background=Brushes.Transparent;};button.Click+=delegate{action();};return button;
        }
        private UIElement Separator(){return new Border{Height=1,Background=palette.Border,Margin=new Thickness(0,2,0,12)};}
        private static TextBlock Label(string text,double size,Brush color){return Label(text,size,color,new Thickness(0));}
        private static TextBlock Label(string text,double size,Brush color,Thickness margin){return new TextBlock{Text=text,FontSize=size,Foreground=color,Margin=margin,TextWrapping=TextWrapping.Wrap,FontFamily=new FontFamily("Segoe UI")};}
    }
}
