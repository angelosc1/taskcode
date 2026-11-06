using System;
using System.Collections.Generic;

namespace Taskcode.Probe
{
    internal sealed class TrendPoint
    {
        internal DateTime At;
        internal double Used;
    }
    // In-memory observations only: drawing and cached reads never create samples.
    internal sealed class QuotaTrend
    {
        private readonly List<TrendPoint> points = new List<TrendPoint>();
        private string periodKey;
        internal int Version { get; private set; }
        internal void Clear() { if (points.Count > 0) Version++; points.Clear(); periodKey = null; }
        internal bool Observe(DateTime at, double used, string period)
        {
            if (at.Kind != DateTimeKind.Utc || used < 0 || used > 100 || double.IsNaN(used) || double.IsInfinity(used)) return false;
            if (points.Count > 0 && (at < points[points.Count - 1].At || (at == points[points.Count - 1].At && periodKey == period))) return false;
            if (points.Count > 0 && (periodKey != period || used < points[points.Count - 1].Used - .00001 || at - points[points.Count - 1].At > TimeSpan.FromMinutes(6))) Clear();
            periodKey = period;
            points.Add(new TrendPoint { At = at, Used = used });
            while (points.Count > 1 && (points.Count > 120 || at - points[0].At > TimeSpan.FromMinutes(30))) points.RemoveAt(0);
            Version++; return true;
        }
        internal TrendPoint[] PointsAt(DateTime now)
        {
            if (points.Count == 0 || now < points[points.Count - 1].At || now - points[points.Count - 1].At > TimeSpan.FromMinutes(6)) return new TrendPoint[0];
            return points.ToArray();
        }
        internal static string PeriodKey(DateTime? start, DateTime? end)
        { return (start.HasValue ? start.Value.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?") + ":" + (end.HasValue ? end.Value.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?"); }
    }
}
