using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuotaOrb
{
    public struct DeviceRect
    {
        public double X, Y, Width, Height;
        public DeviceRect(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
        public double Right { get { return X + Width; } }
        public double Bottom { get { return Y + Height; } }
    }

    public static class Placement
    {
        public static double Clamp(double value, double min, double max) { return Math.Max(min, Math.Min(Math.Max(min, max), value)); }
        public static DeviceRect Orb(DeviceRect host, string side, double fraction, double scale, int diameter = 64)
        {
            double size = diameter * 84.0 / 64 * scale, gap = 4 * scale, top = host.Y + 48 * scale;
            double x = side == "left" ? host.X + gap : host.Right - size - gap;
            double y = top + Math.Max(0, host.Bottom - gap - size - top) * Clamp(fraction, 0, 1);
            return new DeviceRect(Clamp(x, host.X, host.Right - size), Clamp(y, host.Y, host.Bottom - size), size, size);
        }
        public static DeviceRect Card(DeviceRect host, DeviceRect orb, double width, double height, double scale)
        {
            double gap = 6 * scale, margin = 8 * scale;
            width = Math.Min(width, Math.Max(120 * scale, host.Width - 2 * margin));
            double x, y;
            if (orb.X - host.X >= width + gap) { x = orb.X - width - gap; y = orb.Y + (orb.Height - height) / 2; }
            else if (host.Right - orb.Right >= width + gap) { x = orb.Right + gap; y = orb.Y + (orb.Height - height) / 2; }
            else { x = orb.X + (orb.Width - width) / 2; y = orb.Bottom + gap + height < host.Bottom - margin ? orb.Bottom + gap : orb.Y - height - gap; }
            return new DeviceRect(Clamp(x, host.X + margin, host.Right - width - margin), Clamp(y, host.Y + margin, host.Bottom - height - margin), width, height);
        }
    }

    internal sealed class HostFrame
    {
        public IntPtr Handle;
        public DeviceRect Bounds;
        public double Scale;
        public bool Dark;
    }

    internal sealed class HostWindow
    {
        private readonly HashSet<int> codexProcesses = new HashSet<int>();
        private DateTime nextProcessScan;
        private IntPtr lastHost;
        private DateTime nextThemeRead;
        private bool dark = true;

        public HostFrame Read(int ownProcessId)
        {
            if (DateTime.UtcNow >= nextProcessScan)
            {
                codexProcesses.Clear();
                foreach (var process in Process.GetProcessesByName("ChatGPT"))
                {
                    try { if (process.MainModule.FileName.IndexOf("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) >= 0) codexProcesses.Add(process.Id); }
                    catch { }
                    finally { process.Dispose(); }
                }
                nextProcessScan = DateTime.UtcNow.AddSeconds(3);
            }
            IntPtr active = Native.GetForegroundWindow();
            uint activePid; Native.GetWindowThreadProcessId(active, out activePid);
            if (codexProcesses.Contains((int)activePid)) lastHost = Native.GetAncestor(active, 2);
            else if (activePid != ownProcessId && !IsFrontApplication(ownProcessId)) return null;
            if (lastHost == IntPtr.Zero || !Native.IsWindowVisible(lastHost) || Native.IsIconic(lastHost)) return null;
            uint hostPid; Native.GetWindowThreadProcessId(lastHost, out hostPid);
            if (!codexProcesses.Contains((int)hostPid)) return null;
            var bounds = Native.Bounds(lastHost);
            if (bounds.Width < 150 || bounds.Height < 180) return null;
            double scale = Native.GetDpiForWindow(lastHost) / 96.0;
            if (scale <= 0) scale = 1;
            if (DateTime.UtcNow >= nextThemeRead)
            {
                // One pixel of the host's empty right margin; no screen image or text is retained.
                IntPtr dc = Native.GetDC(lastHost);
                if (dc != IntPtr.Zero)
                {
                    uint color = Native.GetPixel(dc, Math.Max(1, (int)bounds.Width - (int)(15 * scale)), (int)(72 * scale));
                    Native.ReleaseDC(lastHost, dc);
                    if (color != 0xffffffff) dark = ((color & 255) * .2126 + ((color >> 8) & 255) * .7152 + ((color >> 16) & 255) * .0722) < 128;
                }
                nextThemeRead = DateTime.UtcNow.AddSeconds(2);
            }
            return new HostFrame { Handle = lastHost, Bounds = bounds, Scale = scale, Dark = dark };
        }

        private bool IsFrontApplication(int ownProcessId)
        {
            bool found = false;
            Native.EnumWindows(delegate(IntPtr window, IntPtr state) {
                if (!Native.IsWindowVisible(window) || Native.IsIconic(window)) return true;
                uint pid; Native.GetWindowThreadProcessId(window, out pid);
                if (pid == ownProcessId) return true;
                if ((Native.ExtendedStyle(window) & (0x80 | 0x08000000)) != 0) return true;
                var className = new StringBuilder(128); Native.GetClassName(window, className, className.Capacity);
                string name = className.ToString();
                if (name == "Shell_TrayWnd" || name == "Shell_SecondaryTrayWnd" || name == "Progman" || name == "WorkerW" || name == "TaskListThumbnailWnd") return true;
                int cloaked; if (Native.DwmGetWindowAttribute(window, 14, out cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
                var bounds = Native.Bounds(window); if (bounds.Width < 150 || bounds.Height < 180) return true;
                if (codexProcesses.Contains((int)pid)) { lastHost = window; found = true; return false; }
                try {
                    using (var candidate = Process.GetProcessById((int)pid)) {
                        string processName = candidate.ProcessName;
                        if (processName == "StartMenuExperienceHost" || processName == "SearchHost" || processName == "ShellExperienceHost" || processName == "TextInputHost") return true;
                    }
                } catch { return true; }
                // The first ordinary application in z-order is not Codex.
                return false;
            }, IntPtr.Zero);
            return found;
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern uint GetDpiForSystem();
        internal delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern uint GetPixel(IntPtr dc, int x, int y);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint="GetWindowLongW")] private static extern int GetWindowLong32(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint="SetWindowLongW")] private static extern int SetWindowLong32(IntPtr window, int index, int value);
        internal static DeviceRect Bounds(IntPtr window)
        { RECT r; GetWindowRect(window, out r); return new DeviceRect(r.Left, r.Top, r.Right-r.Left, r.Bottom-r.Top); }
        internal static void MakeCompanion(IntPtr window)
        {
            long style = ExtendedStyle(window);
            style |= 0x08000000L | 0x00000080L;
            style &= ~0x00040000L;
            if (IntPtr.Size == 8) SetWindowLongPtr64(window, -20, new IntPtr(style)); else SetWindowLong32(window, -20, (int)style);
        }
        internal static long ExtendedStyle(IntPtr window) { return IntPtr.Size == 8 ? GetWindowLongPtr64(window, -20).ToInt64() : GetWindowLong32(window, -20); }
        internal static void Move(IntPtr window, DeviceRect rect)
        {
            var current = Bounds(window);
            if (Math.Abs(current.X - rect.X) < .5 && Math.Abs(current.Y - rect.Y) < .5 && Math.Abs(current.Width - rect.Width) < .5 && Math.Abs(current.Height - rect.Height) < .5) return;
            SetWindowPos(window, new IntPtr(-1), (int)Math.Round(rect.X), (int)Math.Round(rect.Y), (int)Math.Round(rect.Width), (int)Math.Round(rect.Height), 0x0010);
        }
    }
}
