using System;
using System.Collections.Generic;
using System.Globalization;

namespace Taskcode.Probe
{
    internal enum SpeedState { Calculating, Active, Idle, Stale, Unavailable, Expired }

    internal sealed class SpeedEstimate
    {
        internal SpeedState State;
        internal int Level;
        internal double PointsPerHour;
        internal double WindowSeconds;
        internal bool PeriodAverage;
        internal TimeSpan? TimeToExhaustion;
        internal string Summary
        {
            get
            {
                if (State == SpeedState.Idle) return PeriodAverage ? "No measured consumption yet" : "Idle · no recent consumption";
                if (State == SpeedState.Stale) return "Waiting for fresh quota readings";
                if (State == SpeedState.Expired) return "Period ended · waiting for new readings";
                if (State == SpeedState.Unavailable) return "Usage estimate unavailable";
                if (State != SpeedState.Active || !TimeToExhaustion.HasValue) return "Calculating recent usage...";
                var span = TimeToExhaustion.Value;
                if (span <= TimeSpan.Zero) return "Quota exhausted";
                string duration = span.TotalDays >= 1 ? span.TotalDays.ToString("0.0", CultureInfo.InvariantCulture) + "d" :
                    span.TotalHours >= 1 ? (int)span.TotalHours + "h " + span.Minutes + "m" : Math.Max(1, (int)Math.Ceiling(span.TotalMinutes)) + "m";
                return PeriodAverage ? "~" + duration + " left · period avg" : "~" + duration + " of use at this rate";
            }
        }
    }

    internal sealed class UsageSpeed
    {
        private sealed class Sample { internal DateTime At; internal double Remaining; }
        private readonly List<Sample> samples = new List<Sample>();
        private DateTime periodStart, periodEnd;
        private int stableLevel, pendingLevel, pendingCount;
        private bool hasBaseline;
        private readonly TimeSpan Window, freshness;
        private readonly int minimumSamples;
        internal UsageSpeed() : this(TimeSpan.FromSeconds(2)) { }
        internal UsageSpeed(TimeSpan expectedInterval)
        {
            Window = TimeSpan.FromSeconds(Math.Max(15, expectedInterval.TotalSeconds * 2));
            freshness = TimeSpan.FromSeconds(Math.Max(30, expectedInterval.TotalSeconds * 1.2));
            minimumSamples = expectedInterval.TotalSeconds > 15 ? 2 : 3;
        }

        internal bool Observe(DateTime at, double remaining, DateTime start, DateTime end)
        {
            if (at.Kind != DateTimeKind.Utc || start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || end <= start || at < start || at >= end ||
                double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0 || remaining > 100) return false;
            if (samples.Count > 0 && at <= samples[samples.Count - 1].At) return false;
            if (periodStart != start || periodEnd != end || (samples.Count > 0 &&
                (remaining > samples[samples.Count - 1].Remaining + 0.00001 || at - samples[samples.Count - 1].At > freshness)))
            { samples.Clear(); stableLevel = pendingLevel = pendingCount = 0; hasBaseline = false; }
            periodStart = start; periodEnd = end;
            samples.Add(new Sample { At = at, Remaining = remaining });
            DateTime cutoff = at - Window;
            while (samples.Count > 2 && samples[1].At <= cutoff) samples.RemoveAt(0);
            var estimate = Calculate(at);
            if (estimate.State != SpeedState.Active)
            { stableLevel = pendingLevel = pendingCount = 0; hasBaseline = estimate.State == SpeedState.Idle; return true; }
            int proposed = estimate.Level;
            if (!hasBaseline) { stableLevel = proposed; hasBaseline = true; }
            else if (proposed == stableLevel) { pendingLevel = pendingCount = 0; }
            else
            {
                if (pendingLevel != proposed) { pendingLevel = proposed; pendingCount = 1; }
                else pendingCount++;
                if (pendingCount >= 3) { stableLevel = proposed; pendingLevel = pendingCount = 0; }
            }
            return true;
        }
        internal SpeedEstimate EstimateAt(DateTime now)
        {
            var result = Calculate(now);
            if (result.State == SpeedState.Active)
            {
                result.Level = stableLevel;
                if (stableLevel == 0) result.State = SpeedState.Calculating;
            }
            return result;
        }
        internal SpeedEstimate DisplayEstimateAt(DateTime now)
        {
            var recent = EstimateAt(now);
            if (recent.State != SpeedState.Calculating || samples.Count == 0) return recent;
            var last = samples[samples.Count - 1];
            // A real quota observation can provide a labelled period average while
            // the recent estimator warms up. Never insert synthetic observations.
            double elapsed = (last.At - periodStart).TotalHours;
            if (elapsed < 5.0 / 3600) return recent;
            double used = 100 - last.Remaining;
            if (used <= 0) return new SpeedEstimate { State = SpeedState.Idle, PeriodAverage = true };
            double hours = last.Remaining / (used / elapsed);
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours > TimeSpan.MaxValue.TotalHours / 2) return recent;
            double ratio = hours / (periodEnd - now).TotalHours;
            return new SpeedEstimate { State = SpeedState.Active, PeriodAverage = true,
                PointsPerHour = used / elapsed, WindowSeconds = elapsed * 3600,
                TimeToExhaustion = TimeSpan.FromHours(hours), Level = ratio < 0.8 ? 3 : ratio <= 1.2 ? 2 : 1 };
        }
        private SpeedEstimate Calculate(DateTime now)
        {
            if (now.Kind != DateTimeKind.Utc) return new SpeedEstimate { State = SpeedState.Unavailable };
            if (samples.Count == 0) return new SpeedEstimate { State = SpeedState.Calculating };
            var last = samples[samples.Count - 1];
            if (now < last.At) return new SpeedEstimate { State = SpeedState.Unavailable };
            if (now >= periodEnd) return new SpeedEstimate { State = SpeedState.Expired };
            if (now - last.At > freshness) return new SpeedEstimate { State = SpeedState.Stale };
            if (samples.Count < minimumSamples || last.At - samples[0].At < TimeSpan.FromSeconds(5)) return new SpeedEstimate { State = SpeedState.Calculating };
            DateTime from = last.At - Window;
            double initial = samples[0].Remaining;
            if (samples[0].At > from) from = samples[0].At;
            else if (samples.Count > 1 && samples[1].At > from)
            {
                double fraction = (from - samples[0].At).TotalSeconds / (samples[1].At - samples[0].At).TotalSeconds;
                initial += fraction * (samples[1].Remaining - initial);
            }
            double rate = Math.Max(0, initial - last.Remaining) / (last.At - from).TotalHours;
            if (rate < 0.00001) return new SpeedEstimate { State = SpeedState.Idle, WindowSeconds = (last.At - from).TotalSeconds };
            double hours = last.Remaining / rate;
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours > TimeSpan.MaxValue.TotalHours / 2)
                return new SpeedEstimate { State = SpeedState.Unavailable };
            double ratio = hours / (periodEnd - now).TotalHours;
            return new SpeedEstimate { State = SpeedState.Active, PointsPerHour = rate, WindowSeconds = (last.At - from).TotalSeconds, TimeToExhaustion = TimeSpan.FromHours(hours), Level = ratio < 0.8 ? 3 : ratio <= 1.2 ? 2 : 1 };
        }
    }
}
