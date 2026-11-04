using System;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using QuotaOrb;

internal static class PositionTests
{
    internal static void Run(Action<bool,string> check)
    {
        var settings=new JavaScriptSerializer().Deserialize<Settings>("{\"Diameter\":36,\"Side\":\"right\",\"VerticalPosition\":0.9,\"ShowCredits\":true}");
        check(settings.PositionLocked&&settings.UseOfficialPosition&&settings.Diameter==36,"An existing profile must adopt the locked official anchor while retaining its chosen size.");
        int changes=0;
        var menu=PositionMenu.Create(settings,new Palette(true),delegate{changes++;},delegate{});
        check((string)((MenuItem)menu.Items[0]).Header=="解锁拖动","A locked orb must offer drag unlocking.");
        ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(!settings.PositionLocked&&settings.UseOfficialPosition&&changes==1,"Unlocking must keep the anchored position until the user actually drags.");
        settings.UseOfficialPosition=false;settings.Diameter=64;
        menu=PositionMenu.Create(settings,new Palette(true),delegate{changes++;},delegate{});
        check((string)((MenuItem)menu.Items[0]).Header=="锁定位置","An unlocked orb must offer position locking.");
        ((MenuItem)menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(settings.PositionLocked&&settings.UseOfficialPosition&&settings.Diameter==64&&settings.ShowCredits,"Restoring position must relock the official anchor without resetting size or quota display settings.");
        var loaded=new JavaScriptSerializer().Deserialize<Settings>(new JavaScriptSerializer().Serialize(settings));
        check(loaded.PositionLocked&&loaded.UseOfficialPosition&&loaded.Diameter==64,"Position preferences must survive a settings round trip.");
        foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0})foreach(int diameter in new[]{24,36,40,64,80}){
            var client=new DeviceRect(-1500,80,1200,1000);
            var rect=Placement.OfficialOrb(client,scale,diameter);
            check(Math.Abs(rect.X+rect.Width/2-(client.X+26*scale))<0.001,"Resizing must keep the official usage center horizontally fixed at each DPI.");
            check(Math.Abs(rect.Y+rect.Height/2-(client.Bottom-66*scale))<0.001,"Resizing must keep the official usage center vertically fixed at each DPI.");
        }
    }
}
