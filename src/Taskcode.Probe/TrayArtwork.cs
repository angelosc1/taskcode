using System.Drawing;
using System.Reflection;

namespace Taskcode.Probe
{
    internal static class TrayArtwork
    {
        internal static Icon Create()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Taskcode.AppIcon.ico"))
            using (var icon = new Icon(stream, new Size(32, 32)))
                return (Icon)icon.Clone();
        }
    }
}
