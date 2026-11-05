# Roadmap

## 1. Prepare the repository

- [x] Document the proposal and the current project status.
- [x] Record the three selected display styles.
- [x] Store current application previews in the project.
- [x] Configure exclusions for local files and credentials.
- [x] Create and publish the public GitHub repository.
- [x] Choose a license before publishing reusable code (MIT).

## 2. Validate Windows integration

- [ ] Identify the Windows version, monitors, and display scaling used for initial testing.
- [x] Investigate ways to position the indicator to the left of the system tray.
- [x] Build a small proof of concept before choosing the final technology stack.
- [x] Confirm initial placement before the hidden-icons arrow on the test desktop (user verified).
- [ ] Compare integration and overlay approaches: reserved space, clicks, resizing, and stability.
- [ ] Check taskbar auto-hide, multiple monitors, DPI scaling, and Explorer restarts.
- [x] Document the experimental approach and its limitations.

## 3. Build the visual prototype

- [x] Implement the Lines style using sample data.
- [ ] Add the hover panel and keyboard access.
- [x] Add Essential and Blocks.
- [x] Save style and placement preferences.
- [x] Add a tray icon, close-to-tray settings, and opt-in Windows startup.
- [x] Adapt indicator and details colors to the Windows taskbar's light/dark theme.
- [x] Render the taskbar indicator without an opaque background.
- [x] Add provider-colored Ripple for observed local execution, with motion preferences and idle handling.
- [x] Use the Claude symbol in compact styles and details.
- [ ] Refine the full settings window appearance for both themes.

## 4. Connect providers

- [x] Connect Codex through the official CLI app-server and browser sign-in.
- [x] Receive Claude quota through Claude Code's official status-line input.
- [x] Document fields, installation, freshness, privacy, and client dependencies.
- [x] Distinguish subscription quota from context usage and API spending.
- [x] Delegate credential storage to the official clients; keep credentials out of Taskcode.
- [x] Refresh in the background with error backoff and reconnect controls.
- [x] Show missing, stale, expired, and disconnected data explicitly.
- [ ] Validate Claude quota delivery from a real signed-in Claude Code session.
- [ ] Investigate a separate connection for users of only the Claude website/desktop app.

## 5. Publish a test release

- [ ] Validate behavior across supported Windows configurations.
- [ ] Document installation, removal, and privacy.
- [ ] Prepare a reproducible package and evaluate executable signing.
- [ ] Publish a test release with known limitations.

There is no fixed timeline. Integration feasibility will guide implementation.
