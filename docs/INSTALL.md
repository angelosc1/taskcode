# Install Taskcode

## Requirements

- Windows 11 x64 and .NET Framework 4.8 or later.
- A Claude and/or ChatGPT subscription with quota exposed by the provider.
- The official Claude Code and/or Codex CLI for account authentication. Account windows offer installation when missing. The npm-based Codex installer requires Node.js/npm.

Current releases are unsigned. Windows may display an unknown-publisher notice. Download only from [angelosc1/taskcode Releases](https://github.com/angelosc1/taskcode/releases).

## Installer - recommended

1. Open the [latest release](https://github.com/angelosc1/taskcode/releases/latest).
2. Download `Taskcode-Setup-<version>-win-x64.exe` from **Assets**. Do not download **Source code** to install the app.
3. Run the setup wizard. If Windows shows **Windows protected your PC**, follow the [SmartScreen steps below](#windows-smartscreen-warning). It installs to `%LOCALAPPDATA%\Programs\Taskcode` for your user; no administrator account is needed. A desktop shortcut is optional.
4. Open Taskcode from the Start menu if its settings did not open after installation.
5. In **Accounts**, choose **Connect** for each provider. Use **Sign in** to select an account, or **Use existing CLI account**. Complete the official sign-in flow. [Connection guide](ACCOUNTS.md).
6. Open **Appearance** to choose **Essential**, **Lines** or **Blocks**, hover/click behavior, weekly quota and optional usage-based bar colors.
7. In **General**, optionally enable **Start with Windows**. It is disabled by default.

The indicator appears immediately to the left of the hidden-icons arrow. Windows may initially put Taskcode's tray icon under that arrow. Left-click the tray icon for settings; right-click it or the quota capsule for the menu.

**Close** and the header close button hide settings while the indicator keeps running. **Exit**, available only in the right-click menu, ends the app.

## Windows SmartScreen warning

The current Taskcode installer is not code-signed. Microsoft Defender SmartScreen may display **Windows protected your PC** and say that it prevented an unrecognized app from starting. After expanding the warning, **Publisher: Unknown publisher** is expected for this unsigned release. This warning does not establish whether a file is safe; check its source before continuing.

If you downloaded the installer from the [official Taskcode Releases page](https://github.com/angelosc1/taskcode/releases/latest) and trust it:

1. On **Windows protected your PC**, click the **More info** link below the message.
2. Check that **App** matches the installer you downloaded: `Taskcode-Setup-<version>-win-x64.exe`.
3. Click **Run anyway**. This button appears after clicking **More info**.
4. Continue through the Taskcode setup wizard.

You do not need to turn off Microsoft Defender or SmartScreen. If **Run anyway** is missing on a managed work or school computer, contact your administrator; device policy may prevent running unrecognized apps.

For an optional download-integrity check, download **SHA256SUMS** from the same release and run this in PowerShell from your Downloads folder, replacing `<version>` with the downloaded version:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath '.\Taskcode-Setup-<version>-win-x64.exe'
```

Compare the hash with the installer's entry in **SHA256SUMS**. A matching hash verifies that the bytes match that release; it does not replace publisher signing or a safety assessment.

See Microsoft's [SmartScreen reputation guidance](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation) for background on these warnings.

## Updates

**General** shows the installed version and **Check for updates**. Automatic daily checks are on by default; **Check automatically** turns them off.

When a newer public stable release exists, the button becomes **Install v<version>**. Click it to download and install. Taskcode checks the official release asset's size and SHA-256 digest, then closes, updates and restarts in the tray. No update is installed silently in the background without your click. A failed check/download leaves the current version installed and can be retried.

You can also download and run the newer installer manually. Existing settings, account profiles and startup preference are preserved. Installation never enables Windows startup unless it was already enabled.

The update check uses the public GitHub Releases API and requires internet access. It does not send account details or quota readings. Only stable releases are offered.

## Portable ZIP

Download `Taskcode-<version>-win-x64.zip`, extract it to a permanent folder, and run `Taskcode.exe`. Keep `Taskcode.exe.config` and `Taskcode.ClaudeBridge.exe` with it. The config file is required for modern .NET networking.

Portable copies use the same per-user settings and accounts. Their update button opens Releases so you can exit Taskcode, extract the new ZIP and replace the application files manually. To use installer-based updates, install the setup edition.

## Moving from the development build

Exit the old copy first. A build with the new data layout reads legacy `settings.xml` beside its executable when no shared settings exist, then saves preferences in the per-user directory. Existing separate account profiles remain in `%LOCALAPPDATA%\Taskcode\accounts`; tokens do not need to be copied into the installation folder. If no preferences were migrated, reconnect through Accounts.

Only one instance runs per Windows session. Running the executable again opens the existing settings panel.

## Files and removal

- Application: `%LOCALAPPDATA%\Programs\Taskcode`.
- Preferences: `%LOCALAPPDATA%\Taskcode\settings.xml`.
- Separate official-client sign-ins: `%LOCALAPPDATA%\Taskcode\accounts`.
- Managed Claude hook helper: `%LOCALAPPDATA%\Taskcode\Taskcode.ClaudeBridge.exe`, refreshed with installer updates.
- Safe lifecycle diagnostics: `%LOCALAPPDATA%\Taskcode\lifecycle.log`.
- Verified update downloads: `%LOCALAPPDATA%\Taskcode\updates`.

Uninstall from **Windows Settings > Apps > Installed apps > Taskcode > Uninstall**. The uninstaller exits Taskcode, removes its startup registration and restores Taskcode's Claude Code integration before removing application files. If restoration fails, it stops and asks you to disconnect Claude first. Other Claude hooks are preserved.

Account profiles and preferences are retained for reinstalling. Uninstalling does not sign you out of your normal Claude/Codex clients. You can remove the Taskcode data directory separately if you deliberately want to discard its saved preferences and isolated sign-ins.

## Troubleshooting

- **Indicator missing:** check **General > Show indicator**. Try **Overlay** if Embedded fails. Taskcode does not force itself over occupied taskbar controls.
- **Tray icon missing:** open Windows' hidden-icons arrow, or run Taskcode again from Start.
- **`--` quota:** check Accounts, source identity and the connection message. Claude normally refreshes at five-minute intervals; Codex at ten-second intervals.
- **Idle with quota visible:** forecasts follow local execution signals. Website/Desktop activity is not detected by the local activity monitor.
- **Update unavailable/offline:** retry **Check for updates** later or use Releases manually. The existing installation is unchanged.
- **Something else:** [open an issue](https://github.com/angelosc1/taskcode/issues) with the Taskcode version, Windows build, display scaling and reproduction steps. Do not attach credential files or raw session transcripts.
