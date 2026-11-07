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
        private static readonly GraphicsPath ClaudePath = LoadSvg("Taskcode.Claude.svg");
        private static readonly GraphicsPath CodexPath = CreateCodexPath();
        private static readonly GraphicsPath[] ProviderPaths = LoadProviders();
        private static GraphicsPath[] LoadProviders()
        {
            var paths = new GraphicsPath[ProviderCatalog.Count];
            for (int index = 0; index < paths.Length; index++)
                paths[index] = LoadSvg("Taskcode.Provider." + ProviderCatalog.Logos[index] + ".svg");
            return paths;
        }
        private static GraphicsPath LoadSvg(string resource)
        {
            var doc = new XmlDocument { XmlResolver = null };
            var result = new GraphicsPath();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) doc.Load(stream);
            foreach (XmlElement element in doc.GetElementsByTagName("path"))
            {
                var flattened = Geometry.Parse(element.GetAttribute("d")).GetFlattenedPathGeometry();
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
            }
            return result;
        }
        internal static void DrawClaude(Graphics graphics, System.Drawing.Brush brush, RectangleF bounds, double degrees = double.NaN)
        {
            var solid = brush as SolidBrush;
            if (solid == null) DrawPath(graphics, brush, bounds, ClaudePath);
            else ExtrudedLogo.Draw(graphics, ClaudePath, solid.Color, bounds, double.IsNaN(degrees) ? LogoMotion.Angle(0) : degrees, !double.IsNaN(degrees) || LogoMotion.HasReflection(0));
        }
        internal static void DrawProvider(Graphics graphics, System.Drawing.Brush brush, int index, RectangleF bounds, double degrees = double.NaN)
        {
            var solid = brush as SolidBrush;
            if (solid == null) DrawPath(graphics, brush, bounds, ProviderPaths[index]);
            else ExtrudedLogo.Draw(graphics, ProviderPaths[index], solid.Color, bounds, double.IsNaN(degrees) ? LogoMotion.Angle(index + 2) : degrees, !double.IsNaN(degrees) || LogoMotion.HasReflection(index + 2));
        }
        internal static void DrawOriginal(Graphics graphics, System.Drawing.Brush brush, int provider, RectangleF bounds)
        {
            DrawPath(graphics, brush, bounds, provider == 0 ? ClaudePath : provider == 1 ? CodexPath : ProviderPaths[provider - 2]);
        }
        private static void DrawPath(Graphics graphics, System.Drawing.Brush brush, RectangleF bounds, GraphicsPath source)
        {
            using (var path = (GraphicsPath)source.Clone())
            using (var transform = new System.Drawing.Drawing2D.Matrix(bounds.Width / 24f, 0, 0, bounds.Height / 24f, bounds.X, bounds.Y))
            { path.Transform(transform); graphics.FillPath(brush, path); }
        }
        internal static void DrawCodex(Graphics graphics, System.Drawing.Brush brush, RectangleF bounds, double degrees = double.NaN)
        {
            bounds.Inflate(bounds.Width * .06f, bounds.Height * .06f);
            var solid = brush as SolidBrush;
            if (solid == null) DrawPath(graphics, brush, bounds, CodexPath);
            else ExtrudedLogo.Draw(graphics, CodexPath, solid.Color, bounds, double.IsNaN(degrees) ? LogoMotion.Angle(1) : degrees, !double.IsNaN(degrees) || LogoMotion.HasReflection(1));
        }
        private static GraphicsPath CreateCodexPath()
        {
            var path = new GraphicsPath();
            path.AddLines(new[] { new PointF(2.5f, 4), new PointF(6.8f, 8), new PointF(2.5f, 12) });
            path.StartFigure(); path.AddLine(9, 12, 13.5f, 12);
            using (var pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.15f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round }) path.Widen(pen);
            using (var scale = new System.Drawing.Drawing2D.Matrix(1.5f, 0, 0, 1.5f, 0, 0)) path.Transform(scale);
            return path;
        }
    }
}
