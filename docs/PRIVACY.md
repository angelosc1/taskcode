# Privacy

Taskcode runs locally. It has no Taskcode account, cloud backend, telemetry or analytics.

## Provider connections

The nine [additional Windows providers](OPTIONAL-PROVIDERS.md) are disabled by default. Disabled providers do not read credentials or poll. Enabling one authorizes only its documented adapter. Borrowed credentials are read-only, while explicit keys are stored with Windows DPAPI under `%LOCALAPPDATA%/Taskcode/provider-keys` and can be removed in the connection screen. Optional quota snapshots remain in memory; only retry deadlines survive restarts. Qoder's official SDK/runtime owns its sign-in; DeepSeek Harness reads local token counters and emits only totals. Neither connector initiates model inference. Antigravity's certificate exception is scoped to its local loopback request.

The official Claude/Codex clients own authentication. New sign-ins use isolated Taskcode profiles; existing-account mode reads the corresponding CLI profile. Taskcode reads Claude's access token into memory solely for the documented provider usage request. Codex authentication remains inside its local app-server. Tokens are not stored in Taskcode preferences or lifecycle logs. [Implementation and endpoints](ACCOUNTS.md).

Local activity detection reads lifecycle markers from Codex session files and Taskcode's Claude Code hooks. It does not retain prompts, model responses, tool outputs or session transcripts. Activity detection for the Claude website/Desktop app is not implemented. [Activity details](ACTIVITY.md).

## Updates

With **Check automatically** enabled, Taskcode checks the public `angelosc1/taskcode` GitHub release about once a day. Manual checks use the same endpoint. GitHub receives the normal network request metadata and a Taskcode version user agent; no provider account identity, quota or credentials are included. Installers are downloaded only after you click Install and are checked against the release asset's SHA-256 digest.

## Local data

Preferences, isolated official-client profiles and safe diagnostics live under `%LOCALAPPDATA%\Taskcode`. Lifecycle logs record timestamps, lifecycle events, exception types and method names, without raw exception messages or provider payloads. Normal settings may display the account email locally; public screenshots use sample data.

Uninstall keeps preferences and isolated sign-ins for a possible reinstall, while restoring Taskcode's Claude integration and removing its startup entry. Remove that data separately only if you intend to discard it.

Never upload credentials, authentication JSON, cookies, private account profiles or raw session files to a GitHub issue.
