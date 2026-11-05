using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Media;
using System.Xml;

namespace Taskcode.Probe
{
    internal static class BrandMarks
    {
        private static readonly GraphicsPath ClaudePath = LoadClaude();
        private static GraphicsPath LoadClaude()
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Taskcode.Claude.svg")) doc.Load(stream);
            var element = (XmlElement)doc.GetElementsByTagName("path")[0];
            var flattened = Geometry.Parse(element.GetAttribute("d")).GetFlattenedPathGeometry();
            var result = new GraphicsPath();
            foreach (var figure in flattened.Figures)
            {
                var points = new List<PointF> { new PointF((float)figure.StartPoint.X, (float)figure.StartPoint.Y) };
                foreach (var segment in figure.Segments)
                {
                    var lines = segment as PolyLineSegment;
                    if (lines != null) foreach (var point in lines.Points) points.Add(new PointF((float)point.X, (float)point.Y));
                    var line = segment as LineSegment;
                    if (line != null) points.Add(new PointF((float)line.Point.X, (float)line.Point.Y));
                }
                result.AddLines(points.ToArray());
                if (figure.IsClosed) result.CloseFigure();
            }
            return result;
        }
        internal static void DrawClaude(Graphics graphics, System.Drawing.Brush brush, RectangleF bounds)
        {
            using (var path = (GraphicsPath)ClaudePath.Clone())
            using (var transform = new System.Drawing.Drawing2D.Matrix(bounds.Width / 24f, 0, 0, bounds.Height / 24f, bounds.X, bounds.Y))
            { path.Transform(transform); graphics.FillPath(brush, path); }
        }
        internal static void DrawCodex(Graphics graphics, System.Drawing.Brush brush, RectangleF bounds)
        {
            // A centered terminal mark avoids font side bearings and baseline offsets.
            var state = graphics.Save();
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 16f, bounds.Height / 16f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new System.Drawing.Pen(brush, 1.15f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
            {
                graphics.DrawLines(pen, new[] { new PointF(2.5f, 4), new PointF(6.8f, 8), new PointF(2.5f, 12) });
                graphics.DrawLine(pen, 9, 12, 13.5f, 12);
            }
            graphics.Restore(state);
        }
    }
}
