# Ripple activity indicator

While a provider has an observed running turn, two soft rings expand and fade around its symbol. Claude uses its peach accent and Codex its mint accent. Each provider works independently. A quota refresh, an open application, and a change in quota are not treated as proof of a running turn.

The cycle lasts 2.21 seconds, with the two rings half a cycle apart. The strip keeps its transparent background. The effect uses the existing 30 FPS timer only while activity or a temporary speed hint needs animation. When Windows disables client-area animations or high contrast is active, a small static activity dot replaces moving rings. Idle or unknown activity has no ripple.

Lines now includes the provider symbols alongside the names so the same ripple has a symbol to surround in every style. Essential, Blocks, and the weekly layouts keep their existing arrangement. Existing recent-consumption arrows describe quota velocity and remain separate from execution activity.

## Claude Code

Connecting Claude adds command hooks for `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `PermissionRequest`, `Stop`, `StopFailure`, and `SessionEnd`. See the [official hooks reference](https://code.claude.com/docs/en/hooks). Start and tool events mark a session working; permission waits, stops, failures, and session ends clear it. Any active observed session can activate Claude's ripple.

The bridge retains only the event name and UTC timestamp, in one local file per hashed session identifier under `%LOCALAPPDATA%/Taskcode/activity/`. Hook inputs are not saved, printed, or sent to a server. Existing hooks remain installed; reconnecting does not duplicate Taskcode hooks. Disconnecting removes only Taskcode's hook commands and restores the original status line. Restart Claude Code after installing or updating the hooks so new sessions load them.

This detects Claude Code activity, not independent activity in the Claude website or desktop app. Blocked hooks, unsupported clients, and missing lifecycle events can prevent accurate detection.

## Codex

Taskcode reads lifecycle records from local Codex session JSONL files under `CODEX_HOME/sessions` or `%USERPROFILE%/.codex/sessions`. The [Codex protocol](https://github.com/openai/codex/blob/main/codex-rs/protocol/src/protocol.rs) identifies `task_started`/`turn_started`, `task_complete`/`turn_complete`, and `turn_aborted` records. Only records with the `event_msg` envelope affect activity; similarly named text in messages is ignored.

Discovery checks the latest 32 session files in today's and yesterday's UTC directories, every three seconds. Reading is incremental, in the background, approximately every 750ms. On initial discovery, at most the last 1 MiB is inspected. The monitor retains byte offsets and lifecycle state in memory; it does not retain prompts, generated responses, tool outputs, or transcripts. There are no requests to model inference or changes to Codex configuration.

An already-running turn whose start is outside the initial tail may remain unknown until a new observed turn starts. Clients that do not write compatible local lifecycle records cannot activate the effect. A missing completion or crash expires after thirty minutes without a newer recognized activity event. Multiple sessions are aggregated so one idle session cannot stop another working session.

## Verification

Self-tests cover start, stop, permission waiting, event order, stale activity, malformed records, message spoofing, hook idempotency/restoration, staggered rings, and transparent rendering. `out/probe/ripple-check.png` is an offscreen comparison across styles and themes. `--activity-check` writes only a sanitized working/idle-or-unavailable summary to the ignored output directory.

Offline `--demo` mode animates both providers for fifteen seconds on startup and after **Simulate speed** in active scenarios. It is explicitly simulated and does not write live activity files.
