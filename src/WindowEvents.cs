using System;
using System.Runtime.InteropServices;

namespace QuotaOrb
{
    internal sealed class WindowEvents : IDisposable
    {
        private delegate void WinEventCallback(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time);
        private readonly WinEventCallback callback;
        private readonly Action<uint> changed;
        private IntPtr foreground, minimize, location, visibility, host;
        internal bool ForegroundHookActive { get { return foreground != IntPtr.Zero; } }

        internal WindowEvents(Action<uint> changed)
        {
            this.changed = changed;
            callback = OnEvent;
            // Out-of-context observation only. No input hooks or injected code.
            foreground = SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 2);
            minimize = SetWinEventHook(0x16, 0x17, IntPtr.Zero, callback, 0, 0, 2);
        }
        internal void Track(IntPtr window)
        {
            if (window == host) return;
            if (location != IntPtr.Zero) UnhookWinEvent(location);
            if (visibility != IntPtr.Zero) UnhookWinEvent(visibility);
            host = window; location = visibility = IntPtr.Zero;
            if (window == IntPtr.Zero) return;
            uint pid; Native.GetWindowThreadProcessId(window, out pid);
            location = SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, callback, pid, 0, 2);
            visibility = SetWinEventHook(0x8001, 0x8003, IntPtr.Zero, callback, pid, 0, 2);
        }
        private void OnEvent(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time)
        {
            if (kind == 0x800B && (window != host || objectId != 0)) return;
            if (kind >= 0x8001 && kind <= 0x8003 && (window != host || objectId != 0)) return;
            changed(kind == 3 || kind == 0x17 ? time : 0);
        }
        public void Dispose()
        {
            foreach (IntPtr hook in new[] { foreground, minimize, location, visibility }) if (hook != IntPtr.Zero) UnhookWinEvent(hook);
            foreground = minimize = location = visibility = IntPtr.Zero;
        }
        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventCallback callback, uint processId, uint threadId, uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    }
}
