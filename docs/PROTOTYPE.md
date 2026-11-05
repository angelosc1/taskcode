# Running the positioning prototype

The probe tests placement and display styles with sample data. It does not connect to Claude or Codex.

## Build and run

Requirements: Windows x64 with the .NET Framework 4.x x64 compiler. Only framework assemblies are used; no NuGet restore is required.

From the repository root in PowerShell or Command Prompt:

```powershell
.\scripts\build-probe.cmd --test
.\out\probe\Taskcode.Probe.exe
```

Compiler warnings are treated as errors. Seven placement assertions run with `--test`; results appear in `out/probe/self-test.txt`.

The same command also checks transparent background pixels, rendered content, and palette contrast for every style in both light and dark mode. It writes `out/probe/appearance-check.png`, an offscreen renderer comparison, not a screenshot of the Windows taskbar.

The details panel includes separate remaining-quota bars for each provider's five-hour and weekly windows. Sample values are Claude 68% / 81% and Codex 42% / 63%. The build also writes `out/probe/details-check.png` for an offscreen layout review of the panel in both themes.

Each detail bar has a downward arrow tracking elapsed time. The arrow moves from the start at reset to the end at renewal, independently of quota consumption. Muted space on the left means used quota; provider-colored space on the right means remaining quota. A consumption boundary beyond the arrow means usage is ahead of time. Hover over a bar for the numeric pace comparison.

Elapsed time and reset countdowns refresh once per second while the details panel is visible. Demo period dates are simulated at application startup, including the weekly periods. Real provider periods are not connected yet. The self-test includes ten assertions for time boundaries, unavailable timing, and pace comparisons.

The details also show recent-consumption chevrons and approximate time to exhaustion, using a 15-second observation window. Use **Simulate speed** to preview changes. These are simulated histories, not real account readings. See [recent consumption speed](USAGE-SPEED.md) for thresholds, stabilization, data-quality handling, and the temporary taskbar hints.

## Controls

1. Keep **Lines (default)** or select **Essential** or **Blocks**.
2. Start with **Embedded (experimental)** and check the status below the preview.
3. Look just before the primary taskbar's system tray for the provider readings. Sample data is identified in the control window and details panel.
4. Choose **Open details > Hover (default)** to open the panel on hover or click, or **Click only** to prevent hover from opening it. The preference is saved. Click-opened panels stay open until dismissed; the **Demo details** button remains available in either mode.
5. Select **Overlay** to compare the fallback. If verified space is insufficient, the indicator hides and explains why.
6. Uncheck **Show indicator on the primary taskbar** to pause it.
7. Use **Exit prototype** to close the prototype and its indicator.

**Show weekly quota in the indicator** is an optional setting, unchecked by default. Enabling it shows two compact rows per provider, labeled `5h` and `7d`, with provider symbols and remaining percentages. Lines adds thin meters; Blocks uses segmented meters; Essential shows numbers. Disabling it restores the original five-hour-only layout. The preference is saved, the preview updates immediately, and the detail panel continues to include weekly quota in either mode. The optional layout uses a little more horizontal space while retaining the same height.

The actual taskbar indicator has a per-pixel transparent background. Its colors follow the Windows taskbar's light/dark setting, including theme changes while running. The preview uses a solid sample background to make each palette visible; that sample background is not applied to the taskbar indicator. The control window itself still uses the prototype's dark appearance.

Placement changes how the indicator is hosted, not its appearance: **Embedded** attaches it as a child of the taskbar window; **Overlay** keeps it as an independent window over the same measured gap. Both modes use the same style, transparent surface, and theme palette.

The indicator uses a 117% content scale, in addition to one monitor-DPI transform. Its former demo-label gutter has been removed. The control window and details panel still disclose that the displayed values are sample data.

The prototype runs as the current user, without administrator rights. It does not install itself, register startup tasks, or change taskbar settings. Preferences are stored in `settings.xml` beside the executable. Deleting that file while the prototype is closed restores default style and placement on the next run.

The `out/` directory is ignored by Git. Do not commit local executable files or settings.

## Review checklist

- Does the strip appear immediately before the hidden-icons arrow?
- Is its text readable at your usual display scaling?
- Does hover open details without interrupting your current application?
- Which style balances space and readability best?
- Does Embedded remain visible while you open and switch applications?

See the [integration investigation](TASKBAR-INTEGRATION.md) for evidence and known limitations. This is not a production release.
