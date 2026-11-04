namespace QuotaOrb
{
    public static class BreathingPolicy
    {
        public static bool IsEnabled(bool legacyMotion, bool systemAnimation, bool highContrast, bool? preference)
        {
            return !highContrast && (preference ?? (legacyMotion && systemAnimation));
        }
    }
}
