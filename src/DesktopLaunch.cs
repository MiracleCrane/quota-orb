using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.ComponentModel;

namespace QuotaOrb
{
    public static class DesktopLaunch
    {
        public static void Start(string executable, string arguments, string workingDirectory)
        {
            string path = Path.GetFullPath(executable);
            if (!File.Exists(path)) throw new FileNotFoundException("找不到启动程序。", path);
            // Route through the existing desktop's dispatch object. A newly created
            // Shell.Application object alone can still launch under the caller's job.
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            dynamic windows = shell.Windows();
            int desktopHandle = 0;
            dynamic desktop = windows.FindWindowSW(0, 0, 8, ref desktopHandle, 1);
            if (desktop == null) throw new InvalidOperationException("Windows 桌面会话不可用，请从桌面快捷方式启动。");
            desktop.Document.Application.ShellExecute(path, arguments ?? "", workingDirectory ?? Path.GetDirectoryName(path), "open", 0);
        }

        public static bool RecoveryAllowed(DateTime lastRecovery, DateTime now)
        {
            return now - lastRecovery >= TimeSpan.FromMinutes(1);
        }

        public static void RecoverAfterExit(int processId)
        {
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QuotaOrb.exe");
            try {
                using (var previous = Process.GetProcessById(processId)) {
                    try {
                        if (!previous.HasExited && !String.Equals(previous.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase)) return;
                        if (!previous.WaitForExit(10000)) return;
                    } catch (InvalidOperationException) {
                        try { if (!previous.HasExited) return; } catch { return; }
                    } catch (Win32Exception) {
                        try { if (!previous.HasExited) return; } catch { return; }
                    }
                }
            } catch (ArgumentException) { }
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuotaOrb");
            Directory.CreateDirectory(directory);
            string marker = Path.Combine(directory, "last-render-recovery.utc");
            DateTime previousTime;
            if (File.Exists(marker) && DateTime.TryParse(File.ReadAllText(marker), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out previousTime) && !RecoveryAllowed(previousTime.ToUniversalTime(), DateTime.UtcNow)) return;
            File.WriteAllText(marker, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            Start(executable, "", AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
