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

## Controls

1. Keep **Lines (default)** or select **Essential** or **Blocks**.
2. Start with **Embedded (experimental)** and check the status below the preview.
3. Look just before the primary taskbar's system tray. The indicator includes a `DEMO` label.
4. Hover or click the indicator for sample details. The **Demo details** button also opens the panel from the control window.
5. Select **Overlay** to compare the fallback. If verified space is insufficient, the indicator hides and explains why.
6. Uncheck **Show indicator on the primary taskbar** to pause it.
7. Use **Exit prototype** to close the prototype and its indicator.

The prototype runs as the current user, without administrator rights. It does not install itself, register startup tasks, or change taskbar settings. Preferences are stored in `settings.xml` beside the executable. Deleting that file while the prototype is closed restores default style and placement on the next run.

The `out/` directory is ignored by Git. Do not commit local executable files or settings.

## Review checklist

- Does the strip appear immediately before the hidden-icons arrow?
- Is its text readable at your usual display scaling?
- Does hover open details without interrupting your current application?
- Which style balances space and readability best?
- Does Embedded remain visible while you open and switch applications?

See the [integration investigation](TASKBAR-INTEGRATION.md) for evidence and known limitations. This is not a production release.
