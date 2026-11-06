using System;
using System.Drawing;

namespace Taskcode.Probe
{
    internal static class UsageBarPalette
    {
        internal static readonly Color Green = Color.FromArgb(98, 208, 153);
        internal static readonly Color Yellow = Color.FromArgb(241, 198, 83);
        internal static readonly Color Red = Color.FromArgb(238, 100, 104);
        internal static Color Resolve(Color providerColor, double used, bool enabled, bool highContrast)
        {
            if (!enabled || highContrast || used < 0 || double.IsNaN(used) || double.IsInfinity(used)) return providerColor;
            double value = Math.Max(0, Math.Min(100, used));
            return value <= 50 ? UiSurface.Blend(Green, Yellow, value / 50) : UiSurface.Blend(Yellow, Red, (value - 50) / 50);
        }
    }
}
