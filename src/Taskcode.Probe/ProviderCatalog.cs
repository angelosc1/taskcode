using System;
using System.Drawing;

namespace Taskcode.Probe
{
    internal static class ProviderCatalog
    {
        internal const int Count = 9;
        internal static readonly string[] Ids = { "cursor", "antigravity", "glm", "grok", "copilot", "opencode", "commandcode", "qoder", "deepseek" };
        internal static readonly string[] Names = { "Cursor", "Antigravity", "GLM Coding Plan", "Grok", "GitHub Copilot", "OpenCode Go", "Command Code", "Qoder", "DeepSeek Harness" };
        internal static readonly string[] Marks = { "Cu", "Ag", "GL", "Gr", "GH", "OC", "CC", "Qo", "DS" };
        internal static readonly string[] ShortNames = { "Cursor", "Antigrav.", "GLM", "Grok", "Copilot", "OpenCode", "Command", "Qoder", "DeepSeek" };
        internal static readonly string[] Websites = { "https://cursor.com/downloads", "https://antigravity.google/download", "https://z.ai/manage-apikey/apikey-list", "https://grok.com", "https://cli.github.com", "https://opencode.ai", "https://commandcode.ai", "https://docs.qoder.com/cli/quick-start", "https://github.com/deepseek-ai" };
        internal static readonly string[] Help = {
            "Uses the account signed in to the Cursor editor. Complete sign-in in Cursor.",
            "Uses the running Antigravity app. Complete sign-in there and keep it open.",
            "Use a Z.ai Coding Plan key, GLM_API_KEY, or the Z.ai key already configured in Claude Code, ZCode or OpenCode.",
            "Uses the official Grok CLI login. Choose Sign in to run grok login.",
            "Uses GitHub CLI or GH_TOKEN. Choose Sign in to open the official GitHub CLI login.",
            "Uses OpenCode's Go plan login or an OpenCode Go API key. Zen credit has no usage API.",
            "Uses the Command Code app login or a Command Code API key.",
            "Uses qodercli login and the official account SDK. Install the connector once, then sign in.",
            "Counts today's tokens from local DeepSeek Harness sessions. Requires Node.js 24+. No provider quota or cost is inferred." };
        internal static bool AcceptsKey(int index) { return index == 2 || index == 4 || index == 5 || index == 6; }
        internal static OptionalProvider[] Create()
        {
            var result = new OptionalProvider[Count];
            Color[] colors = { Color.FromArgb(175,191,221), Color.FromArgb(135,178,255), Color.FromArgb(139,194,190), Color.FromArgb(204,204,212), Color.FromArgb(183,159,223), Color.FromArgb(191,198,210), Color.FromArgb(220,170,126), Color.FromArgb(133,201,168), Color.FromArgb(129,157,245) };
            for (int index = 0; index < Count; index++) result[index] = new OptionalProvider(index, Ids[index], Names[index], colors[index]);
            return result;
        }
        internal static OptionalSnapshot Read(int index)
        {
            if (index == 0) return CursorUsage.Read();
            if (index == 1) return AntigravityUsage.Read();
            if (index == 7) return QoderUsage.Read();
            if (index == 8) return DeepSeekUsage.Read();
            return SubscriptionUsage.Read(index);
        }
    }
}
