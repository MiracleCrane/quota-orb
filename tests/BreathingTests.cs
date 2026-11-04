using System;
using System.Web.Script.Serialization;
using QuotaOrb;

internal static class BreathingTests
{
    internal static void Run(Action<bool,string> check)
    {
        check(!BreathingPolicy.IsEnabled(true,false,false,null),"An untouched profile must continue to follow disabled system animation.");
        check(BreathingPolicy.IsEnabled(true,false,false,true),"An explicit in-app choice must enable breathing with Windows client animation disabled.");
        check(!BreathingPolicy.IsEnabled(true,true,false,false),"The in-app off choice must stop breathing even when system animation is enabled.");
        check(!BreathingPolicy.IsEnabled(true,true,true,true),"High contrast must still suppress decorative breathing.");
        var oldProfile=new JavaScriptSerializer().Deserialize<Settings>("{\"Motion\":true,\"Diameter\":36}");
        check(!oldProfile.BreathingEnabled.HasValue,"Older profiles must retain their system-following animation preference.");
        oldProfile.BreathingEnabled=true;
        var restored=new JavaScriptSerializer().Deserialize<Settings>(new JavaScriptSerializer().Serialize(oldProfile));
        check(restored.BreathingEnabled==true&&restored.Diameter==36,"The explicit breathing preference must persist without resetting size.");
        if(!System.Windows.SystemParameters.HighContrast){
            var face=new OrbFace();
            face.Update(QuotaSnapshot.Demo(false),new Palette(true),false,false,36,true);
            check(face.BreathingEnabled,"The real orb visual must start the explicit breathing animation independently of legacy motion.");
            face.Update(QuotaSnapshot.Demo(false),new Palette(true),true,false,36,true);
            check(face.BreathingEnabled,"A stale read must not cancel explicitly enabled breathing.");
            face.Update(QuotaSnapshot.Demo(false),new Palette(true),false,true,36,false);
            check(!face.BreathingEnabled&&face.BreathingOpacity==0,"Turning breathing off must clear the decorative halo.");
        }
    }
}
