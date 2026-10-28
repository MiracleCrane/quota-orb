using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace QuotaOrb
{
    internal sealed class LauncherSettings
    {
        public string OriginalShortcut { get; set; }
    }

    internal static class LauncherProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try {
                int previousProcess;
                if (args.Length == 2 && args[0] == "--recover" && Int32.TryParse(args[1], out previousProcess) && previousProcess > 0) {
                    try { DesktopLaunch.RecoverAfterExit(previousProcess); } catch (Exception) { return 1; }
                    return 0;
                }
                string config;
                if (args.Length == 2 && args[0] == "--config") config = Path.GetFullPath(args[1]);
                else if (args.Length == 2 && args[0] == "--link" && System.Text.RegularExpressions.Regex.IsMatch(args[1], "^[a-f0-9]{16}$"))
                    config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuotaOrb", "launchers", args[1] + ".json");
                else throw new InvalidOperationException("请通过已配置的 Codex 快捷方式启动，或使用 Quota Orb 手动开关。");
                var settings = new JavaScriptSerializer().Deserialize<LauncherSettings>(File.ReadAllText(config));
                if (settings == null || String.IsNullOrWhiteSpace(settings.OriginalShortcut) || !settings.OriginalShortcut.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("原始启动快捷方式配置无效，请重新安装联动。");
                DesktopLaunch.Start(settings.OriginalShortcut, "", Path.GetDirectoryName(settings.OriginalShortcut));
                DesktopLaunch.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QuotaOrb.exe"), "", AppDomain.CurrentDomain.BaseDirectory);
                return 0;
            } catch (Exception error) {
                MessageBox.Show(error.Message, "Codex 启动联动", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
        }
    }
}
