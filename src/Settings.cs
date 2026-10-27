using System;
using System.IO;
using System.Web.Script.Serialization;

namespace QuotaOrb
{
    public sealed class Settings
    {
        public bool ShowCredits = false;
        public bool Motion = true;
        public string Theme = "auto";
        public string Side = "right";
        public double VerticalPosition = 0.52;
        public int RefreshSeconds = 15;
        public int Diameter = 52;
        public string CodexPath = "";
        public static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuotaOrb"); } }
        public static Settings Load()
        {
            try {
                var settings = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(Path.Combine(DirectoryPath, "settings.json")));
                settings.RefreshSeconds = Math.Max(15, Math.Min(300, settings.RefreshSeconds));
                settings.Diameter = Math.Max(48, Math.Min(80, settings.Diameter));
                settings.VerticalPosition = Math.Max(0, Math.Min(1, settings.VerticalPosition));
                if (settings.Side != "left") settings.Side = "right";
                if (settings.Theme != "dark" && settings.Theme != "light") settings.Theme = "auto";
                return settings;
            } catch { return new Settings(); }
        }
        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            string target = Path.Combine(DirectoryPath, "settings.json"), temporary = target + ".new";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(this));
            if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
        }
    }
}
