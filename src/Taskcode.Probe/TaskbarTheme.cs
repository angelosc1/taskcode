using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class TaskbarTheme
    {
        internal bool Dark, HighContrast;
        internal Color Text, Muted, Track, Divider, Claude, Codex, PreviewBackground;
        internal static TaskbarTheme Create(bool dark)
        {
            return new TaskbarTheme
            {
                Dark = dark,
                Text = dark ? Color.FromArgb(239, 242, 245) : Color.FromArgb(30, 34, 39),
                Muted = dark ? Color.FromArgb(172, 180, 190) : Color.FromArgb(86, 94, 105),
                Track = dark ? Color.FromArgb(92, 99, 108) : Color.FromArgb(188, 194, 201),
                Divider = dark ? Color.FromArgb(96, 104, 114) : Color.FromArgb(170, 177, 185),
                Claude = dark ? Color.FromArgb(222, 157, 126) : Color.FromArgb(157, 70, 34),
                Codex = dark ? Color.FromArgb(144, 219, 188) : Color.FromArgb(20, 104, 77),
                PreviewBackground = dark ? Color.FromArgb(36, 38, 41) : Color.FromArgb(242, 243, 245)
            };
        }
        // Read() and AppPalette() build a new object each time, so compare colors, not references.
        internal bool SameAs(TaskbarTheme other)
        {
            return other != null && Dark == other.Dark && HighContrast == other.HighContrast && Text == other.Text && Muted == other.Muted &&
                Track == other.Track && Divider == other.Divider && Claude == other.Claude && Codex == other.Codex && PreviewBackground == other.PreviewBackground;
        }
        internal static TaskbarTheme AppPalette(TaskbarTheme palette)
        { return palette.HighContrast ? palette : Create(true); }
        internal static TaskbarTheme Read()
        {
            var theme = Create(true);
            if (SystemInformation.HighContrast)
            {
                theme.HighContrast = true;
                theme.Text = theme.Claude = theme.Codex = SystemColors.WindowText;
                theme.Track = theme.Divider = theme.Muted = SystemColors.GrayText;
                theme.PreviewBackground = SystemColors.Window;
            }
            return theme;
        }
    }
}
