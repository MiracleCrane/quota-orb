using System;
using QuotaOrb;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class QuotaTests
{
    private static int assertions;
    private static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    [STAThread]
    private static void Main()
    {
        try { Run(); }
        catch (Exception error) { Console.Error.WriteLine("FAIL: "+error.Message); Environment.ExitCode=1; }
    }
    private static void Run()
    {
        var now=new DateTimeOffset(2026,10,27,0,0,0,TimeSpan.Zero);
        var onlyWeek=QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":22,\"windowDurationMins\":10080,\"resetsAt\":1793599881}}}",now);
        Check(onlyWeek.FiveHour==null,"A weekly primary must not create a 5h window.");Check(onlyWeek.Week.Remaining==78,"Remaining is 100 minus used.");
        var both=QuotaSnapshot.Parse("{\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"usedPercent\":38,\"windowDurationMins\":300},\"secondary\":{\"usedPercent\":22,\"windowDurationMins\":10080}},\"other\":{\"primary\":{\"usedPercent\":99,\"windowDurationMins\":300}}},\"rateLimits\":{\"primary\":{\"usedPercent\":50,\"windowDurationMins\":300}}}",now);
        Check(both.FiveHour.Remaining==62&&both.Week.Remaining==78,"Choose the Codex bucket, not another model or the legacy bucket.");
        bool rejected=false;try{QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300}}}",now);}catch(FormatException){rejected=true;}
        Check(rejected,"Missing usedPercent must not mean full allowance.");
        var exhausted=QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":110,\"windowDurationMins\":300}}}",now);
        Check(exhausted.FiveHour.Remaining==0,"Overage must clamp to zero.");
        var credit=QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":22,\"windowDurationMins\":10080}},\"rateLimitResetCredits\":{\"availableCount\":3,\"credits\":[{\"status\":\"redeemed\",\"expiresAt\":1794000000},{\"status\":\"available\",\"expiresAt\":1793599881}]}}",now);
        Check(credit.ResetCredits==3&&credit.PartialCreditDetails&&credit.CreditExpiry.HasValue,"Count is authoritative and incomplete detail must be marked.");
        Check(Placement.EffectiveDiameter(24,false)==24,"Single quota must support avatar size.");
        Check(Placement.EffectiveDiameter(24,true)==40,"Dual quotas must enlarge automatically to keep both visible.");
        Check(Placement.EffectiveDiameter(64,true)==64,"An explicitly larger size must be preserved.");
        var bounds=new DeviceRect(-1920,0,1920,1080);
        foreach(double scale in new[]{1.0,1.25,1.5,2.0})foreach(string side in new[]{"left","right"})foreach(int diameter in new[]{24,28,32,40,48,52,64,80})foreach(double fraction in new[]{0.0,.5,1.0}){
            var orb=Placement.Orb(bounds,side,fraction,scale,diameter);var card=Placement.Card(bounds,orb,252*scale,270*scale,scale);
            Check(orb.X>=bounds.X&&orb.Right<=bounds.Right&&orb.Y>=bounds.Y&&orb.Bottom<=bounds.Bottom,"Orb must fit on a negative-coordinate monitor.");
            Check(card.X>=bounds.X&&card.Right<=bounds.Right&&card.Y>=bounds.Y&&card.Bottom<=bounds.Bottom,"Panel must fit at edges across DPI scales.");
        }
        VerifyDispatcherAtStartup();
        var noMotionFace=new OrbFace();noMotionFace.Update(QuotaSnapshot.Demo(false),new Palette(true),false,false,32);
        Check(!noMotionFace.BreathingEnabled&&noMotionFace.BreathingOpacity==0,"Disabling motion must not leave an opaque static halo on first render.");
        VerifyBreathingAcrossQuotaFailure();
        VerifyCodexLifetime();
        VerifyWindowPolicy();
        VerifyForegroundPromotion();
        Console.WriteLine("PASS: "+assertions+" quota, credit, placement and UI dispatch assertions.");
    }
    private static void VerifyCodexLifetime()
    {
        var startup=new HostLifetime();
        Check(!startup.ShouldExit(false,TimeSpan.FromSeconds(29)),"The launcher must allow Codex time to create its window.");
        Check(startup.ShouldExit(false,TimeSpan.FromSeconds(30)),"An unused manual start must not become a resident watcher.");
        var linked=new HostLifetime();
        Check(!linked.ShouldExit(true,TimeSpan.Zero),"A visible Codex window must keep the orb alive.");
        Check(!linked.ShouldExit(true,TimeSpan.FromHours(2)),"Background or minimized Codex still has a window and must not close the orb.");
        Check(!linked.ShouldExit(false,TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1))),"A disappearing window must allow a short close grace period.");
        Check(!linked.ShouldExit(true,TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(4))),"A replacement window during the grace period must cancel the close.");
        Check(!linked.ShouldExit(false,TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(10))),"A later close must get its own grace period.");
        Check(!linked.ShouldExit(false,TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(30))),"An update taking more than five seconds must keep the hidden orb ready for the replacement window.");
        Check(linked.ShouldExit(false,TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(55)))&&linked.ExitReason=="codex-closed","A closed Codex must still stop the orb after the bounded replacement grace.");
        var update=new HostLifetime();
        Check(!update.ShouldExit(true,TimeSpan.Zero)&&!update.ShouldExit(false,TimeSpan.FromSeconds(1))&&!update.ShouldExit(true,TimeSpan.FromSeconds(40)),"A replacement Codex window must cancel update shutdown.");
    }
    private static void VerifyWindowPolicy()
    {
        Check(WindowPolicy.Foreground(false,false,false,"GameWindow")==ForegroundRole.OtherApplication,"A third-party foreground app must hide the orb regardless of tool/noactivate styles or lower z-order windows.");
        Check(WindowPolicy.Foreground(false,false,false,"Chrome_WidgetWin_1")==ForegroundRole.OtherApplication,"Another Chromium app must not be classified as Codex.");
        Check(WindowPolicy.Foreground(false,false,false,"Shell_TrayWnd")==ForegroundRole.SystemSurface,"The taskbar may inspect the underlying front application.");
        Check(WindowPolicy.Foreground(false,false,false,"CabinetWClass")==ForegroundRole.OtherApplication,"File Explorer is an ordinary application, not a taskbar surface.");
        Check(WindowPolicy.Foreground(true,false,false,"Chrome_WidgetWin_1")==ForegroundRole.Codex,"An identified Codex process may show the orb.");
        Check(WindowPolicy.SamePixels(new DeviceRect(10,20,66,66),new DeviceRect(10.5,20.5,66,66)),"Half-pixel targets rounding to the current rectangle must not repost a native move.");
        Check(!WindowPolicy.SamePixels(new DeviceRect(10,20,66,66),new DeviceRect(11,20,66,66)),"A real pixel movement must still move the orb.");
        Check(!WindowPolicy.IsRecoverableWindowFailure(new System.ComponentModel.Win32Exception(1816)),"Unrelated quota errors must not be swallowed or restarted.");
        Check(WindowPolicy.IsRecoverableWindowFailure(new WindowFailureFixture(1816)),"The observed HwndTarget message-quota failure must take the bounded recovery path.");
        Check(!WindowPolicy.IsRecoverableWindowFailure(new WindowFailureFixture(87)),"Other native window errors must retain normal exception handling.");
        foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0}){
            var target=WindowPolicy.Pixels(Placement.Card(new DeviceRect(0,0,1500,1000),new DeviceRect(10,50,66,66),Math.Round(252*scale),Math.Ceiling(223.2*scale),scale));
            Check(WindowPolicy.SamePixels(target,new DeviceRect(target.X,target.Y,(target.Width/scale)*scale,(target.Height/scale)*scale)),"Card logical dimensions and native placement must resolve to one identical pixel rectangle at each DPI.");
        }
        Check(!DesktopLaunch.RecoveryAllowed(DateTime.UtcNow,DateTime.UtcNow.AddSeconds(30)),"A repeated render failure must not create a restart loop.");
        Check(DesktopLaunch.RecoveryAllowed(DateTime.UtcNow,DateTime.UtcNow.AddMinutes(2)),"A later isolated render failure may recover.");
    }
    private sealed class WindowFailureFixture : System.ComponentModel.Win32Exception
    {
        public WindowFailureFixture(int code):base(code){}
        public override string StackTrace { get { return "at System.Windows.Interop.HwndTarget.UpdateWindowSettings(Boolean)"; } }
    }
    private static void VerifyForegroundPromotion()
    {
        var owner=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();int calls=0;
        var ticks=new DispatcherTicks(owner,delegate{calls++;frame.Continue=false;});
        for(int i=0;i<1000;i++)ticks.Request(false);
        Check(ticks.PendingPriority==DispatcherPriority.Background,"Location bursts must coalesce below foreground input.");
        ticks.Request(true);ticks.Request(false);
        Check(ticks.PendingPriority==DispatcherPriority.Input,"A foreground event must promote an already queued geometry tick.");
        var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
        timeout.Tick+=delegate{timeout.Stop();frame.Continue=false;};timeout.Start();
        Dispatcher.PushFrame(frame);timeout.Stop();
        Check(calls==1,"A location burst followed by focus change must execute one promoted tick.");
    }
    private static void VerifyBreathingAcrossQuotaFailure()
    {
        var face=new OrbFace();var palette=new Palette(true);var retained=QuotaSnapshot.Demo(false);
        retained.Week.Remaining=53;
        face.Update(retained,palette,false,true,32);
        if(!System.Windows.SystemParameters.ClientAreaAnimation||System.Windows.SystemParameters.HighContrast)return;
        Check(face.BreathingEnabled,"The initial valid snapshot must start breathing.");
        face.Update(retained,palette,true,true,32);
        Check(face.BreathingEnabled,"A transient quota refresh failure must not stop breathing while retained data stays visible.");
        face.Update(retained,palette,false,true,32);
        Check(face.BreathingEnabled,"Recovering a quota read must preserve breathing.");
        face.Update(retained,palette,true,false,32);
        Check(!face.BreathingEnabled,"The explicit motion switch must still stop breathing during a read failure.");
    }
    private static void VerifyDispatcherAtStartup()
    {
        var owner=Dispatcher.CurrentDispatcher;
        var element=new Border();
        var previous=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        var frame=new DispatcherFrame();bool completed=false,failed=false;
        var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};
        timeout.Tick+=delegate{timeout.Stop();frame.Continue=false;};timeout.Start();
        DispatcherWork.Run(owner,delegate{return 52;},delegate(int value){Check(owner.CheckAccess(),"Refresh completion must run on the UI thread without a captured synchronization context.");element.Width=value;completed=true;frame.Continue=false;},delegate{failed=true;frame.Continue=false;});
        Dispatcher.PushFrame(frame);timeout.Stop();SynchronizationContext.SetSynchronizationContext(previous);
        Check(completed&&!failed&&element.Width==52,"Startup refresh must be able to update a real WPF object.");
    }
}
