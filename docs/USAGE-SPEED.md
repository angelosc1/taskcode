# Recent consumption speed

## Meaning

The details panel estimates time to quota exhaustion from recent observations, separately for every provider and quota period. These filled, right-pointing arrows are different from the downward elapsed-time marker:

| Indicator | Projected time to exhaustion divided by time until reset |
| --- | --- |
| `›` | More than 1.2: quota should last beyond reset with headroom |
| `››` | From 0.8 through 1.2: quota is projected to run out around reset |
| `›››` | Less than 0.8: quota is projected to run out well before reset |

The estimate is remaining quota divided by the observed consumption rate. The rate is measured in percentage points per hour; its observation window adapts to the source cadence: **15 seconds** for the demo, **20 seconds** for ten-second Codex updates, and up to **10 minutes** for five-minute Claude quota updates. Details show approximate remaining usage time and identify the source. An estimate assumes the recent rate continues; it is not a guarantee.

## Initial estimate

Before enough recent observations arrive, Details can show a forecast labelled **period avg**, with arrow levels computed from the average consumed quota since reset. It uses the timestamp of the actual quota reading, does not add samples to the recent estimator, and automatically yields to the recent estimate. While a provider is running, a fresh but unchanged quota reading can also use its own period average; this keeps coarse weekly percentages from falsely implying that execution stopped. Each window uses its own consumed quota and elapsed time, without borrowing the five-hour rate for the weekly forecast. It never replaces stale or unavailable readings. Zero measured consumption shows a waiting status without arrows. Taskbar speed-change hints continue to use recent observations only. Details forecasts and arrows follow the same per-provider local activity signal as Ripple: when a provider stops, its row shows **Idle ? not running**, without arrows or a time forecast. Resuming restores the estimate without inventing samples or resetting history. Quota freshness warnings remain visible independently of activity.

## Calculation rules

- Require three distinct observations covering at least five seconds for frequent updates. Sparse sources such as Claude require two actual responses spanning their polling interval.
- Use a rolling window of at least 15 seconds and two expected polling intervals, interpolating the sample interval that crosses its boundary. The tooltip reports the actual measured interval.
- Require three consecutive new observations proposing a different level before changing an established arrow count.
- Do not treat duplicate, out-of-order, non-UTC, or invalid observations as new activity.
- Discard history when period bounds change, quota increases, or a sampling gap exceeds the source freshness allowance: 30 seconds for Codex and six minutes for Claude.
- Show **Calculating** before sufficient history, **Idle** without recent consumption, and a stale/expired/unavailable state when appropriate. Those states do not show speed arrows. The row stays visible with its calculation or availability status. Each quota also shows consumed percentage versus elapsed-time percentage directly above the reset countdown.
- Do not generate new observations just because the display timer ticks. A repeated unchanged reading is valid only when the data source has supplied that observation.

Provider refresh frequency limits responsiveness. Codex is queried approximately every ten seconds; Claude's direct usage check has a five-minute minimum interval and can also receive newer local status-line observations when using the matching local CLI account. Rechecking a cached response does not reveal new consumption. The estimator waits for real observations and tolerates the expected delay between polls. Missing a refresh beyond the freshness allowance shows "Waiting for fresh quota readings". Local execution activity drives Ripple independently; a running model does not itself provide a quota measurement. See [account connections](ACCOUNTS.md).

## Taskbar hints

A confirmed level change displays filled arrows for eight seconds without resizing the strip or replacing its quota values. Five-hour hints appear for the relevant provider. Weekly hints appear only when the optional weekly indicator is enabled. In the two-row layout, five-hour arrows appear above the provider symbol and weekly arrows below it. In the single-period Lines style, arrows appear just above the thin meter.

The arrowheads are vector shapes with subtly rounded corners and narrow spacing. Their opacity follows a smooth 1.25-second cycle, with peaks delayed from left to right. A faint 20% baseline keeps the arrow count discernible. Rendering targets 30 frames per second, independently of data sampling. Taskbar animation runs only during the temporary hint; details animation runs only while the panel is visible and has active speed estimates. The Windows client-area animation preference and high-contrast mode are respected by showing static shapes instead.

## Testing the prototype

Start with `--demo` and use **Simulate speed** to cycle through Mixed, Comfortable, Near reset, Fast, Idle, and Calculating scenarios. It opens details and creates clearly simulated histories; it does not consume real quota or read account data. Normal live mode offers **Refresh accounts** instead.

The demo initially seeds fifteen seconds of past activity. Subsequent synthetic observations arrive every two seconds with unchanged quota, so the simulated burst naturally decays to Idle when no new activity is simulated. Clicking the button seeds another history and previews changed arrow counts immediately. This explicit preview does not bypass stabilization for real incoming observations.

The self-test includes regression checks for rates, projections, warmup, stale and invalid readings, interpolation, resets, resuming from Idle, three-observation stabilization, and sequential opacity transitions. Offscreen artifacts include `details-check.png`, `speed-check.png`, and an `animation-check.png` contact sheet for visual review in both themes.
