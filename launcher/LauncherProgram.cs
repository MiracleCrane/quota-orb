using System;
using System.Diagnostics;
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
                string original = OriginalShortcut(args);
                // Codex is the required operation. Do not put its startup behind
                // Explorer COM, the quota executable, or quota/account settings.
                using (Process.Start(new ProcessStartInfo(original) {
                    UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(original)
                })) { }
                try {
                    string companion = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QuotaOrb.exe");
                    try { DesktopLaunch.Start(companion, "", AppDomain.CurrentDomain.BaseDirectory); }
                    catch (Exception) {
                        // Explorer automation is optional too. A shortcut-launched
                        // wrapper can use the normal shell even if COM is unavailable.
                        using (Process.Start(new ProcessStartInfo(companion) {
                            UseShellExecute = true, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                        })) { }
                    }
                } catch (Exception error) {
                    // An optional companion failure must never become a Codex error dialog.
                    try {
                        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuotaOrb");
                        Directory.CreateDirectory(directory);
                        File.AppendAllText(Path.Combine(directory, "launcher.log"), DateTimeOffset.UtcNow.ToString("o") +
                            " companion-start-failed type=" + error.GetType().Name + " code=" + error.HResult + Environment.NewLine);
                    } catch (Exception) { }
                }
                return 0;
            } catch (Exception error) {
                MessageBox.Show(error.Message, "Codex 启动联动", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
        }

        private static string OriginalShortcut(string[] args)
        {
            string original;
            if (args.Length == 2 && args[0] == "--shortcut") original = Path.GetFullPath(args[1]);
            else {
                string config;
                if (args.Length == 2 && args[0] == "--config") config = Path.GetFullPath(args[1]);
                else if (args.Length == 2 && args[0] == "--link" && System.Text.RegularExpressions.Regex.IsMatch(args[1], "^[a-f0-9]{16}$"))
                    config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuotaOrb", "launchers", args[1] + ".json");
                else throw new InvalidOperationException("请通过已配置的 Codex 快捷方式启动，或使用 Quota Orb 手动开关。");
                var settings = new JavaScriptSerializer().Deserialize<LauncherSettings>(File.ReadAllText(config));
                original = settings == null ? null : settings.OriginalShortcut;
            }
            if (String.IsNullOrWhiteSpace(original) || !original.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || !File.Exists(original))
                throw new InvalidOperationException("找不到保存的 Codex 原始快捷方式，请重新安装联动。");
            return original;
        }
    }
}
