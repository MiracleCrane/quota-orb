using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using QuotaOrb;

internal static class NativeMenuTests
{
    internal static void Run(Action<bool, string> check)
    {
        bool observed = false;
        IntPtr menu = CreatePopupMenu();
        using (var window = new HwndSource(new HwndSourceParameters("Quota Orb menu regression") {
            Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000)
        })) {
            AppendMenu(menu, 0, new UIntPtr(1), "Quota Orb test menu");
            var reader = new HostWindow();
            var worker = new Thread(delegate() {
                try {
                    for (int attempt = 0; attempt < 80; attempt++) {
                        if (Native.MenuIsOpen()) {
                            observed = reader.Read(System.Diagnostics.Process.GetCurrentProcess().Id) == null && reader.MenuOpen;
                            break;
                        }
                        Thread.Sleep(25);
                    }
                } finally { PostMessage(window.Handle, 0x1f, IntPtr.Zero, IntPtr.Zero); }
            });
            worker.Start();
            try { TrackPopupMenuEx(menu, 0x100, 8, 8, window.Handle, IntPtr.Zero); }
            finally { worker.Join(3000); reader.Dispose(); DestroyMenu(menu); }
        }
        check(observed, "A real native tray-style popup must suppress the companion even when the main app remains foreground.");
        check(!Native.MenuIsOpen(), "Closing the popup must release menu suppression so the orb can return.");
    }

    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
    [DllImport("user32.dll")] private static extern bool TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
}
