using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: AssemblyTitle("Quota Orb")]
[assembly: AssemblyDescription("Minimal Codex quota companion")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyCopyright("Copyright © 2026 MiracleCrane")]

namespace QuotaOrb
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if(args.Length==2&&args[0]=="--snapshot"){
                using(var client=new CodexClient()){
                    var snapshot=client.Read(Settings.Load().ShowCredits);
                    File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(new {version="0.1.0",success=snapshot!=null,error=client.Error,quota=snapshot}));
                    return snapshot==null?1:0;
                }
            }
            if(args.Length==2&&args[0]=="--render"){PreviewRenderer.Render(args[1]);return 0;}
            bool demo=Array.IndexOf(args,"--demo")>=0;
            bool created;
            using(var instance=new Mutex(true,"Local\\QuotaOrb.Standalone.0",out created)){
                if(!created&&!demo)return 0;
                var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                using(var controller=new OrbController(app,demo)){
                    app.Dispatcher.BeginInvoke(new Action(controller.Start),DispatcherPriority.Loaded);
                    app.Run();
                }
            }
            return 0;
        }
    }

    internal sealed class CompanionWindow : Window
    {
        internal IntPtr Handle;
        internal CompanionWindow(string title,FrameworkElement content)
        {
            Title=title;Content=content;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
            UseLayoutRounding=true;SnapsToDevicePixels=true;
            SourceInitialized+=delegate{Handle=new WindowInteropHelper(this).Handle;Native.MakeCompanion(Handle);};
        }
    }

    internal sealed class OrbController : IDisposable
    {
        private readonly Application app;
        private readonly Settings settings=Settings.Load();
        private readonly CodexClient client=new CodexClient();
        private readonly HostWindow hostReader=new HostWindow();
        private readonly OrbFace face=new OrbFace();
        private readonly DetailView detail;
        private readonly CompanionWindow orb,card;
        private readonly DispatcherTimer timer,openTimer,closeTimer,snapTimer;
        private readonly bool demo;
        private HostFrame host;
        private QuotaSnapshot snapshot;
        private Palette palette=new Palette(true);
        private string error;
        private bool busy,disposed,pinned,pointerDown,dragging,snapping,wasVisible,refreshAgain;
        private Native.POINT downPoint;
        private DeviceRect dragStart,snapStart,snapEnd;
        private DateTime snapBegan,nextRead=DateTime.MinValue,nextStatus=DateTime.MinValue;
        private int failures;
        private int cardOpenCount,cardCloseCount;

        internal OrbController(Application app,bool demo)
        {
            this.app=app;this.demo=demo;
            detail=new DetailView(settings);var orbContainer=new Viewbox{Child=face,Stretch=Stretch.Uniform};orb=new CompanionWindow(demo?"Quota Orb · 演示":"Quota Orb",orbContainer){Width=settings.Diameter*84.0/64,Height=settings.Diameter*84.0/64};card=new CompanionWindow("Quota Orb · 额度",detail){Width=252,SizeToContent=SizeToContent.Height,Opacity=0};
            timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};timer.Tick+=delegate{Tick();};
            openTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};openTimer.Tick+=delegate{openTimer.Stop();OpenCard();};
            closeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(220)};closeTimer.Tick+=delegate{closeTimer.Stop();if(!CursorWithinCompanion()&&!pinned)CloseCard();};
            snapTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(15)};snapTimer.Tick+=delegate{AnimateSnap();};
            orb.MouseEnter+=delegate{closeTimer.Stop();if(!pointerDown)openTimer.Start();};
            orb.MouseLeave+=delegate{openTimer.Stop();closeTimer.Start();};
            card.MouseEnter+=delegate{closeTimer.Stop();};card.MouseLeave+=delegate{closeTimer.Start();};
            orb.MouseLeftButtonDown+=BeginDrag;orb.MouseMove+=MoveDrag;orb.MouseLeftButtonUp+=EndDrag;
            orb.MouseRightButtonUp+=delegate{pinned=true;OpenCard();};
            detail.OptionsChanged+=delegate{ApplyVisuals();RequestRefresh();};
            detail.RefreshRequested+=RequestRefresh;
            detail.ExitRequested+=delegate{app.Shutdown();};
            detail.SizeChangedByContent+=delegate{if(card.IsVisible)card.Dispatcher.BeginInvoke(new Action(PositionCard),DispatcherPriority.Loaded);};
            card.SizeChanged+=delegate{if(card.IsVisible)PositionCard();};
            if(demo)snapshot=QuotaSnapshot.Demo(true);
            ApplyVisuals();
        }
        internal void Start(){timer.Start();Tick();}
        private void Tick()
        {
            if(disposed)return;
            if(demo){var area=SystemParameters.WorkArea;double scale=Native.GetDpiForSystem()/96.0;host=new HostFrame{Bounds=new DeviceRect(area.Left*scale,area.Top*scale,area.Width*scale,area.Height*scale),Scale=scale,Dark=true};}
            else host=hostReader.Read(Process.GetCurrentProcess().Id);
            if(host==null){if(orb.IsVisible)orb.Hide();if(card.IsVisible)card.Hide();if(wasVisible){wasVisible=false;WriteStatus();}return;}
            bool dark=settings.Theme=="dark"||(settings.Theme=="auto"&&host.Dark);
            if(palette.Dark!=dark){palette=new Palette(dark);ApplyVisuals();}
            if(!orb.IsVisible)orb.Show();
            if(!dragging&&!snapping)Native.Move(orb.Handle,Placement.Orb(host.Bounds,settings.Side,settings.VerticalPosition,host.Scale,settings.Diameter));
            if(!wasVisible){wasVisible=true;if(DateTime.Now-nextRead>TimeSpan.FromSeconds(5))nextRead=DateTime.MinValue;}
            if(card.IsVisible){PositionCard();if(!pinned&&!pointerDown){if(CursorWithinCompanion())closeTimer.Stop();else if(!closeTimer.IsEnabled)closeTimer.Start();}}
            if(card.IsVisible&&(Native.GetAsyncKeyState(27)&0x8000)!=0){pinned=false;CloseCard();}
            if(!demo&&!busy&&DateTime.Now>=nextRead)Refresh();
            if(DateTime.Now>=nextStatus){WriteStatus();nextStatus=DateTime.Now.AddSeconds(2);}
        }
        private void Refresh()
        {
            busy=true;bool credits=settings.ShowCredits;
            DispatcherWork.Run(app.Dispatcher,delegate{return client.Read(credits);},CompleteRefresh,delegate{CompleteRefresh(null);});
        }
        private void CompleteRefresh(QuotaSnapshot data)
        {
            app.Dispatcher.VerifyAccess();busy=false;if(disposed)return;
            error=client.Error;
            if(data==null&&String.IsNullOrEmpty(error))error="额度读取失败";
            if(data!=null){snapshot=data;failures=0;}else failures++;
            nextRead=DateTime.Now.AddSeconds(data!=null?settings.RefreshSeconds:Math.Min(300,30*Math.Pow(2,Math.Min(3,failures-1))));
            if(refreshAgain){nextRead=DateTime.MinValue;refreshAgain=false;}
            ApplyVisuals();WriteStatus();
        }
        private void RequestRefresh(){if(busy)refreshAgain=true;else nextRead=DateTime.MinValue;}
        private void ApplyVisuals(){orb.Width=orb.Height=settings.Diameter*84.0/64;face.Update(snapshot,palette,!String.IsNullOrEmpty(error),settings.Motion);detail.Update(snapshot,palette,error);}
        private void OpenCard()
        {
            if(host==null||dragging)return;closeTimer.Stop();
            if(!card.IsVisible){
                cardOpenCount++;
                card.BeginAnimation(UIElement.OpacityProperty,null);card.Opacity=0;
                card.Show();PositionCard();
                card.Dispatcher.BeginInvoke(new Action(delegate{
                    if(!card.IsVisible)return;PositionCard();
                    if(settings.Motion&&SystemParameters.ClientAreaAnimation)card.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));
                    else card.Opacity=1;
                }),DispatcherPriority.Render);
            }
            else PositionCard();
        }
        private void CloseCard(){openTimer.Stop();if(card.IsVisible)cardCloseCount++;card.BeginAnimation(UIElement.OpacityProperty,null);card.Opacity=0;card.Hide();}
        private bool CursorWithinCompanion()
        {
            Native.POINT cursor;Native.GetCursorPos(out cursor);var ball=Native.Bounds(orb.Handle);
            if(cursor.X>=ball.X&&cursor.X<=ball.Right&&cursor.Y>=ball.Y&&cursor.Y<=ball.Bottom)return true;
            if(!card.IsVisible)return false;var panel=Native.Bounds(card.Handle);
            return cursor.X>=Math.Min(ball.X,panel.X)&&cursor.X<=Math.Max(ball.Right,panel.Right)&&cursor.Y>=Math.Min(ball.Y,panel.Y)&&cursor.Y<=Math.Max(ball.Bottom,panel.Bottom);
        }
        private void PositionCard()
        {
            if(host==null||card.Handle==IntPtr.Zero)return;
            detail.Measure(new Size(252,Double.PositiveInfinity));
            double height=detail.DesiredSize.Height;
            card.Width=252;card.Height=height;
            var target=Placement.Card(host.Bounds,Native.Bounds(orb.Handle),252*host.Scale,height*host.Scale,host.Scale);
            Native.Move(card.Handle,target);
        }
        private void BeginDrag(object sender,MouseButtonEventArgs e)
        {if(host==null)return;Native.GetCursorPos(out downPoint);dragStart=Native.Bounds(orb.Handle);pointerDown=true;dragging=false;openTimer.Stop();orb.CaptureMouse();e.Handled=true;}
        private void MoveDrag(object sender,MouseEventArgs e)
        {
            if(!pointerDown||host==null)return;Native.POINT point;Native.GetCursorPos(out point);double dx=point.X-downPoint.X,dy=point.Y-downPoint.Y;
            if(!dragging&&Math.Sqrt(dx*dx+dy*dy)<5*host.Scale)return;
            dragging=true;pinned=false;CloseCard();snapTimer.Stop();snapping=false;
            Native.Move(orb.Handle,new DeviceRect(Placement.Clamp(dragStart.X+dx,host.Bounds.X,host.Bounds.Right-dragStart.Width),Placement.Clamp(dragStart.Y+dy,host.Bounds.Y,host.Bounds.Bottom-dragStart.Height),dragStart.Width,dragStart.Height));
        }
        private void EndDrag(object sender,MouseButtonEventArgs e)
        {
            if(!pointerDown)return;pointerDown=false;orb.ReleaseMouseCapture();
            if(dragging&&host!=null){
                var current=Native.Bounds(orb.Handle);settings.Side=current.X+current.Width/2<host.Bounds.X+host.Bounds.Width/2?"left":"right";
                double top=host.Bounds.Y+48*host.Scale,range=host.Bounds.Bottom-6*host.Scale-current.Height-top;
                settings.VerticalPosition=Placement.Clamp((current.Y-top)/Math.Max(1,range),0,1);settings.Save();
                snapStart=current;snapEnd=Placement.Orb(host.Bounds,settings.Side,settings.VerticalPosition,host.Scale,settings.Diameter);snapBegan=DateTime.Now;
                if(settings.Motion&&SystemParameters.ClientAreaAnimation){snapping=true;snapTimer.Start();}else Native.Move(orb.Handle,snapEnd);
            }else{pinned=!pinned;if(pinned)OpenCard();else CloseCard();}
            dragging=false;e.Handled=true;
        }
        private void AnimateSnap()
        {
            double t=Math.Min(1,(DateTime.Now-snapBegan).TotalMilliseconds/180),ease=1-Math.Pow(1-t,3);
            Native.Move(orb.Handle,new DeviceRect(snapStart.X+(snapEnd.X-snapStart.X)*ease,snapStart.Y+(snapEnd.Y-snapStart.Y)*ease,snapEnd.Width,snapEnd.Height));
            if(t>=1){snapTimer.Stop();snapping=false;}
        }
        private void WriteStatus()
        {
            if(demo)return;
            try{
                Directory.CreateDirectory(Settings.DirectoryPath);
                var status=new {version="0.1.0",processId=Process.GetCurrentProcess().Id,running=!disposed,writtenAtUtc=DateTimeOffset.UtcNow,source="Codex app-server",quota=snapshot,error=error,
                    orbVisible=orb.IsVisible,panelVisible=card.IsVisible,hostFound=host!=null,scale=host==null?0:host.Scale,
                    orbBounds=orb.Handle==IntPtr.Zero?new DeviceRect():Native.Bounds(orb.Handle),dark=palette.Dark,showCredits=settings.ShowCredits,motion=settings.Motion,diameter=settings.Diameter,refreshSeconds=settings.RefreshSeconds,
                    toolWindow=orb.Handle!=IntPtr.Zero&&(Native.ExtendedStyle(orb.Handle)&0x80)!=0,taskbarEntry=orb.ShowInTaskbar,cardOpenCount=cardOpenCount,cardCloseCount=cardCloseCount};
                File.WriteAllText(Path.Combine(Settings.DirectoryPath,"status.json"),new JavaScriptSerializer().Serialize(status));
            }catch(IOException){}
        }
        public void Dispose(){disposed=true;timer.Stop();openTimer.Stop();closeTimer.Stop();snapTimer.Stop();orb.Close();card.Close();client.Dispose();}
    }

    internal static class PreviewRenderer
    {
        internal static void Render(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach(bool dark in new[]{true,false})foreach(bool both in new[]{false,true}){
                var palette=new Palette(dark);var data=QuotaSnapshot.Demo(both);var face=new OrbFace();face.Update(data,palette,false,false);
                Save(face,84,84,Path.Combine(directory,(dark?"dark":"light")+(both?"-both-orb.png":"-week-orb.png")),2);
                var view=new DetailView(new Settings());view.Update(data,palette,null);view.Measure(new Size(252,Double.PositiveInfinity));
                Save(view,252,view.DesiredSize.Height,Path.Combine(directory,(dark?"dark":"light")+(both?"-both-card.png":"-week-card.png")),2);
            }
        }
        private static void Save(FrameworkElement element,double width,double height,string path,double scale)
        {
            element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(element);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);
        }
    }
}
