# Design decisions

## Placement

Preferred: inside the taskbar, to the left of the hidden-icons arrow. Fallback: an overlay in that same area. Avoid covering the clock, system tray, or other applications' buttons.

Integration and overlay are technical approaches to validate, independent of the three display styles.

## Approved styles

1. **Lines (default):** names and thin horizontal bars.
2. **Essential:** compact symbols and percentages.
3. **Blocks:** horizontal segmented meters.

Proposed setting: **Settings > Appearance > Indicator style**. Selection provides an immediate preview and is saved. Light/dark mode is a separate preference.

## Data semantics

Percentages represent remaining quota and must be labeled accordingly in the details panel. Claude and Codex use the same convention. Do not mix remaining and consumed percentages.

The main indicator must identify which quota window it represents. This choice depends on the actual available data and has not yet been validated. The mockup uses a five-hour window as the primary value, with weekly limits in the details panel.

Mockup values are not real readings. Displayed reset times and limits will depend on each provider's data source.

## Behavior

- Hover opens a panel with both providers and their quota details.
- Support click and keyboard access rather than relying exclusively on hover.
- Show the last update time and connection state.
- Make missing or stale data explicit.
- Color helps distinguish providers; text and numbers must identify them as well.
- Avoid continuous animations and distracting taskbar behavior.

## Mockup

The [comparison board](images/taskcode-concepts-en.png) was generated with the built-in image tool. It shows four concepts; the first three were approved as display styles. The fourth illustrates overlay placement.

Visual brief: Taskcode monitor for Windows 11 in dark mode, to the left of the system tray arrow, Claude in terracotta and Codex in soft mint, four concepts in a 2 by 2 grid with hover panels, understated typography, and illustrative remaining-quota percentages.

Localization brief: translate all labels into English while preserving the approved layout, display styles, colors, provider names, and sample values.
