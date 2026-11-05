# Roadmap

## 1. Prepare the repository

- [x] Document the proposal and the current project status.
- [x] Record the three selected display styles.
- [x] Store the mockup in the project.
- [x] Configure exclusions for local files and credentials.
- [ ] Create and publish the public GitHub repository.
- [ ] Choose a license before publishing reusable code.

## 2. Validate Windows integration

- [ ] Identify the Windows version, monitors, and display scaling used for initial testing.
- [ ] Investigate ways to position the indicator to the left of the system tray.
- [ ] Build a small proof of concept before choosing the final technology stack.
- [ ] Compare integration and overlay approaches: reserved space, clicks, resizing, and stability.
- [ ] Check taskbar auto-hide, multiple monitors, DPI scaling, and Explorer restarts.
- [ ] Document the chosen approach and its limitations.

## 3. Build the visual prototype

- [ ] Implement the Lines style using sample data.
- [ ] Add the hover panel and keyboard access.
- [ ] Add Essential and Blocks.
- [ ] Save preferences and support light and dark mode.

## 4. Connect providers

- [ ] Investigate data sources and authentication for Claude and Codex separately.
- [ ] Document available fields, limitations, and any dependencies on non-public interfaces.
- [ ] Distinguish subscription limits, usage, and API spending.
- [ ] Implement secure credential storage if needed, keeping credentials out of the repository.
- [ ] Implement an appropriate refresh interval and handle expired authentication.
- [ ] Show unavailable data explicitly, without inventing quota values or treating zero as a successful reading.

## 5. Publish a test release

- [ ] Validate behavior across supported Windows configurations.
- [ ] Document installation, removal, and privacy.
- [ ] Prepare a reproducible package and evaluate executable signing.
- [ ] Publish a test release with known limitations.

There is no fixed timeline. Integration feasibility will guide implementation.
