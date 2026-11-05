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

Detail bars use the same fill convention as the taskbar: colored remaining quota on the left and muted used quota on the right. A downward arrow independently advances from left to right as the period elapses. Its position represents elapsed time, while the colored bar represents remaining quota; their percentages use opposite directions. Use the tooltip's numeric budget-pace comparison to compare consumption with elapsed time.

The arrow starts at zero on reset and reaches the end at renewal. Unknown or invalid time bounds hide the arrow instead of guessing. Expired data stays at the end until a new period is supplied. The tooltip includes used quota, elapsed time, and budget pace; a two-percentage-point tolerance is treated as on pace.

The prototype uses the five-hour window as the primary value, with weekly quota in details and optionally in the indicator. Provider adapters use the reported reset timestamps and documented/matched period durations; missing windows stay unavailable.

Offline preview values are simulated; normal startup uses the selected account source. See [account connections](ACCOUNTS.md) for sign-in, privacy, and provider limitations.

## Behavior

- Hover opens a panel containing only the selected provider's five-hour and weekly details. The hit regions follow each rendered style and optional weekly layout. Moving to another provider replaces the current panel; both panels are never shown together.
- **Open details** defaults to **Hover**, with a saved **Click only** alternative. Click-only mode disables both native hover events and dwell-based opening. Changing the preference dismisses the current panel immediately.
- Support click and keyboard access rather than relying exclusively on hover.
- Show the last update time and connection state.
- Make missing or stale data explicit.
- Color helps distinguish providers; text and numbers must identify them as well.
- Use the selected Ripple only during observed local execution, respecting Windows motion preferences. Idle and unknown states remain still. See [activity detection](ACTIVITY.md).

## Current visual references

The repository uses previews rendered by the current application:

- [Three implemented indicator styles](images/current-indicators.png), with weekly readings off and on, in both palettes.
- [Details panel](images/current-details.png), including period markers and recent-usage estimates.
- [Settings window](images/current-settings.png), including interaction, weekly quota, and opt-in startup controls.

These are offscreen application renders with illustrative data, not photographs of an installed taskbar or live provider readings.
