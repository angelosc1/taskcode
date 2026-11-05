# Recent consumption speed

## Meaning

The details panel estimates time to quota exhaustion from recent observations, separately for every provider and quota period. These right-pointing chevrons are different from the downward elapsed-time marker:

| Indicator | Projected time to exhaustion divided by time until reset |
| --- | --- |
| `›` | More than 1.2: quota should last beyond reset with headroom |
| `››` | From 0.8 through 1.2: quota is projected to run out around reset |
| `›››` | Less than 0.8: quota is projected to run out well before reset |

The estimate is remaining quota divided by the observed consumption rate. The rate is measured in percentage points per hour; its observation window is **15 seconds**, as requested. The details show approximate remaining usage time and identify the data as simulated in this prototype. An estimate assumes the recent rate continues; it is not a guarantee.

## Calculation rules

- Require at least three distinct observations covering at least five seconds.
- Use a rolling 15-second window, interpolating the sample interval that crosses its boundary.
- Require three consecutive new observations proposing a different level before changing an established arrow count.
- Do not treat duplicate, out-of-order, non-UTC, or invalid observations as new activity.
- Discard history when period bounds change, quota increases, or a sampling gap exceeds 30 seconds.
- Show **Calculating** before sufficient history, **Idle** without recent consumption, and a stale/expired/unavailable state when appropriate. Those states do not show speed arrows.
- Do not generate new observations just because the display timer ticks. A repeated unchanged reading is valid only when the data source has supplied that observation.

Real provider refresh frequency will limit responsiveness. Polling an unchanged cached response more often does not reveal new consumption; the 15-second experience requires sufficiently frequent source readings. Providers are not connected yet.

## Taskbar hints

A confirmed level change displays chevrons for eight seconds, without blinking, resizing the strip, or replacing its quota values. Five-hour hints appear for the relevant provider. Weekly hints appear only when the optional weekly indicator is enabled. In the two-row layout, five-hour chevrons appear above the provider symbol and weekly chevrons below it. In the single-period Lines style, chevrons appear just above the thin meter.

## Testing the prototype

Use **Simulate speed** in the control window to cycle through Mixed, Comfortable, Near reset, Fast, Idle, and Calculating scenarios. It opens the details panel and creates clearly simulated recent histories; it does not consume real quota or read account data.

The demo initially seeds fifteen seconds of past activity. Subsequent synthetic observations arrive every two seconds with unchanged quota, so the simulated burst naturally decays to Idle when no new activity is simulated. Clicking the button seeds another history and previews changed arrow counts immediately. This explicit preview does not bypass stabilization for real incoming observations.

The self-test includes twenty-one assertions for rates, projections, warmup, stale and invalid readings, interpolation, resets, resuming from Idle, and three-observation stabilization. Offscreen artifacts include `details-check.png` and `speed-check.png` for visual review in both themes.
