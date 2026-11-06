# Changelog

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
