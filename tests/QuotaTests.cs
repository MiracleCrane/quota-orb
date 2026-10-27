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
        Console.WriteLine("PASS: "+assertions+" quota, credit, placement and UI dispatch assertions.");
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
