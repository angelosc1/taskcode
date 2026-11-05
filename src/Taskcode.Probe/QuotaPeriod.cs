using System;

namespace Taskcode.Probe
{
    internal sealed class QuotaPeriod
    {
        internal DateTime StartUtc, EndUtc;
        internal int Remaining;
        internal double UsedFraction { get { return 1 - Math.Max(0, Math.Min(100, Remaining)) / 100.0; } }
        internal double? ElapsedAt(DateTime nowUtc)
        {
            if (StartUtc.Kind != DateTimeKind.Utc || EndUtc.Kind != DateTimeKind.Utc || nowUtc.Kind != DateTimeKind.Utc || EndUtc <= StartUtc) return null;
            return Math.Max(0, Math.Min(1, (nowUtc - StartUtc).TotalSeconds / (EndUtc - StartUtc).TotalSeconds));
        }
        internal string PaceAt(DateTime nowUtc)
        {
            var elapsed = ElapsedAt(nowUtc);
            if (!elapsed.HasValue) return "Timing unavailable";
            if (nowUtc >= EndUtc) return "Sample period ended";
            double difference = UsedFraction - elapsed.Value;
            return Math.Abs(difference) <= 0.02 ? "On budget pace" : difference > 0 ? "Above budget pace" : "Below budget pace";
        }
        internal string CaptionAt(DateTime nowUtc)
        {
            var elapsed = ElapsedAt(nowUtc);
            if (!elapsed.HasValue) return "Timing unavailable";
            string time = Math.Floor(elapsed.Value * 100) + "% time";
            var remaining = EndUtc - nowUtc;
            if (remaining <= TimeSpan.Zero) return time + " · Sample period ended";
            string reset = remaining.TotalDays >= 1 ? (int)remaining.TotalDays + "d " + remaining.Hours + "h" :
                remaining.TotalHours >= 1 ? (int)remaining.TotalHours + "h " + remaining.Minutes + "m" :
                Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)) + "m";
            return time + " · resets in " + reset;
        }
        internal string ResetCaptionAt(DateTime nowUtc)
        {
            if (!ElapsedAt(nowUtc).HasValue) return "Reset time unavailable";
            var remaining = EndUtc - nowUtc;
            if (remaining <= TimeSpan.Zero) return "Waiting for reset";
            string reset = remaining.TotalDays >= 1 ? (int)remaining.TotalDays + "d " + remaining.Hours + "h" :
                remaining.TotalHours >= 1 ? (int)remaining.TotalHours + "h " + remaining.Minutes + "m" : Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)) + "m";
            return "Resets in " + reset;
        }
    }
}
