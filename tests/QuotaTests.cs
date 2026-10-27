using System;
using QuotaOrb;

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
        var bounds=new DeviceRect(-1920,0,1920,1080);
        foreach(double scale in new[]{1.0,1.25,1.5,2.0})foreach(string side in new[]{"left","right"})foreach(int diameter in new[]{48,52,64,80})foreach(double fraction in new[]{0.0,.5,1.0}){
            var orb=Placement.Orb(bounds,side,fraction,scale,diameter);var card=Placement.Card(bounds,orb,252*scale,270*scale,scale);
            Check(orb.X>=bounds.X&&orb.Right<=bounds.Right&&orb.Y>=bounds.Y&&orb.Bottom<=bounds.Bottom,"Orb must fit on a negative-coordinate monitor.");
            Check(card.X>=bounds.X&&card.Right<=bounds.Right&&card.Y>=bounds.Y&&card.Bottom<=bounds.Bottom,"Panel must fit at edges across DPI scales.");
        }
        Console.WriteLine("PASS: "+assertions+" quota, credit and placement assertions.");
    }
}
