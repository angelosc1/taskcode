using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class OptionalProviderDetails : Form
    {
        private readonly OptionalProvider provider;
        private readonly Label status;
        private readonly FlowLayoutPanel rows;
        private readonly Timer timer = new Timer { Interval = 1000 };
        private readonly Timer dismissal = new Timer { Interval = 16 };
        private OptionalSnapshot rendered;
        internal bool UsageBarColors { get; set; }
        internal Rectangle HoverAnchor;
        private readonly DetailInteraction interaction = new DetailInteraction();
        private readonly Button closeDetails;
        internal bool Pinned { get { return interaction.Pinned; } }
        internal bool RequiresIndicator;
        internal void SetInteraction(bool pinned)
        { interaction.Reset(pinned, (Native.GetAsyncKeyState(1) & 0x8000) != 0); closeDetails.Visible = pinned; }
        internal OptionalProviderDetails(OptionalProvider provider)
        {
            this.provider = provider; Text = "Taskcode - " + provider.Name;
            ClientSize = new Size(380, 420); FormBorderStyle = FormBorderStyle.None; ControlBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; BackColor = TaskbarTheme.Create(true).PreviewBackground; ForeColor = Color.WhiteSmoke; Font = new Font("Segoe UI", 9f);
            status = new Label { Location = new Point(16, 12), Size = new Size(328, 62) }; Controls.Add(status);
            closeDetails = new Button { Text = "×", AccessibleName = "Close details", Location = new Point(350, 10), Size = new Size(22, 24), FlatStyle = FlatStyle.Flat, ForeColor = ForeColor, BackColor = BackColor, TabStop = false, Visible = false };
            closeDetails.FlatAppearance.BorderSize = 0; closeDetails.Click += delegate { Hide(); }; Controls.Add(closeDetails);
            rows = new FlowLayoutPanel { Location = new Point(12, 80), Size = new Size(356, 328), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false }; Controls.Add(rows);
            timer.Tick += delegate { RefreshUsage(); };
            dismissal.Tick += delegate { TrackPointer(Cursor.Position); };
            VisibleChanged += delegate { timer.Enabled = dismissal.Enabled = Visible; if (Visible) RefreshUsage(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Disposed += delegate { timer.Dispose(); dismissal.Dispose(); }; L10n.Bind(this);
        }
        internal void TrackPointer(Point pointer)
        {
            if (Visible && interaction.ShouldDismiss(Bounds, HoverAnchor, pointer, (Native.GetAsyncKeyState(1) & 0x8000) != 0)) Hide();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        { get { var value = base.CreateParams; value.ExStyle |= 0x08000000 | 0x80 | 0x08; return value; } }
        protected override void WndProc(ref Message message)
        { if (message.Msg == 0x0021) { message.Result = new IntPtr(3); return; } base.WndProc(ref message); }
        internal void RefreshUsage()
        {
            var snapshot = provider.Snapshot;
            status.Text = provider.Name + "\n" + L10n.T(provider.Enabled ? snapshot.Status : "Monitoring disabled");
            if (!string.IsNullOrWhiteSpace(snapshot.Identity)) status.Text += "\n" + snapshot.Identity;
            if (rendered != snapshot)
            {
                while (rows.Controls.Count > 0) rows.Controls[0].Dispose();
                rendered = snapshot;
                foreach (string metric in snapshot.Metrics) rows.Controls.Add(new Label { Text = L10n.T(metric), Size = new Size(328, 42), ForeColor = ForeColor });
                foreach (var quota in snapshot.Quotas) rows.Controls.Add(new OptionalQuotaCard(provider, quota) { Size = new Size(328, 104), Margin = new Padding(2, 2, 2, 8) });
                if (snapshot.Quotas.Count == 0 && snapshot.Metrics.Count == 0) rows.Controls.Add(new Label { Text = L10n.T("No quota data available. Connect and sign in in the official app."), Size = new Size(328, 70) });
            }
            foreach (Control row in rows.Controls)
            { var card = row as OptionalQuotaCard; if (card != null) card.UsageBarColors = UsageBarColors; }
            rows.Invalidate(true);
        }
    }
    internal sealed class OptionalQuotaCard : Control
    {
        private readonly OptionalProvider provider;
        private readonly OptionalQuota quota;
        internal bool UsageBarColors { get; set; }
        internal OptionalQuotaCard(OptionalProvider provider, OptionalQuota quota)
        { this.provider = provider; this.quota = quota; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var now = DateTime.UtcNow; var palette = TaskbarTheme.Create(true);
            e.Graphics.Clear(UiSurface.Card(palette));
            bool fresh = !provider.Snapshot.Failed && now - provider.Snapshot.ObservedUtc <= TimeSpan.FromMinutes(6) && (!quota.ResetUtc.HasValue || now < quota.ResetUtc.Value);
            string reset = !quota.ResetUtc.HasValue ? L10n.T("Reset time unavailable") : quota.ResetUtc.Value <= now ? L10n.T("Waiting for reset") : L10n.T("Resets in ") + ResetIn(quota.ResetUtc.Value - now);
            double? elapsed = quota.StartUtc.HasValue && quota.ResetUtc.HasValue && quota.ResetUtc > quota.StartUtc ?
                (double?)Math.Max(0, Math.Min(1, (now - quota.StartUtc.Value).TotalSeconds / (quota.ResetUtc.Value - quota.StartUtc.Value).TotalSeconds)) : null;
            using (var text = new SolidBrush(palette.Text))
            using (var muted = new SolidBrush(palette.Muted))
            using (var fill = new SolidBrush(UsageBarPalette.Resolve(provider.Color, quota.Used, UsageBarColors, false)))
            using (var track = new SolidBrush(palette.Track))
            {
                e.Graphics.DrawString(L10n.T(quota.Name), Font, text, new RectangleF(10, 8, Width - 20, 30));
                e.Graphics.FillRectangle(track, 10, 46, Width - 20, 6);
                if (fresh) e.Graphics.FillRectangle(fill, 10, 46, (Width - 20) * (float)Math.Min(1, quota.Used / 100), 6);
                if (fresh && elapsed.HasValue) using (var marker = new Pen(palette.Text)) { float x = 10 + (Width - 20) * (float)elapsed.Value; e.Graphics.DrawLine(marker, x, 42, x, 56); }
                e.Graphics.DrawString(fresh ? L10n.T(Math.Round(quota.Used) + "% used / " + (elapsed.HasValue ? Math.Round(elapsed.Value * 100) + "% time" : "-- time")) : L10n.T("Unavailable"), Font, text, 10, 60);
                e.Graphics.DrawString(reset, Font, muted, 10, 81);
            }
        }
        private static string ResetIn(TimeSpan remaining)
        { return remaining.TotalDays >= 1 ? (int)remaining.TotalDays + "d " + remaining.Hours + "h" : remaining.TotalHours >= 1 ? (int)remaining.TotalHours + "h " + remaining.Minutes + "m" : Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)) + "m"; }
    }
}
