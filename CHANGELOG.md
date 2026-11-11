# Changelog

## 0.4.5 - 2026-11-11

- Add the usage atom: while a provider works, 2, 4 or 6 small spheres orbit its spinning logo for light, medium or heavy usage pace (the same levels as the Details speed arrows). Each sphere has its own tilted path, neighbours run in opposite directions, and a comet tail in the provider color trails behind. With Windows animations off the spheres stay still and still show the level. Turn it off in Appearance > Usage atom to keep only the spinning logo; the choice is kept across updates.
- Make the settings window slightly taller to fit the new switch, and shorten the Portuguese time marker label so it is no longer cut off.

## 0.4.4 - 2026-11-11

- Add a time marker to every indicator style, showing how much of each quota period has passed: arrows between the bars (default), arrows outside the bars, a line, or a lighter trail. Donuts gain a start mark at 12 o'clock. Optional pace colors turn the marker green while usage stays within the elapsed time and white once it gets ahead. The choice is saved and kept across updates.
- Move the Details button beside the Appearance heading to make room for the new options.
- Fix Claude quotas showing as unavailable right after a reset, when the service still reports the retired window: show the window as reset and check again after 30 seconds instead of five minutes.

## 0.4.3 - 2026-11-11

- Stop the Details card border and logo from flickering: the card was fully repainted every second because unchanged themes compared as different. The card is now double-buffered.
- Keep the indicator in place for up to 15 seconds while a taskbar measurement is late or briefly fails, instead of hiding it. When only the system tray resizes, follow its new edge immediately; taskbar buttons are still never covered.
- Keep hover Details working: the hover area follows the indicator while it moves or scrolls, a provider split across the scrolling edge uses the piece under the pointer, and the card is raised above other topmost windows when shown.

## 0.4.2 - 2026-11-07

- Never send expired Claude tokens, which the usage service answers with a one-hour rate limit. Renew shortly before expiry by briefly starting the official CLI, clear cooldowns when the token changes, and retry once after a rejected token.
- Keep the last Claude reading visible for up to an hour while checks fail or are rate limited.
- Start on the taskbar only; settings open on first run, on a second launch or from the icon.

## 0.4.1 - 2026-11-07

- Fix Claude quota disappearing hours after connecting: Claude now uses only the existing Claude Code account, which the CLI keeps refreshed. Existing installs switch automatically without signing in again.
- Report expired Grok and OpenCode sessions as expired, with renewal instructions.
- Use a new close button on Details cards.

## 0.4.0 - 2026-11-07

- Replace activity Ripple with rotating 3D provider logos, 15% extrusion, softer side/back shading, and a fixed-direction diagonal reflection only while active. Increase the Codex mark slightly; keep Details headers static.
- Map Claude/Codex rotation to recent-speed levels and local activity. Animate optional providers from measured quota/token increases, stopping on unchanged, stale or disconnected readings.
- Bound the indicator to the verified free taskbar gap and scroll excess providers right to left at normal size. Pause for hover/Details, retain correct click routing, and separate the last/first providers.
- Remove the temporary animation tester and sample-data toggle; retain synthetic data only in excluded regression checks.

## 0.3.1 - 2026-11-06

- Replace optional-provider initials with embedded vector logos in all indicator styles, the provider list and provider headers.
- Prevent brief taskbar measurement failures from immediately hiding the indicator; keep verified geometry within the existing four-second freshness limit, retain live geometry checks, and avoid native frame changes when placement is unchanged.
- Refresh the transparent surface on visibility recovery and record indicator pause/resume reasons in the bounded lifecycle log.

## 0.3.0 - 2026-11-06

- Add Dot Matrix and Color Strip styles with five-hour/weekly modes, independent consumption colors and existing provider visibility controls.
- Tighten Sparkline value/chart and weekly-row spacing; reduce donut size and center donut content with balanced cell separators and padding.
- Fit the settings preview immediately on startup so saved wider styles stay within their preview card.

- Fix Details closing immediately from settings or when taskbar placement is paused. Hover cards still dismiss on pointer exit; explicitly opened cards stay pinned until closed or clicked outside, without stealing focus.

- Add the Sparkline style with current usage, percentage-point change and compact measured history; support five-hour/weekly rows, usage colors, optional providers and visibility controls.
- Keep trend history in memory with bounded retention; restart after quota rollover, corrections, errors, disconnects or source gaps. Never create trend observations from cached readings or rendering.

- Add the Donuts indicator style: a mini ring beside each provider mark, five-hour or five-hour/weekly display, independent usage colors, existing activity Ripple and provider visibility controls.

- Give Claude and Codex the same monitoring toggle as the other providers, retaining their enabled defaults and account profiles. Disabled providers disappear from the indicator, whose layout reflows; an empty selection hides it entirely.

- Add optional Cursor, Antigravity, GLM Coding Plan, Grok, GitHub Copilot, OpenCode Go, Command Code, Qoder and DeepSeek Harness monitoring, all disabled by default.
- Add provider connection settings with official app/CLI sign-in, encrypted per-user API keys where supported, refresh, disconnect and Qoder connector installation.
- Show each provider's own quota windows and reset dates; show DeepSeek Harness local token counts without inventing a quota or price.
- Keep optional polling independent, discard disconnected in-flight results, and preserve server rate-limit cooldowns across restarts.
- Expand indicator rendering/routing, English/Portuguese text, provider fixtures and sidecar checks.

## 0.2.0 - 2026-11-06

- Add System, English and Brazilian Portuguese language selection with immediate switching and per-user persistence.
- Translate settings, details, menus, tooltips, connection/update status and the Windows installer.
- Embed discoverable JSON language catalogs with English fallback and placeholder checks.
- Add contributor and translation guides for community submissions.

## 0.1.3 - 2026-11-06

- Show exhausted quotas without consumption-speed arrows, independently of execution activity.
- Remove animation preview controls from settings, the context menu and the command line.
- Publish and verify release notes as plain Markdown, avoiding PowerShell object metadata.

## 0.1.2 — 2026-11-06

- Preserve a fresh zero-use Claude session without a reset as available, waiting for its first request.
- Keep all five hours available until the provider reports the next session's reset; do not synthesize a local countdown.
- Refresh once when a known reset is crossed, with existing network/rate-limit backoff preserved.
- Recheck a waiting session on local execution start, with a bounded activity hint.
- Support canonical Claude usage windows and retain the named idle-session fallback.

## 0.1.1 — 2026-11-05

- Refresh the managed Claude Code hook helper during installation and updates, including migrations from a development build.
- Keep the installed application and its active integration helper on the same version.

## 0.1.0 - 2026-11-05

First public Windows test release.

- Added Essential, Lines and Blocks inside a dark taskbar capsule.
- Added separate provider Details with elapsed-time markers, reset countdowns and usage estimates.
- Connected Claude and Codex through their official clients, with isolated account profiles.
- Added local execution Ripple, optional weekly indicators and consumption-based bar colors.
- Added per-user installation, a portable package, optional Windows startup and integration-aware removal.
- Added daily/manual release checks and SHA-256-verified installer updates.
- Kept developer diagnostics and sample-data entry points out of the release build.

See [installation and updates](docs/INSTALL.md) and [known limitations](docs/release-notes/0.1.0.md).
