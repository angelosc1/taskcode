using System;

namespace Taskcode.Probe
{
    internal sealed class DemoReading
    {
        internal string Provider, Window;
        internal QuotaPeriod Period;
        internal UsageSpeed Speed;
        internal DateTime PulseUntil;
    }

    internal sealed class UsageDemo
    {
        internal readonly DemoReading[] Readings;
        private int scenario;
        private DateTime nextSample;
        internal UsageDemo()
        {
            var now = DateTime.UtcNow;
            Readings = new[]
            {
                Create("Claude", "5-hour window", 68, now.AddHours(-5).AddHours(2).AddMinutes(14), now.AddHours(2).AddMinutes(14)),
                Create("Claude", "Weekly", 81, now.AddDays(-1.75), now.AddDays(5.25)),
                Create("Codex", "5-hour window", 42, now.AddHours(-5).AddHours(1).AddMinutes(8), now.AddHours(1).AddMinutes(8)),
                Create("Codex", "Weekly", 63, now.AddDays(-1.96), now.AddDays(5.04))
            };
            Seed(now, false);
        }
        private static DemoReading Create(string provider, string window, int remaining, DateTime start, DateTime end)
        { return new DemoReading { Provider = provider, Window = window, Period = new QuotaPeriod { Remaining = remaining, StartUtc = start, EndUtc = end } }; }
        internal void Cycle(DateTime now) { scenario = (scenario + 1) % 6; Seed(now, true); }
        internal string ScenarioName { get { return new[] { "Mixed", "Comfortable", "Near reset", "Fast", "Idle", "Calculating" }[scenario]; } }
        private void Seed(DateTime now, bool announce)
        {
            for (int index = 0; index < Readings.Length; index++)
            {
                var reading = Readings[index];
                int previous = reading.Speed == null ? 0 : reading.Speed.EstimateAt(now).Level;
                reading.Speed = new UsageSpeed();
                int kind = scenario == 0 ? new[] { 1, 2, 3, 1 }[index] : scenario;
                double ratio = kind == 1 ? 1.8 : kind == 2 ? 1.0 : 0.45;
                double rate = kind >= 4 ? 0 : reading.Period.Remaining / Math.Max(0.01, (reading.Period.EndUtc - now).TotalHours * ratio);
                for (int second = kind == 5 ? 0 : 15; second >= 0; second--)
                    reading.Speed.Observe(now.AddSeconds(-second), Math.Min(100, reading.Period.Remaining + rate * second / 3600.0), reading.Period.StartUtc, reading.Period.EndUtc);
                int current = reading.Speed.EstimateAt(now).Level;
                reading.PulseUntil = announce && previous != current && current > 0 ? now.AddSeconds(8) : DateTime.MinValue;
            }
            nextSample = now.AddSeconds(2);
        }
        internal void Tick(DateTime now)
        {
            if (now < nextSample) return;
            nextSample = now.AddSeconds(2);
            foreach (var reading in Readings)
            {
                int previous = reading.Speed.EstimateAt(now).Level;
                reading.Speed.Observe(now, reading.Period.Remaining, reading.Period.StartUtc, reading.Period.EndUtc);
                int current = reading.Speed.EstimateAt(now).Level;
                if (previous != current && current > 0) reading.PulseUntil = now.AddSeconds(8);
            }
        }
        internal int PulseKey(DateTime now, bool includeWeekly)
        {
            int key = 0;
            for (int index = 0; index < Readings.Length; index++)
                if ((includeWeekly || index % 2 == 0) && Readings[index].PulseUntil > now)
                    key |= Readings[index].Speed.EstimateAt(now).Level << (index * 2);
            return key;
        }
    }
}
