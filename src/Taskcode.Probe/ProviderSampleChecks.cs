using System;

namespace Taskcode.Probe
{
    // Synthetic observations used only by offscreen regression checks.
    internal static class ProviderSamples
    {
        internal static OptionalProvider[] Create(DateTime now)
        {
            var providers = ProviderCatalog.Create();
            for (int index = 0; index < providers.Length; index++)
            {
                var snapshot = new OptionalSnapshot { Status = "Sample data", ObservedUtc = now };
                if (index == 8)
                {
                    snapshot.CountText = "1.3M"; snapshot.TokenCount = 1250000;
                    snapshot.Metrics.Add("Sample data");
                }
                else
                {
                    snapshot.Quotas.Add(new OptionalQuota { Name = "Session", Used = 18 + index * 9, StartUtc = now.AddHours(-2), ResetUtc = now.AddHours(3) });
                    snapshot.Quotas.Add(new OptionalQuota { Name = "Weekly", Used = 12 + index * 7, StartUtc = now.AddDays(-2), ResetUtc = now.AddDays(5) });
                }
                var baseline = new OptionalSnapshot { Status = "Sample data", ObservedUtc = now.AddMinutes(-5), TokenCount = snapshot.TokenCount.HasValue ? snapshot.TokenCount.Value - 25000 : (double?)null };
                foreach (var quota in snapshot.Quotas) baseline.Quotas.Add(new OptionalQuota { Name = quota.Name, Used = Math.Max(0, quota.Used - 2), StartUtc = quota.StartUtc, ResetUtc = quota.ResetUtc });
                providers[index].Apply(baseline); providers[index].Apply(snapshot);
            }
            return providers;
        }
    }
}
