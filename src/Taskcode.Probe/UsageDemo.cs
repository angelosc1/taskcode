using System;

namespace Taskcode.Probe
{
    internal sealed class DemoReading
    {
        internal string Provider, Window;
        internal QuotaPeriod Period;
        internal UsageSpeed Speed;
        internal DateTime PulseUntil;
        internal bool Available = true;
        internal DateTime ObservedUtc;
    }

    internal sealed class UsageDemo
    {
        internal readonly DemoReading[] Readings;
        private int scenario;
        private DateTime nextSample;
        internal bool IsLive;
        internal int Revision;
        internal int ActivityKey;
        private DateTime demoActivityUntil = DateTime.UtcNow.AddSeconds(15);
        internal int ActivityAt(DateTime now) { return IsLive ? ActivityKey : now < demoActivityUntil && scenario < 4 ? 3 : 0; }
        internal readonly string[] AccountStatus = { "Demo data", "Demo data" };
        internal readonly string[] AccountIdentity = { "", "" };
        internal void BeginLive()
        {
            IsLive = true;
            for (int provider = 0; provider < 2; provider++) ClearProvider(provider, "Connecting...");
        }
        internal void ClearProvider(int provider, string status)
        {
            ActivityKey &= ~(1 << provider);
            AccountStatus[provider] = status;
            AccountIdentity[provider] = "";
            for (int i = provider * 2; i < provider * 2 + 2; i++)
            {
                Readings[i].Available = false; Readings[i].ObservedUtc = DateTime.MinValue;
                Readings[i].Period.Remaining = -1; Readings[i].Period.StartUtc = Readings[i].Period.EndUtc = DateTime.MinValue;
                Readings[i].Speed = LiveSpeed(provider); Readings[i].PulseUntil = DateTime.MinValue;
            }
            Revision++;
        }
        internal void Apply(int provider, AccountSnapshot snapshot)
        {
            AccountStatus[provider] = snapshot.Status;
            if (!snapshot.Failed) AccountIdentity[provider] = snapshot.Identity ?? "";
            if (snapshot.Failed) { Revision++; return; }
            for (int index = 0; index < 2; index++)
            {
                var reading = Readings[provider * 2 + index];
                var period = snapshot.Periods[index];
                if (period == null) { reading.Available = false; reading.Period.Remaining = -1; reading.Speed = LiveSpeed(provider); reading.PulseUntil = DateTime.MinValue; continue; }
                if (snapshot.ObservedUtc == reading.ObservedUtc && reading.Available && period.Remaining == reading.Period.Remaining && period.StartUtc == reading.Period.StartUtc && period.EndUtc == reading.Period.EndUtc) continue;
                int previous = reading.Speed.EstimateAt(snapshot.ObservedUtc).Level;
                reading.Available = true; reading.ObservedUtc = snapshot.ObservedUtc;
                reading.Period.Remaining = period.Remaining; reading.Period.StartUtc = period.StartUtc; reading.Period.EndUtc = period.EndUtc;
                reading.Speed.Observe(snapshot.ObservedUtc, snapshot.Remaining[index].Value, period.StartUtc, period.EndUtc);
                int current = reading.Speed.EstimateAt(snapshot.ObservedUtc).Level;
                if (previous != current && current > 0) reading.PulseUntil = DateTime.UtcNow.AddSeconds(8);
            }
            Revision++;
        }
        internal int DisplayValue(int index, DateTime now)
        {
            var reading = Readings[index];
            return !IsLive ? reading.Period.Remaining : reading.Available && now < reading.Period.EndUtc && now - reading.ObservedUtc <= TimeSpan.FromMinutes(index < 2 ? 6 : 2) ? reading.Period.Remaining : -1;
        }
        private static UsageSpeed LiveSpeed(int provider)
        { return new UsageSpeed(TimeSpan.FromSeconds(provider == 0 ? 300 : 10)); }
        internal SpeedEstimate DetailSpeedAt(int index, DateTime now)
        {
            if (DisplayValue(index, now) < 0) return new SpeedEstimate { State = SpeedState.Unavailable };
            var estimate = Readings[index].Speed.DisplayEstimateAt(now);
            if ((ActivityAt(now) & (1 << (index / 2))) == 0 &&
                (estimate.State == SpeedState.Active || estimate.State == SpeedState.Calculating || estimate.State == SpeedState.Idle))
                return new SpeedEstimate { State = SpeedState.Paused };
            return estimate;
        }
        internal int DisplayUsed(int index, DateTime now)
        {
            int remaining = DisplayValue(index, now);
            return remaining < 0 ? -1 : 100 - Math.Max(0, Math.Min(100, remaining));
        }
        internal string ProviderStatus(int provider, DateTime now)
        {
            if (!IsLive) return "DEMO DATA / Simulated history";
            var reading = Readings[provider * 2].Available ? Readings[provider * 2] : Readings[provider * 2 + 1];
            if (!reading.Available) return AccountStatus[provider];
            if (now >= reading.Period.EndUtc) return "Period ended. Waiting for fresh quota data.";
            return AccountStatus[provider] + " / Updated " + Math.Max(0, (int)(now - reading.ObservedUtc).TotalSeconds) + "s ago";
        }
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
        internal void Cycle(DateTime now) { scenario = (scenario + 1) % 6; demoActivityUntil = now.AddSeconds(15); Seed(now, true); }
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
            if (IsLive) return; // Only fresh provider observations can feed the speed estimate.
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
                if (DisplayValue(index, now) >= 0 && (includeWeekly || index % 2 == 0) && Readings[index].PulseUntil > now)
                    key |= Readings[index].Speed.EstimateAt(now).Level << (index * 2);
            return key;
        }
    }
}
