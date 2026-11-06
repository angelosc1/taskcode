# Additional Windows providers

**Accounts > Manage providers** lists Claude and Codex alongside the nine additions. The same **Enable monitoring** switch applies to every provider. Claude and Codex default to enabled; the other nine default to disabled. Disabling a provider removes it from the indicator, stops its polling and hides its details while preserving the official login and selected profile. Re-enabling resumes that profile. The indicator shrinks around the remaining providers and is hidden when none are enabled. These preferences survive restarting Taskcode.

Open **Settings > Accounts > Manage providers**, select a service and choose **Connect**. All nine additional providers start disabled on both fresh installs and upgrades. Monitoring is enabled per service and remembered per Windows user. Turning a service off stops future polling, discards in-flight results and clears its displayed readings without signing out its official client.

The provider set was checked against Codenotch's Windows source at commit [`183fc2f835dbe46855cf1bf0f7c03e05d5f811c8`](https://github.com/vinzdg/codenotch/tree/183fc2f835dbe46855cf1bf0f7c03e05d5f811c8/windows/codenotch/src). OpenCode/OpenCode Go refer to the same subscription adapter, not two separate accounts. DeepSeek Harness is the Windows local-session adapter; it is different from the macOS DeepSeek Platform browser integration. The broader macOS provider list is not assumed to be implemented on Windows.

| Provider | What Taskcode displays | Connection |
| --- | --- | --- |
| Cursor | Included, API and on-demand usage; actual billing-cycle dates | Reuses the Cursor editor's account from `%APPDATA%/Cursor/User/globalStorage/state.vscdb`, opened read-only with WAL support. **Sign in / Open app** opens Cursor, or its official download page if absent. Sign in inside the editor. |
| Antigravity | Every reported model group's quota buckets, with provider reset dates | Open and sign in to the Antigravity IDE. Taskcode discovers its authenticated loopback language-server endpoint. This adapter requires the app's local server to remain running; standalone `agy` CLI reads and Google Credential Manager fallback are not included. |
| GLM Coding Plan | Session, weekly and MCP quotas when reported | Paste a Coding Plan key, choose Z.ai or BigModel, then **Save key and connect**. Also accepts `GLM_API_KEY`, a Z.ai-bound key in Claude Code settings, ZCode's plaintext Coding Plan configuration or a GLM-named OpenCode entry. Encrypted ZCode credential vaults are not decrypted; use manual key entry for those installations. |
| Grok | Grok Build credit usage and its reported period | **Sign in / Open app** runs `grok login` if installed. Reads only valid xAI-issued sessions from `~/.grok/auth.json`; unrelated customer-IdP sessions are never sent to the public Grok service. Expired sessions require renewal through the official CLI. |
| GitHub Copilot | Premium requests, chat and completion quotas; skips unlimited allowances | **Sign in / Open app** runs `gh auth login --hostname github.com --web`. Uses the GitHub CLI's own token read or `GH_TOKEN`/`GITHUB_TOKEN`. A token may also be entered explicitly. The token needs access to the account's Copilot usage endpoint. |
| OpenCode Go | Rolling five-hour, weekly and monthly Go-plan quotas | **Sign in / Open app** runs `opencode auth login`. Reads the active Go/API or OpenCode OAuth credential from its read-only SQLite store or `auth.json`; also accepts `OPENCODE_APIKEY` or manual Go-key entry. Go keys and OAuth sessions use their respective endpoints. Zen pay-as-you-go credit has no supported usage API. |
| Command Code | Monthly credit spend plus five-hour/weekly limits | Sign in through the Command Code app, use `COMMAND_CODE_API_KEY`, or paste the service's API key. Reads `~/.commandcode/auth.json` without modifying it. Billing requests retain the account's reported organization and billing-cycle start. |
| Qoder | Account plan, add-on and organization usage | Install Node.js, choose **Install Qoder connector**, then **Sign in / Open app** to run `qodercli login`. The connector installs the official `@qoder-ai/qoder-agent-sdk@1.0.59` and its official runtime in the Taskcode user directory. It calls only `auth()` and `getUsageInfo()` on a temporary SDK daemon; it does not create conversations or inference. |
| DeepSeek Harness | Today's input/output/cache token counts and session count | Uses local `~/.dsh/sessions/**/session.v4.jsonl.zstd` files. Install Node.js 24+ and connect. Concatenated compressed frames are decoded individually. No account token or network call is needed; this does not display a provider quota or infer a dollar cost. |

## Connection controls

**Connect** turns on monitoring. **Sign in / Open app** opens the official client or its setup page. Complete authentication there; Taskcode does not receive passwords or authorization codes. The supported CLI logins run visibly, can be cancelled, and time out after fifteen minutes. Closing the additional-provider connection window also cancels any login/setup process it owns; existing desktop apps are left running.

For services accepting explicit keys, the entry is masked and **Save key and connect** stores it encrypted with Windows DPAPI for the current user. Preferences and logs contain no plaintext key. **Forget saved key** removes only Taskcode's encrypted copy and disables that provider. It leaves borrowed official-client logins intact.

**Refresh** checks when a request is eligible. Automatic and manual checks share a five-minute attempt floor and honor the complete `Retry-After` deadline. Rate-limit deadlines survive restarting Taskcode. Polling runs separately for each optional provider; a cold Qoder read cannot block Claude/Codex or another provider. Owned helper processes have timeouts and Windows job-object cleanup.

## Display and limitations

Enabled services append their own indicators. Hover/click opens the corresponding card, including additional quota rows. The cards retain provider-specific periods and model groups; missing timing does not become a fabricated five-hour or weekly countdown. Observations older than six minutes and expired quotas are not drawn as current percentages. When all selected indicators cannot fit the verified taskbar slot, reduce the enabled providers or select Essential.

These additions monitor allowance or local counts. Claude/Codex execution Ripple and consumption-speed forecasts are unchanged; no execution activity or forecast is invented for the new services. The DeepSeek count has no percentage denominator and never becomes a quota meter.

The internal provider endpoints can change or reject access. The implementation is checked with synthetic response fixtures, a live-WAL SQLite fixture, mocked SDK account calls, compressed DeepSeek fixtures, disconnect races, all provider hit regions and rendered English/Portuguese UI. Live quotas and sign-in completion require the corresponding installed client and user account; those are not certified by fixture tests.

## Endpoints and local access

Cursor sends its editor session only to `https://cursor.com/api/usage-summary`. GLM sends its plan key only to `https://api.z.ai/api/monitor/usage/quota/limit` or `https://open.bigmodel.cn/api/monitor/usage/quota/limit`. Grok uses `https://cli-chat-proxy.grok.com/v1/billing?format=credits`; Copilot uses `https://api.github.com/copilot_internal/user`; OpenCode uses its `https://opencode.ai/zen/go/v1/usage` or `https://opencode.ai/inference/go/v1/usage` endpoint. Command Code uses its `/alpha/whoami`, `/alpha/billing/credits`, `/alpha/billing/subscriptions` and `/alpha/usage/summary` endpoints on `api.commandcode.ai`.

Antigravity's request is limited to the discovered local process's listener and `/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary`; its certificate exception applies to that single `127.0.0.1` request, never public HTTPS. Qoder authentication and account networking stay inside its official SDK/runtime. DeepSeek reads counts locally and returns only totals. No optional provider persists raw account responses, prompts or transcripts.
