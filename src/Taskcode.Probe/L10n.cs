using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    // Translate only presentation text. Protocol values, provider data and settings IDs stay stable.
    internal static class L10n
    {
        private const string Prefix = "Taskcode.Locales.";
        private static readonly Dictionary<string, string> english = Load("en");
        private static Dictionary<string, string> translations = english;
        private static readonly List<Action> refresh = new List<Action>();
        private static readonly Dictionary<string, Regex> patterns = new Dictionary<string, Regex>();
        private static readonly Dictionary<Control, Dictionary<ToolTip, string>> hints = new Dictionary<Control, Dictionary<ToolTip, string>>();
        internal static string Language = "en";
        internal static string[] Languages { get { return new[] { "auto" }.Concat(Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Select(n => n.Substring(Prefix.Length, n.Length - Prefix.Length - 5)).OrderBy(n => n == "en" ? "" : n)).ToArray(); } }
        internal static string Normalize(string code) { return Languages.Contains(code) ? code : "auto"; }
        internal static string Resolve(string code, string system)
        {
            if (code != "auto" && Languages.Contains(code)) return code;
            if (Languages.Contains(system)) return system;
            string neutral = system.Split('-')[0];
            return Languages.Contains(neutral) ? neutral : "en";
        }
        internal static void SetLanguage(string code)
        {
            Language = Resolve(Normalize(code), CultureInfo.CurrentUICulture.Name);
            translations = Load(Language);
            foreach (var action in refresh.ToArray()) action();
        }
        internal static Dictionary<string, string> Load(string code)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Prefix + code + ".json"))
            {
                if (stream == null) return new Dictionary<string, string>();
                using (var reader = new StreamReader(stream)) return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
            }
        }
        internal static bool SamePlaceholders(string source, string target)
        {
            return string.Join(",", Regex.Matches(source, @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(s => s)) ==
                string.Join(",", Regex.Matches(target, @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(s => s));
        }
        internal static string T(string source)
        {
            if (string.IsNullOrEmpty(source) || Language == "en") return source;
            string translated;
            if (translations.TryGetValue(source, out translated) && !string.IsNullOrWhiteSpace(translated) && SamePlaceholders(source, translated)) return translated;
            if (source.Contains("\n")) return string.Join("\n", source.Split('\n').Select(T));
            // Explicit catalog templates also cover status strings assembled by existing providers.
            foreach (string key in english.Keys.Where(k => k.Contains("{0}")).OrderByDescending(k => k.Length))
            {
                if (!translations.TryGetValue(key, out translated) || string.IsNullOrWhiteSpace(translated) || !SamePlaceholders(key, translated)) continue;
                Regex pattern;
                if (!patterns.TryGetValue(key, out pattern))
                {
                    string expression = Regex.Escape(key);
                    expression = Regex.Replace(expression, @"\\\{(\d+)}", m => "(?<p" + m.Groups[1].Value + ">.*?)");
                    patterns[key] = pattern = new Regex("\\A" + expression + "\\z", RegexOptions.CultureInvariant);
                }
                var match = pattern.Match(source);
                if (!match.Success) continue;
                int count = Regex.Matches(key, @"\{\d+\}").Cast<Match>().Select(m => int.Parse(m.Value.Trim('{', '}'), CultureInfo.InvariantCulture)).Max() + 1;
                try { return string.Format(CultureInfo.CurrentCulture, translated, Enumerable.Range(0, count).Select(i => (object)T(match.Groups["p" + i].Value)).ToArray()); }
                catch (FormatException) { return source; }
            }
            return source;
        }
        private static void Watch(Control owner, Action action)
        { refresh.Add(action); owner.Disposed += delegate { refresh.Remove(action); }; }
        internal static void Bind(Control root, bool translateTitle = true)
        {
            if (root is ComboBox) Watch(root, delegate { root.Invalidate(); });
            if (translateTitle && !(root is ComboBox) && !(root is TextBoxBase))
            {
                string source = root.Text; bool applying = false;
                Action apply = delegate { applying = true; root.Text = T(source); applying = false; root.Invalidate(); };
                root.TextChanged += delegate { if (!applying) { source = root.Text; apply(); } };
                Watch(root, apply); apply();
            }
            foreach (Control child in root.Controls) Bind(child);
        }
        internal static void Bind(ToolStripItem item, Control owner)
        {
            string source = item.Text; bool applying = false;
            Action apply = delegate { applying = true; item.Text = T(source); applying = false; };
            item.TextChanged += delegate { if (!applying) { source = item.Text; apply(); } };
            Watch(owner, apply); apply();
        }
        internal static void Hint(ToolTip tip, Control control, string source)
        {
            Dictionary<ToolTip, string> values;
            if (!hints.TryGetValue(control, out values))
            {
                hints[control] = values = new Dictionary<ToolTip, string>();
                var captured = values;
                Watch(control, delegate { foreach (var value in captured) value.Key.SetToolTip(control, T(value.Value)); });
                control.Disposed += delegate { hints.Remove(control); };
            }
            values[tip] = source; tip.SetToolTip(control, T(source));
        }
        internal sealed class Choice
        {
            internal readonly string Key;
            internal Choice(string key) { Key = key; }
            public override string ToString() { return T(Key); }
        }
    }
}
