using System;
using System.IO;
using System.Xml;

namespace Taskcode.Probe
{
    internal sealed class Settings
    {
        internal IndicatorStyle Style = IndicatorStyle.Lines;
        internal HostMode Mode = HostMode.Embedded;
        internal static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
        internal static Settings Load()
        {
            var value = new Settings();
            try
            {
                var doc = new XmlDocument { XmlResolver = null };
                using (var reader = XmlReader.Create(Path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) doc.Load(reader);
                IndicatorStyle style;
                HostMode mode;
                if (Enum.TryParse(doc.DocumentElement.GetAttribute("style"), out style) && Enum.IsDefined(typeof(IndicatorStyle), style)) value.Style = style;
                if (Enum.TryParse(doc.DocumentElement.GetAttribute("mode"), out mode) && Enum.IsDefined(typeof(HostMode), mode)) value.Mode = mode;
            }
            catch (IOException) { }
            catch (XmlException) { }
            catch (UnauthorizedAccessException) { }
            return value;
        }
        internal void Save()
        {
            using (var writer = XmlWriter.Create(Path, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("taskcode");
                writer.WriteAttributeString("style", Style.ToString());
                writer.WriteAttributeString("mode", Mode.ToString());
                writer.WriteEndElement();
            }
        }
    }
}
