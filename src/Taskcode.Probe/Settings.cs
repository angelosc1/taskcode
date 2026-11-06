using System;
using System.IO;
using System.Xml;

namespace Taskcode.Probe
{
    internal enum DetailsTrigger { Hover, Click }

    internal sealed class Settings
    {
        internal IndicatorStyle Style = IndicatorStyle.Essential;
        internal HostMode Mode = HostMode.Embedded;
        internal DetailsTrigger DetailsTrigger = DetailsTrigger.Hover;
        internal bool ShowWeekly;
        internal bool CodexEnabled = true, ClaudeEnabled = true;
        internal bool CodexPrivateProfile, ClaudePrivateProfile;
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
                DetailsTrigger detailsTrigger;
                bool showWeekly;
                string savedStyle = doc.DocumentElement.GetAttribute("style");
                if (savedStyle == "Pill") savedStyle = "Essential";
                if (Enum.TryParse(savedStyle, out style) && Enum.IsDefined(typeof(IndicatorStyle), style)) value.Style = style;
                if (Enum.TryParse(doc.DocumentElement.GetAttribute("mode"), out mode) && Enum.IsDefined(typeof(HostMode), mode)) value.Mode = mode;
                if (Enum.TryParse(doc.DocumentElement.GetAttribute("detailsTrigger"), out detailsTrigger) && Enum.IsDefined(typeof(DetailsTrigger), detailsTrigger)) value.DetailsTrigger = detailsTrigger;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("showWeekly"), out showWeekly)) value.ShowWeekly = showWeekly;
                bool connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("codexEnabled"), out connected)) value.CodexEnabled = connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("claudeEnabled"), out connected)) value.ClaudeEnabled = connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("codexPrivateProfile"), out connected)) value.CodexPrivateProfile = connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("claudePrivateProfile"), out connected)) value.ClaudePrivateProfile = connected;
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
                writer.WriteAttributeString("detailsTrigger", DetailsTrigger.ToString());
                writer.WriteAttributeString("showWeekly", ShowWeekly.ToString());
                writer.WriteAttributeString("codexEnabled", CodexEnabled.ToString());
                writer.WriteAttributeString("claudeEnabled", ClaudeEnabled.ToString());
                writer.WriteAttributeString("codexPrivateProfile", CodexPrivateProfile.ToString());
                writer.WriteAttributeString("claudePrivateProfile", ClaudePrivateProfile.ToString());
                writer.WriteEndElement();
            }
        }
    }
}
