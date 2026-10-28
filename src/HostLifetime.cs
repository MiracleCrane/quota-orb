using System;

namespace QuotaOrb
{
    public sealed class HostLifetime
    {
        private bool hasSeenWindow;
        private TimeSpan? missingSince;
        public string ExitReason { get; private set; }

        public bool ShouldExit(bool windowExists, TimeSpan elapsed)
        {
            if (windowExists) { hasSeenWindow = true; missingSince = null; return false; }
            if (!hasSeenWindow) {
                if (elapsed < TimeSpan.FromSeconds(30)) return false;
                ExitReason = "codex-not-started";
                return true;
            }
            if (!missingSince.HasValue) missingSince = elapsed;
            // Hide immediately, but keep this bounded window-replacement grace for desktop updates.
            if (elapsed - missingSince.Value < TimeSpan.FromSeconds(45)) return false;
            ExitReason = "codex-closed";
            return true;
        }
    }
}
