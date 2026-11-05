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
    }
}
