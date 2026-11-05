# Running the positioning prototype

The probe tests taskbar placement and display styles and can monitor account quota through Codex CLI and Claude Code. See [account connections](ACCOUNTS.md) for sign-in, privacy, dependencies, and removal.

## Build and run

Requirements: Windows x64 with the .NET Framework 4.x x64 compiler. Only framework assemblies are used; no NuGet restore is required.

Exit any running copy through **Tray > Exit** before building. From the repository root in PowerShell or Command Prompt:

```powershell
.\scripts\build-probe.cmd --test
.\out\probe\Taskcode.Probe.exe
```

Compiler warnings are treated as errors. Seven placement assertions run with `--test`; results appear in `out/probe/self-test.txt`.

The same command also checks transparent background pixels, rendered content, and palette contrast for every style in both light and dark mode. It writes `out/probe/appearance-check.png`, an offscreen renderer comparison, not a screenshot of the Windows taskbar.

The details panel includes separate remaining-quota bars for each provider's five-hour and weekly windows. Normal startup reads account data. `--demo` runs an explicit offline preview with Claude 68% / 81% and Codex 42% / 63%. The build writes `out/probe/details-check.png` using sample data for an offscreen layout review in both themes.

Each detail bar has a downward arrow tracking elapsed time. The arrow moves from the start at reset to the end at renewal, independently of quota consumption. Provider-colored space on the left means remaining quota; muted space on the right means used quota, matching the taskbar. Hover over a bar for the numeric budget-pace comparison; the arrow tracks elapsed time while the fill tracks remaining quota.

Elapsed time and reset countdowns refresh once per second while the details panel is visible. Live periods use source reset timestamps. Demo dates are simulated only in offline preview mode. The self-test includes ten assertions for time boundaries, unavailable timing, and pace comparisons.

The details also show animated, filled consumption-speed arrows and approximate time to exhaustion, using a 15-second observation window. The closely spaced arrowheads brighten and fade in sequence from left to right. Live estimates require enough fresh source observations. **Simulate speed** is available only in `--demo` mode. See [recent consumption speed](USAGE-SPEED.md) for thresholds and data-quality handling.

## Controls

1. Keep **Lines (default)** or select **Essential** or **Blocks**.
2. Start with **Embedded (experimental)** and check the status below the preview.
3. Look just before the primary taskbar's system tray for provider readings. Use the account connection buttons to complete setup. `--` means unavailable or stale; the details show source and age. Offline demo mode is identified explicitly.
4. Choose **Open details > Hover (default)** to open only the hovered provider's panel, or **Click only** to open it only when that provider is clicked. Moving between Claude and Codex switches panels after a short hover delay; only one panel is shown at a time. The preference is saved. Click-opened panels stay open until dismissed. **Details** offers a menu for choosing Claude or Codex, and clicking a provider in the preview opens its panel as well.
5. Select **Overlay** to compare the fallback. If verified space is insufficient, the indicator hides and explains why.
6. Uncheck **Show indicator on the primary taskbar** to pause it.
7. Use **Exit Taskcode** or the tray menu's **Exit** to close the app and its indicator.

**Show weekly quota in the indicator** is an optional setting, unchecked by default. Enabling it shows two compact rows per provider, labeled `5h` and `7d`, with provider symbols and remaining percentages. Lines adds thin meters; Blocks uses segmented meters; Essential shows numbers. Disabling it restores the original five-hour-only layout. The preference is saved, the preview updates immediately, and the detail panel continues to include weekly quota in either mode. The optional layout uses a little more horizontal space while retaining the same height.

The actual taskbar indicator has a per-pixel transparent background. Its colors follow the Windows taskbar's light/dark setting, including theme changes while running. The preview uses a solid sample background to make each palette visible; that sample background is not applied to the taskbar indicator. The control window itself still uses the prototype's dark appearance.

Placement changes how the indicator is hosted, not its appearance: **Embedded** attaches it as a child of the taskbar window; **Overlay** keeps it as an independent window over the same measured gap. Both modes use the same style, transparent surface, and theme palette.

The indicator uses a 117% content scale, in addition to one monitor-DPI transform. Its former demo-label gutter has been removed. Account data and offline demo data are identified in settings and details.

## Tray and Windows startup

Taskcode has a tray icon instead of a normal taskbar application button. Left-click the tray icon to open settings, or right-click for **Open settings** and **Exit**. Windows may place a new tray icon under the hidden-icons arrow. Closing or minimizing settings hides the window while the quota indicator keeps running. Running the executable again reopens the existing instance's settings rather than creating another instance.

Right-clicking anywhere inside the taskbar indicator opens Taskcode's own settings/exit menu. It does not open Explorer's taskbar menu. Hover details pause while the menu is visible, and dismissing it refreshes the indicator without hiding it.

**Start with Windows** is unchecked by default. Selecting it registers only Taskcode for the current user's sign-in, launching with `--tray` so settings stay hidden. Clearing it removes Taskcode's startup entry. Opening the app, running tests, and changing unrelated preferences do not enable startup. Windows may delay startup applications or override their execution through its own startup controls.

The prototype runs as the current user, without administrator rights, and does not change taskbar settings. Connecting Claude installs a local status-line bridge; disconnect it before removing the app. Style, connection toggles, and interaction preferences are stored in `settings.xml` beside the executable. Deleting that file restores those preferences, but does not remove a Windows startup registration or Claude integration. Disable **Start with Windows** before moving or deleting the portable executable.

Command-line switches: `--tray` starts without showing settings; `--exit` requests a clean exit from a running instance. The build's `--test` sequence also checks hidden startup, settings reopen, close-to-tray, the missing taskbar button, and unchanged startup registration. Its offscreen settings preview is saved as `out/probe/settings-check.png`.

The `out/` directory is ignored by Git. Do not commit local executable files or settings.

## Review checklist

- Does the strip appear immediately before the hidden-icons arrow?
- Is its text readable at your usual display scaling?
- Does hover open details without interrupting your current application?
- Which style balances space and readability best?
- Does Embedded remain visible while you open and switch applications?

See the [integration investigation](TASKBAR-INTEGRATION.md) for evidence and known limitations. This is not a production release.
