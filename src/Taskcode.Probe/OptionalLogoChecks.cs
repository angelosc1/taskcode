using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace Taskcode.Probe
{
    internal static class OptionalLogoChecks
    {
        private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
        internal static void Run(string directory)
        {
            DateTime now = DateTime.UtcNow;
            var fixtures = ProviderSamples.Create(now);
            var live = ProviderCatalog.Create();
            foreach (var provider in fixtures)
            {
                provider.Enabled = true;
                Check(provider.DisplayText(now) != "--" && provider.RotationLevel(now) > 0, "All sample providers need visible values and independent consumption-driven animation.");
                Check(provider.RotationLevel(now.AddMinutes(7)) == 0, "Stale observations must stop optional-provider animation.");
                provider.ResetRotation(); Check(provider.RotationLevel(now) == 0, "Disconnect must discard previous motion.");
            }
            foreach (var provider in live) Check(provider.Snapshot.Quotas.Count == 0 && provider.Snapshot.CountText == null && provider.RotationLevel(now) == 0, "Sample fixtures must not alter live provider objects.");
            var activeFixtures = ProviderSamples.Create(now);
            foreach (var provider in activeFixtures) provider.Enabled = true;
            var usage = new UsageDemo { OptionalProviders = activeFixtures };
            LogoMotion.Update(usage, now); LogoMotion.Advance(.05);
            for (int provider = 0; provider < 2 + ProviderCatalog.Count; provider++)
                if (SpeedArrows.IsAnimated) Check(LogoMotion.Angle(provider) > 0 && LogoMotion.HasReflection(provider), "Consumption must drive all eleven logos independently.");
            usage.IsLive = true; usage.ActivityKey = 0;
            foreach (var provider in activeFixtures) provider.ResetRotation();
            LogoMotion.Update(usage, now);
            for (int provider = 0; provider < 2 + ProviderCatalog.Count; provider++) Check(LogoMotion.Angle(provider) == 0 && !LogoMotion.HasReflection(provider), "All idle logos must face forward without reflection.");
            double[] angles = { 0, 40, 90, 180, 320 };
            using (var board = new Bitmap(470, ProviderCatalog.Count * 74))
            using (var graphics = Graphics.FromImage(board))
            {
                graphics.Clear(Color.FromArgb(25, 28, 30));
                for (int provider = 0; provider < ProviderCatalog.Count; provider++)
                for (int index = 0; index < angles.Length; index++)
                using (var brush = new SolidBrush(fixtures[provider].Color))
                {
                    BrandMarks.DrawProvider(graphics, brush, provider, new RectangleF(index * 94 + 12, provider * 74 + 8, 48, 48), angles[index]);
                    BrandMarks.DrawProvider(graphics, brush, provider, new RectangleF(index * 94 + 65, provider * 74 + 26, 16, 16), angles[index]);
                }
                board.Save(Path.Combine(directory, "optional-logos-3d-check.png"));
            }
            var clock = Stopwatch.StartNew();
            using (var bitmap = new Bitmap(240, 24))
            using (var graphics = Graphics.FromImage(bitmap))
                for (int frame = 0; frame < 120; frame++)
                for (int provider = 0; provider < ProviderCatalog.Count; provider++)
                using (var brush = new SolidBrush(fixtures[provider].Color))
                    BrandMarks.DrawProvider(graphics, brush, provider, new RectangleF(provider * 24 + 2, 4, 16, 16), frame * 3);
            File.WriteAllText(Path.Combine(directory, "optional-logos-3d-timing.txt"), "Nine optional logos: " + (clock.Elapsed.TotalMilliseconds / 120).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms/frame.");
        }
    }
}
