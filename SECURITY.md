# Security model — Opsis Appearance

## Trust model
- **Runs as the current user**, never elevated (installer: `PrivilegesRequired=lowest`). Only an optional system **restore point** needs administrator rights and is requested through the documented `SRSetRestorePointW`; without rights it is skipped and the plugin says so.
- **Plugin inside Opsis**: the DLL lives in `%AppData%\Opsis\plugins\Opsis Appearance`, a folder that any process of the same user can write. Opsis loads whatever is there, so the plugin directory has the same trust level as the user account: malware running as the user could replace it (as it could replace any per-user program). The installer writes only `OpsisAppearancePlugin.dll`; loading and verification are done by the Opsis host (a hash/signature check there is the real fix — reported to the coordinator).
- **No injection, no file changes to Windows**: only documented per-user APIs/registry values; the first change saves a copy of the original values (BackupVault, SHA-256, never overwritten).

## What was reviewed (1.5.1)
| Area | Result |
|---|---|
| Installer | per-user, no PATH writes, Run key only if the user ticks "start with Windows" (removed on uninstall); uninstall of a custom `/PLUGINROOT` folder now only if the folder is called "Opsis Appearance" **and** contains our DLL (the registry value is user-writable) — **fixed** |
| Process launching | `taskkill` was started by bare name (search-path planting) → full `System32` path; `explorer.exe /select` → full Windows path; app launch from the family window only for an existing local `.exe` with a full path (the path comes from user-writable registry) — **fixed**, tests added |
| Downloads/updates | HTTPS only, host allow-list (github.com / api.github.com / raw.githubusercontent.com), size limits, redirects ≤5, SHA-256 must match or the file is deleted, never auto-run — reviewed, covered by 80+ unit tests |
| `.opsistheme` zip | only `theme.json` and one wallpaper entry, no paths, size limits on real bytes (zip-slip impossible) — reviewed, tests exist |
| `.theme` (Windows) | pre-flight check of the referenced visual style; unsigned style → explicit warning — reviewed |
| Secrets | none stored; no API keys; no personal data in logs (the app writes none) |
| Dependencies | `dotnet list package --vulnerable --include-transitive`: none |

## Still open / by design
- Plugin DLL integrity inside Opsis (host side, see above).
- The installer and executables are **not code-signed** (no certificate): Windows SmartScreen will warn; verify the published SHA-256.
- Windows `.theme` files are executed by Windows itself (`ShellExecute`) after the user confirms.
