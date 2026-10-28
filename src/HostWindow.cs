using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public static int EffectiveDiameter(int preference, bool dualQuota) { return Math.Max(dualQuota ? 40 : 24, Math.Min(80, preference)); }
        public static double OrbExtent(int diameter) { return diameter + (diameter < 32 ? 4 : 8); }
        public static double Clamp(double value, double min, double max) { return Math.Max(min, Math.Min(Math.Max(min, max), value)); }
        public static DeviceRect Orb(DeviceRect host, string side, double fraction, double scale, int diameter = 64)
        {
            double size = OrbExtent(diameter) * scale, gap = 4 * scale, top = host.Y + 48 * scale;
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

    internal sealed class HostWindow : IDisposable
    {
        private volatile HashSet<int> codexProcesses = new HashSet<int>();
        private volatile HashSet<int> shellProcesses = new HashSet<int>();
        private DateTime nextProcessScan;
        private IntPtr lastHost;
        private DateTime nextThemeRead;
        private volatile bool dark = true;
        private volatile bool disposed;
        private int scanning, samplingTheme;
        internal event Action Changed;

        public HostFrame Read(int ownProcessId)
        {
            if (disposed) return null;
            if (DateTime.UtcNow >= nextProcessScan) QueueProcessScan();
            IntPtr active = Native.GetForegroundWindow();
            uint activePid; Native.GetWindowThreadProcessId(active, out activePid);
            var activeClass = new StringBuilder(128); Native.GetClassName(active, activeClass, activeClass.Capacity);
            var role = WindowPolicy.Foreground(codexProcesses.Contains((int)activePid), activePid == ownProcessId,
                shellProcesses.Contains((int)activePid), activeClass.ToString());
            if (role == ForegroundRole.Codex) lastHost = Native.GetAncestor(active, 2);
            else if (role == ForegroundRole.OtherApplication || !IsFrontApplication(ownProcessId)) return null;
            if (lastHost == IntPtr.Zero || !Native.IsWindowVisible(lastHost) || Native.IsIconic(lastHost)) return null;
            uint hostPid; Native.GetWindowThreadProcessId(lastHost, out hostPid);
            if (!codexProcesses.Contains((int)hostPid)) return null;
            var bounds = Native.Bounds(lastHost);
            if (bounds.Width < 150 || bounds.Height < 180) return null;
            double scale = Native.GetDpiForWindow(lastHost) / 96.0;
            if (scale <= 0) scale = 1;
            if (DateTime.UtcNow >= nextThemeRead) QueueThemeRead(lastHost, bounds, scale);
            return new HostFrame { Handle = lastHost, Bounds = bounds, Scale = scale, Dark = dark };
        }

        internal bool HasCodexWindow()
        {
            bool found = false;
            Native.EnumWindows(delegate(IntPtr window, IntPtr state) {
                if (!Native.IsWindowVisible(window)) return true;
                uint pid; Native.GetWindowThreadProcessId(window, out pid);
                if (!codexProcesses.Contains((int)pid)) return true;
                if ((Native.ExtendedStyle(window) & 0x80) != 0) return true;
                var name = new StringBuilder(128); Native.GetClassName(window, name, name.Capacity);
                if (!name.ToString().StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal)) return true;
                // A minimized window still exists. Closing its last root window does not.
                found = true; return false;
            }, IntPtr.Zero);
            return found;
        }

        private void QueueProcessScan()
        {
            nextProcessScan = DateTime.UtcNow.AddSeconds(3);
            if (Interlocked.CompareExchange(ref scanning, 1, 0) != 0) return;
            Task.Run(delegate {
                try {
                    var applications = new HashSet<int>();
                    foreach (var process in Process.GetProcessesByName("ChatGPT")) {
                        try { if (process.MainModule.FileName.IndexOf("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) >= 0) applications.Add(process.Id); }
                        catch { }
                        finally { process.Dispose(); }
                    }
                    var shell = new HashSet<int>();
                    foreach (string name in new[] { "StartMenuExperienceHost", "SearchHost", "ShellExperienceHost", "TextInputHost" })
                        foreach (var process in Process.GetProcessesByName(name)) { shell.Add(process.Id); process.Dispose(); }
                    bool changed = !applications.SetEquals(codexProcesses);
                    codexProcesses = applications; shellProcesses = shell;
                    if (changed) NotifyChanged();
                } catch { }
                finally { Interlocked.Exchange(ref scanning, 0); }
            });
        }

        private void QueueThemeRead(IntPtr window, DeviceRect bounds, double scale)
        {
            nextThemeRead = DateTime.UtcNow.AddSeconds(2);
            if (Interlocked.CompareExchange(ref samplingTheme, 1, 0) != 0) return;
            Task.Run(delegate {
                IntPtr dc = IntPtr.Zero;
                try {
                    // Sample only one empty-margin pixel, off the UI thread.
                    dc = Native.GetDC(window);
                    if (dc == IntPtr.Zero) return;
                    uint color = Native.GetPixel(dc, Math.Max(1, (int)bounds.Width - (int)(15 * scale)), (int)(72 * scale));
                    if (color == 0xffffffff) return;
                    bool next = ((color & 255) * .2126 + ((color >> 8) & 255) * .7152 + ((color >> 16) & 255) * .0722) < 128;
                    if (next != dark) { dark = next; NotifyChanged(); }
                } finally {
                    if (dc != IntPtr.Zero) Native.ReleaseDC(window, dc);
                    Interlocked.Exchange(ref samplingTheme, 0);
                }
            });
        }
        private void NotifyChanged() { var handler = Changed; if (!disposed && handler != null) handler(); }
        public void Dispose() { disposed = true; }

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
                if (shellProcesses.Contains((int)pid)) return true;
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
            rect = WindowPolicy.Pixels(rect);
            var current = Bounds(window);
            if (WindowPolicy.SamePixels(current, rect)) return;
            uint flags = 0x0010 | 0x0004 | 0x0200; // Do not activate or reorder either window.
            if (current.Width == rect.Width && current.Height == rect.Height) flags |= 0x0001;
            if (current.X == rect.X && current.Y == rect.Y) flags |= 0x0002;
            SetWindowPos(window, IntPtr.Zero, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, flags);
        }
    }
}
