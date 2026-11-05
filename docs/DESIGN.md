# Design decisions

## Placement

Preferred: inside the taskbar, to the left of the hidden-icons arrow. Fallback: an overlay in that same area. Avoid covering the clock, system tray, or other applications' buttons.

Integration and overlay are technical approaches to validate, independent of the three display styles.

## Approved styles

1. **Lines (default):** names and thin horizontal bars.
2. **Essential:** compact symbols and percentages.
3. **Blocks:** horizontal segmented meters.

Weekly readings are opt-in through **Show weekly quota in the indicator**, disabled by default. When enabled, each provider is identified by its symbol and receives two rows labeled `5h` and `7d`, using the selected style. The height stays constant; the width expands to keep both readings legible. The detail panel always retains both periods.

The **Indicator style** setting provides an immediate preview and saves the selection. The indicator palette follows the Windows taskbar's light/dark mode.

## Data semantics

Percentages represent remaining quota and must be labeled accordingly in the details panel. Claude and Codex use the same convention. Do not mix remaining and consumed percentages.

In the details panel, quota bars also compare consumption with elapsed time. The muted segment on the left is used quota; the colored segment on the right is remaining quota. The boundary between them advances with consumption. A downward arrow above each bar advances from left to right as the period elapses. When the consumption boundary is to the right of the arrow, usage is faster than the period's time pace. This direction applies to the detail bars; taskbar indicators continue to show remaining quota in their compact styles.

The arrow starts at zero on reset and reaches the end at renewal. Unknown or invalid time bounds hide the arrow instead of guessing. Expired data stays at the end until a new period is supplied. The tooltip includes used quota, elapsed time, and budget pace; a two-percentage-point tolerance is treated as on pace.

The prototype uses the five-hour window as the primary value, with weekly quota in the details panel and optionally in the indicator. Real provider fields and period definitions still need validation.

Prototype values are not real readings. Displayed reset times and limits will depend on each provider's data source.

## Behavior

- Hover opens a panel with both providers and their quota details.
- **Open details** defaults to **Hover**, with a saved **Click only** alternative. Click-only mode disables both native hover events and dwell-based opening. Changing the preference dismisses the current panel immediately.
- Support click and keyboard access rather than relying exclusively on hover.
- Show the last update time and connection state.
- Make missing or stale data explicit.
- Color helps distinguish providers; text and numbers must identify them as well.
- Avoid continuous animations and distracting taskbar behavior.

## Current visual references

The repository uses previews rendered by the current application:

- [Three implemented indicator styles](images/current-indicators.png), with weekly readings off and on, in both palettes.
- [Details panel](images/current-details.png), including period markers and recent-usage estimates.
- [Settings window](images/current-settings.png), including interaction, weekly quota, and opt-in startup controls.

These are offscreen application renders with illustrative data, not photographs of an installed taskbar or live provider readings.
