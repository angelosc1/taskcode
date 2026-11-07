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
        internal bool UsageBarColors = false;
        internal bool AutomaticUpdateChecks = true;
        internal string Language = "auto";
        internal DateTime LastUpdateCheckUtc;
        internal bool CodexEnabled = true, ClaudeEnabled = true;
        internal bool CodexPrivateProfile, ClaudePrivateProfile;
        internal readonly bool[] OptionalProvidersEnabled = new bool[ProviderCatalog.Count];
        internal bool CursorEnabled { get { return OptionalProvidersEnabled[0]; } set { OptionalProvidersEnabled[0] = value; } }
        internal bool AntigravityEnabled { get { return OptionalProvidersEnabled[1]; } set { OptionalProvidersEnabled[1] = value; } }
        internal static readonly string Path = System.IO.Path.Combine(AppInfo.DataDirectory, "settings.xml");
        internal static Settings Load()
        {
            var value = new Settings();
            try
            {
                var doc = new XmlDocument { XmlResolver = null };
                string source = File.Exists(Path) ? Path : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
                using (var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) doc.Load(reader);
                IndicatorStyle style;
                HostMode mode;
                DetailsTrigger detailsTrigger;
                bool showWeekly;
                string savedStyle = doc.DocumentElement.GetAttribute("style");
                value.Language = L10n.Normalize(doc.DocumentElement.GetAttribute("language"));
                if (savedStyle == "Pill") savedStyle = "Essential";
                if (Enum.TryParse(savedStyle, out style) && Enum.IsDefined(typeof(IndicatorStyle), style)) value.Style = style;
                if (Enum.TryParse(doc.DocumentElement.GetAttribute("mode"), out mode) && Enum.IsDefined(typeof(HostMode), mode)) value.Mode = mode;
                if (Enum.TryParse(doc.DocumentElement.GetAttribute("detailsTrigger"), out detailsTrigger) && Enum.IsDefined(typeof(DetailsTrigger), detailsTrigger)) value.DetailsTrigger = detailsTrigger;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("showWeekly"), out showWeekly)) value.ShowWeekly = showWeekly;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("usageBarColors"), out showWeekly)) value.UsageBarColors = showWeekly;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("automaticUpdateChecks"), out showWeekly)) value.AutomaticUpdateChecks = showWeekly;
                DateTime lastCheck;
                if (DateTime.TryParse(doc.DocumentElement.GetAttribute("lastUpdateCheckUtc"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out lastCheck)) value.LastUpdateCheckUtc = lastCheck.ToUniversalTime();
                bool connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("codexEnabled"), out connected)) value.CodexEnabled = connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("claudeEnabled"), out connected)) value.ClaudeEnabled = connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("codexPrivateProfile"), out connected)) value.CodexPrivateProfile = connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("cursorEnabled"), out connected)) value.CursorEnabled = connected;
                if (bool.TryParse(doc.DocumentElement.GetAttribute("antigravityEnabled"), out connected)) value.AntigravityEnabled = connected;
                for (int index = 2; index < ProviderCatalog.Count; index++)
                    if (bool.TryParse(doc.DocumentElement.GetAttribute(ProviderCatalog.Ids[index] + "Enabled"), out connected)) value.OptionalProvidersEnabled[index] = connected;
            }
            catch (IOException) { }
            catch (XmlException) { }
            catch (UnauthorizedAccessException) { }
            return value;
        }
        internal void Save()
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            using (var writer = XmlWriter.Create(Path, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("taskcode");
                writer.WriteAttributeString("style", Style.ToString());
                writer.WriteAttributeString("language", Language);
                writer.WriteAttributeString("mode", Mode.ToString());
                writer.WriteAttributeString("detailsTrigger", DetailsTrigger.ToString());
                writer.WriteAttributeString("showWeekly", ShowWeekly.ToString());
                writer.WriteAttributeString("usageBarColors", UsageBarColors.ToString());
                writer.WriteAttributeString("automaticUpdateChecks", AutomaticUpdateChecks.ToString());
                writer.WriteAttributeString("lastUpdateCheckUtc", LastUpdateCheckUtc.ToString("o"));
                writer.WriteAttributeString("codexEnabled", CodexEnabled.ToString());
                writer.WriteAttributeString("claudeEnabled", ClaudeEnabled.ToString());
                writer.WriteAttributeString("codexPrivateProfile", CodexPrivateProfile.ToString());
                writer.WriteAttributeString("claudePrivateProfile", ClaudePrivateProfile.ToString());
                writer.WriteAttributeString("cursorEnabled", CursorEnabled.ToString());
                writer.WriteAttributeString("antigravityEnabled", AntigravityEnabled.ToString());
                for (int index = 2; index < ProviderCatalog.Count; index++) writer.WriteAttributeString(ProviderCatalog.Ids[index] + "Enabled", OptionalProvidersEnabled[index].ToString());
                writer.WriteEndElement();
            }
        }
    }
}
