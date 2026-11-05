# Account connections

Taskcode reads subscription quota through the official local clients. It does not ask for passwords, read browser cookies, copy OAuth tokens, or send data to a Taskcode server. API spending and context-window percentages are not subscription quota.

## Codex

Install the official [Codex CLI](https://developers.openai.com/codex/cli/) and sign in with the ChatGPT account you want to monitor. Taskcode automatically reuses that local login. **Connect Codex** refreshes an existing connection; if the CLI reports no signed-in account, it opens the CLI's browser sign-in flow.

The integration launches its own hidden `codex app-server` process, performs the initialization handshake, reads `account/read`, and calls `account/rateLimits/read`. No conversations or model turns are started. See the [official protocol](https://developers.openai.com/codex/app-server/).

Windows discovery supports the standard npm x64 installation, `.local/bin/codex.exe`, and native `codex.exe` installations on PATH. An API-key login cannot supply ChatGPT subscription quotas. If authentication expires, use **Connect Codex** and complete sign-in in your browser.

Quota refreshes approximately every ten seconds while connected. Connection failures back off for sixty seconds; **Refresh accounts** retries immediately. Only 300-minute and 10,080-minute windows are assigned to the five-hour and weekly indicators. A weekly-only plan stays weekly-only. Other quota pools and unusual window lengths are not relabeled as five-hour quota.

**Disconnect Codex from Taskcode** stops monitoring and clears its readings. It preserves the shared Codex CLI login.

## Claude

**Connect Claude** integrates with the [Claude Code status line](https://code.claude.com/docs/en/statusline). Sign in using Claude Code's own login. A current version must provide `rate_limits.five_hour` and/or `rate_limits.seven_day` in its status-line input.

The bridge receives these observations whenever Claude Code updates its status line. It saves only quota percentages, reset timestamps, and an observation timestamp to `%LOCALAPPDATA%/Taskcode/claude-usage.json`. It does not save the full status-line input, account identity, conversation, workspace, or credentials. Taskcode checks this local file once per second, without inventing additional observations.

The connection copies `Taskcode.ClaudeBridge.exe` into `%LOCALAPPDATA%/Taskcode/` and updates `statusLine` in `%USERPROFILE%/.claude/settings.json` (or `CLAUDE_CONFIG_DIR`). Other settings are preserved. The original status-line definition is backed up locally; its original command continues receiving the same input and its output is forwarded. With no existing status line, the bridge displays a compact quota summary in Claude Code. Disconnecting restores the original definition and clears the cached quota.

Open or resume Claude Code and use it to receive the first reading. A missing quota field means unavailable, not zero. Taskcode cannot fetch fresh Claude quota while Claude Code supplies no observations. This connection does **not** independently sign in to the Claude desktop app or website; users who only use those interfaces will not receive readings through this integration.

## Freshness and speed

The strip shows remaining quota. `--` means missing, expired, disconnected, or older than two minutes. Details identify the source and show the age of the last observation. Temporary connection failures retain the last recent reading until that freshness limit expires. Successful responses with a missing window clear that window immediately.

Time markers use the provider's reset timestamp and the corresponding five-hour or seven-day duration. Fractional percentages are retained for speed calculation; displayed percentages are rounded. Speed arrows require enough actual fresh observations within the existing rolling fifteen-second estimator. They may remain calculating or stale when a source does not update frequently enough. Repainting the interface and rereading an unchanged Claude cache never create artificial speed samples.

## Development and removal

Normal startup uses live data. `--demo` starts an explicit offline design preview with simulated values and the **Simulate speed** button. Self-tests and tray tests use this offline mode and never connect accounts or change Claude settings.

`--accounts-check` writes a sanitized source check to the ignored `out/probe/accounts-check.txt`; it never writes tokens or raw service responses. `--connect-claude` installs the same bridge as the settings button.

Disconnect Claude before removing Taskcode so its status line is restored. Disable **Start with Windows** before moving the portable executable. Removing `%LOCALAPPDATA%/Taskcode` before restoring the status line would remove its bridge and backup.

This remains an experimental Windows prototype. Provider client versions, quota fields, network access, and local sign-in determine availability.
