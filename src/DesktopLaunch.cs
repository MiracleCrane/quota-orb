using System;
using System.IO;

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
    }
}
