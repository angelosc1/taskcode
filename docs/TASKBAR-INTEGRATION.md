# Taskbar integration investigation

## Current decision

The original C# / Windows Forms probe uses the .NET Framework compiler already available on the development machine. It tests feasibility without downloading a UI framework. The final application stack remains open.

Embedded mode hosts a child window under the primary taskbar's `Shell_TrayWnd`. Overlay mode uses a separate window in the same measured location. The probe never resizes the Windows task list or system tray and never injects a DLL into Explorer.

Taskbar class names are implementation details. A successful test on one Windows build is not a compatibility guarantee. Microsoft documents window parenting and its DPI considerations, but that does not establish a supported taskbar extension contract.

## Options considered

| Approach | Assessment |
| --- | --- |
| Notification-area icon | Standard tray presence, but cannot provide the requested strip before the tray |
| Application desktop toolbar (appbar) | Reserves desktop-edge space as a separate toolbar; does not match this placement |
| Hosted child window | Preferred feasibility experiment; depends on Explorer internals |
| Independent overlay | Explicit fallback; does not become a taskbar child |
| Third-party hosting library | Candidate for later evaluation, after the independent experiment |

## Probe behavior

- Finds the primary taskbar and its `TrayNotifyWnd` region.
- Measures visible button and tab-item bounds through UI Automation on a dedicated MTA background thread.
- Places the indicator before the tray only if the measured gap fits.
- Hides it when measurement is missing, stale, or too narrow.
- Rechecks taskbar and tray geometry before positioning.
- Declares per-monitor DPI awareness in its manifest.
- Uses a per-pixel alpha surface for both placement modes, without painting a background rectangle.
- Reads `SystemUsesLightTheme` for the taskbar palette and checks for changes once per second, without modifying Windows settings.
- Uses pixel-sized indicator fonts with one DPI transform, avoiding double scaling.
- Supports three styles, immediate preview, and local preferences.
- Uses live provider connections in normal startup and explicitly simulated data in offline previews. See [account connections](ACCOUNTS.md) for authentication and privacy.

The measurement is a snapshot, not a reserved slot. A newly added taskbar item could briefly overlap before the next measurement. A production host needs stronger layout coordination or faster collision handling.

## Validation record

Initial desktop: Windows build 26300; primary taskbar at 120 DPI. The legacy registry product-name field says Windows 10, so that string alone is not treated as reliable OS identification.

| Check | Result |
| --- | --- |
| Compile with warnings treated as errors | Passed |
| Seven placement assertions: fit, alignment, crowding, missing tray, vertical orientation, negative coordinates, and scaling | Passed |
| Transparent renderer, visible content, and palette contrast | Passed for every style in both themes; offscreen comparison image reviewed |
| Live alpha surface | Windows accepted the transparent Embedded surface at 120 DPI; current light-taskbar palette detected |
| Embedded mode | Host verified that its parent handle matches the taskbar; UI reports active |
| Overlay mode | Placement path activated; UI reports active |
| Switch between all three styles | All three previews visually reviewed; no clipped provider values observed |
| Open demo details | English provider names, remaining percentages, reset times, and sample-data disclosure visually reviewed |
| Keyboard navigation | Style selector reached with Shift+Tab and reset with Home |
| Pause indicator | Paused state observed |
| Restore settings after restart | Blocks and Overlay restored successfully |
| Restore the approved defaults | Lines and Embedded selected again; saved preferences verified |
| Hover on the actual taskbar indicator | Implemented; not independently verified through desktop automation |
| Placement before the hidden-icons arrow | User confirmed that the indicator appears in the expected location on the test desktop |
| Explorer restart | Recovery code exists; not exercised |
| Auto-hide, fullscreen, multiple monitors, display/DPI changes | Not validated |

The desktop inspection tool captures the prototype control window and its related panel, but does not expose the full taskbar as a selectable window. Placement was confirmed by the user on the test desktop. Real-indicator hover behavior remains pending confirmation; the control window's active status is not used as a substitute for that check.

## Known limits

Right-click is handled by Taskcode and opens its own settings/exit menu, including when the indicator is embedded. The native `WM_CONTEXTMENU` message is consumed instead of being forwarded to Explorer. Mouse activation is suppressed, and the surface is refreshed after dismissing the menu. Hover details are paused while that menu is open.

Windows passes clicks through alpha-zero pixels in layered windows. The live input surface therefore uses a minimum alpha of 1/255 in otherwise empty pixels, tinted to the taskbar palette. This is visually transparent, without an opaque panel, while preserving the whole indicator's mouse area. Offscreen artwork retains alpha-zero backgrounds. Regression checks cover the input surface across all styles/themes and context-menu routing in Embedded and Overlay using an isolated host window. See [Microsoft's layered-window hit-testing documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows).

- Primary horizontal taskbar only.
- Indicator and details adapt to light/dark taskbar mode; the settings window remains dark.
- The indicator is transparent rather than a separate acrylic surface. Exact blending on the actual taskbar still needs visual confirmation after this change.
- Does not reserve space or move Windows controls; may hide on a crowded taskbar.
- Overlay does not yet suppress itself for fullscreen applications.
- Taskbar class names and accessibility structure can change with Windows updates.
- Settings open from a tray icon, without a normal taskbar app button. Windows startup is opt-in and launches hidden; no installer or updater is provided yet.
- Functional controls and hover/click details remain experimental rather than a polished release.

## Next checks

Confirm hover behavior on the actual taskbar, then test Explorer recovery, auto-hide, fullscreen, scaling, and monitor changes. Choose the production hosting approach after those checks. Provider integration follows placement validation.

## Primary sources

- [Microsoft: application desktop toolbars](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars)
- [Microsoft: SetParent and DPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)
- [Microsoft: SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
- [Microsoft: UI Automation threading](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading)
- [Microsoft: process DPI awareness](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process)
- [Microsoft: per-pixel layered windows](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow)
- [Taskband](https://github.com/mnismi/taskband)
- [Deskband11Lib](https://github.com/airtaxi/Deskband11Lib)

No source code from these projects has been copied into this probe.
