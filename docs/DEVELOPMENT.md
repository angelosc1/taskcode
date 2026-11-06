# Development

Requires Windows x64, .NET Framework 4.8 and its built-in x64 C# compiler. There are no NuGet dependencies.

```powershell
.\scripts\build-probe.cmd --test
.\out\probe\Taskcode.Probe.exe
```

The development executable retains explicit `--demo`, `--self-test`, `--tray-test` and account diagnostic commands. The official release is compiled with `TASKCODE_RELEASE`: diagnostic routes and test classes are excluded, and normal startup always reads live account state. Public images are offscreen renders with sample values; they never capture the desktop or real account identity.

`VERSION` is the release version source. Both builds embed version metadata. Preferences live in `%LOCALAPPDATA%\Taskcode`, so developer tests must preserve user options and startup registration.

The build checks placement/routing, account parsing, freshness, speed estimates, usage colors, activity/Ripple, native mouse routing, non-activating Details, theme/layout bounds, switch accessibility, tray lifecycle and preferences. Reports and render artifacts are written under `out/probe` and excluded from Git. GUI fixtures stay outside the visible desktop.

[Release packaging](RELEASING.md) ? [Architecture and historical taskbar investigation](TASKBAR-INTEGRATION.md).
