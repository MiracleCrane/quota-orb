using System;
using System.ComponentModel;

namespace QuotaOrb
{
    public enum ForegroundRole { OtherApplication, Codex, Companion, SystemSurface, Menu }

    public static class WindowPolicy
    {
        public static ForegroundRole Foreground(bool codex, bool companion, bool systemProcess, string className)
        {
            if (className == "#32768" || (codex && className == "Chrome_WidgetWin_2")) return ForegroundRole.Menu;
            if (codex) return ForegroundRole.Codex;
            if (companion) return ForegroundRole.Companion;
            if (systemProcess || className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd" ||
                className == "Progman" || className == "WorkerW" || className == "TaskListThumbnailWnd")
                return ForegroundRole.SystemSurface;
            return ForegroundRole.OtherApplication;
        }

        public static bool IsHostWindow(string className, bool owned, long extendedStyle)
        {
            return !owned && (extendedStyle & 0x80) == 0 &&
                (className == "Chrome_WidgetWin_0" || className == "Chrome_WidgetWin_1");
        }

        public static DeviceRect Pixels(DeviceRect rect)
        {
            return new DeviceRect(Math.Round(rect.X), Math.Round(rect.Y),
                Math.Max(1, Math.Round(rect.Width)), Math.Max(1, Math.Round(rect.Height)));
        }

        public static bool SamePixels(DeviceRect current, DeviceRect desired)
        {
            var target = Pixels(desired);
            return current.X == target.X && current.Y == target.Y && current.Width == target.Width && current.Height == target.Height;
        }

        public static bool IsRecoverableWindowFailure(Exception error)
        {
            var native = error as Win32Exception;
            return native != null && native.NativeErrorCode == 1816 &&
                (native.StackTrace ?? "").IndexOf("System.Windows.Interop.HwndTarget.UpdateWindow", StringComparison.Ordinal) >= 0;
        }
    }
}
