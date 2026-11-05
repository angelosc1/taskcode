# Recent consumption speed

## Meaning

The details panel estimates time to quota exhaustion from recent observations, separately for every provider and quota period. These filled, right-pointing arrows are different from the downward elapsed-time marker:

| Indicator | Projected time to exhaustion divided by time until reset |
| --- | --- |
| `›` | More than 1.2: quota should last beyond reset with headroom |
| `››` | From 0.8 through 1.2: quota is projected to run out around reset |
| `›››` | Less than 0.8: quota is projected to run out well before reset |

The estimate is remaining quota divided by the observed consumption rate. The rate is measured in percentage points per hour; its observation window is **15 seconds**. Details show approximate remaining usage time and identify the source. An estimate assumes the recent rate continues; it is not a guarantee.

## Calculation rules

- Require at least three distinct observations covering at least five seconds.
- Use a rolling 15-second window, interpolating the sample interval that crosses its boundary.
- Require three consecutive new observations proposing a different level before changing an established arrow count.
- Do not treat duplicate, out-of-order, non-UTC, or invalid observations as new activity.
- Discard history when period bounds change, quota increases, or a sampling gap exceeds 30 seconds.
- Show **Calculating** before sufficient history, **Idle** without recent consumption, and a stale/expired/unavailable state when appropriate. Those states do not show speed arrows.
- Do not generate new observations just because the display timer ticks. A repeated unchanged reading is valid only when the data source has supplied that observation.

Provider refresh frequency limits responsiveness. Codex is queried approximately every ten seconds; Claude's direct usage check has a five-minute minimum interval and can also receive newer local status-line observations when using the matching local CLI account. Rechecking a cached response does not reveal new consumption. Slow or absent observations leave the estimator calculating or stale. See [account connections](ACCOUNTS.md).

## Taskbar hints

A confirmed level change displays filled arrows for eight seconds without resizing the strip or replacing its quota values. Five-hour hints appear for the relevant provider. Weekly hints appear only when the optional weekly indicator is enabled. In the two-row layout, five-hour arrows appear above the provider symbol and weekly arrows below it. In the single-period Lines style, arrows appear just above the thin meter.

The arrowheads are vector shapes with subtly rounded corners and narrow spacing. Their opacity follows a smooth 1.25-second cycle, with peaks delayed from left to right. A faint 20% baseline keeps the arrow count discernible. Rendering targets 30 frames per second, independently of data sampling. Taskbar animation runs only during the temporary hint; details animation runs only while the panel is visible and has active speed estimates. The Windows client-area animation preference and high-contrast mode are respected by showing static shapes instead.

## Testing the prototype

Start with `--demo` and use **Simulate speed** to cycle through Mixed, Comfortable, Near reset, Fast, Idle, and Calculating scenarios. It opens details and creates clearly simulated histories; it does not consume real quota or read account data. Normal live mode offers **Refresh accounts** instead.

The demo initially seeds fifteen seconds of past activity. Subsequent synthetic observations arrive every two seconds with unchanged quota, so the simulated burst naturally decays to Idle when no new activity is simulated. Clicking the button seeds another history and previews changed arrow counts immediately. This explicit preview does not bypass stabilization for real incoming observations.

The self-test includes twenty-six assertions for rates, projections, warmup, stale and invalid readings, interpolation, resets, resuming from Idle, three-observation stabilization, and sequential opacity transitions. Offscreen artifacts include `details-check.png`, `speed-check.png`, and an `animation-check.png` contact sheet for visual review in both themes.
