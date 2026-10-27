# Changelog

## 1.5.0 — 2026-10-27
- **Safer Windows themes**: before a `.theme` is applied, its visual style (.msstyles) is checked; if it is not signed by Microsoft and no signature patcher is installed, a warning explains what Windows will do (default answer: No). Opsis still never patches system files.
- **Opsis look for the whole page and its dialogs**: pill buttons, rounded check boxes, round radio buttons, drop-downs that open a flat menu, rounded text boxes, − / + number boxes, themed message boxes and prompt, dark scroll bars, and a title bar in the page colours for the stand-alone window. Same palette as the host, repainted on every theme change.
- **Welcome guide** (6 pages, Greek/English) on the first start of each new minor version of the stand-alone app and from the tray menu / the plugin page: the real app icon with a living glow and rotating ring, "Don't show again", closes by shrinking towards the tray / window icon (plain fade when Windows animations are off).
- **"The Opsis family"** window with a card per app (capabilities, installed / update-available state from a read-only probe, Open, Download with confirmation + SHA-256 check that refuses a mismatch, Details), offered once after the guide.
- **New-version balloon** in the tray (stand-alone app): one GET a day to the GitHub releases API with ETag, once per version, switch in the tray menu; "Check now" entry.
- Inside Opsis the plugin only adds the menu/page entries ("Welcome guide…", "The Opsis family…"); it never shows a splash or a balloon by itself.
- Shared core with 81 new unit tests (version comparison, once-per-version, catalogue validation, install detection, SHA-256 verification). 217 tests in total.

## 1.4.0 — 2026-10-23
- **Taskbar look** (TranslucentTB-style): clear / blur / acrylic / solid with tint and strength, with a different state for a maximized window and for Start/Search, on every monitor (`SetWindowCompositionAttribute`, no injection, no file changes, restored when the plugin stops).
- **Rounded / floating taskbar** (RoundedTB-style): radius and gaps through a clipping region (`SetWindowRgn`), re-applied when the taskbar size changes, refused when it would be absurdly small (vertical taskbar).
- **Mica for every window** (Mica For Everyone-style): per-process exclusion list and title bar / title text colours, on top of the existing backdrop, corners and dark title bars.
- **Safety net**: system restore point before the first invasive setting (needs administrator rights, at most once a day, never blocks the change), a "Create a restore point now" button, and `BackupVault` - a verified (SHA-256) backup of the ORIGINAL of any file before it is ever changed, never overwritten, with a checked restore. "Restore my original settings" also turns the taskbar look and shape off.
- 45 new unit tests (128 in total). Not implemented on purpose: Windhawk mods and ExplorerBlurMica (DLL injection into explorer.exe) and TaskbarX animations - see the README.

## 1.3.0 — 2026-10-23
- **Explorer & taskbar behaviour** (Seelen-style settings): show file extensions, show hidden files, seconds in the taskbar clock, window animations, taskbar button grouping (always / when full / never with labels) and a "Restart Explorer" button. All values are part of the original-values backup and are restored by "Restore".
- **Wallpaper slideshow**: change the wallpaper from a folder every N minutes (shuffle or in order, all fit modes), "Next now", and the wallpaper you had before is put back when you turn it off. One cheap timer check per minute, only while it is on.
- 11 new unit tests (83 in total).

## 1.2.0 — 2026-10-22 (first public release)

- **Theme Manager**: full themes (Windows look + window effects + wallpaper) — apply, save the current look, delete, **import/export `.opsistheme`** (safe zip: only `theme.json` and a plain wallpaper, 40 MB cap, values sanitised), and a list of the Windows `.theme` files. Replaces the simple "Looks" buttons.
- **Stand-alone app** (tray icon + window, follows the Windows light/dark mode and language) and an **installer** that installs it either as a stand-alone app or as an Opsis plugin; `/MODE=plugin|standalone` for silent installs.

## 1.1.0

- **Window effects** (documented DWM APIs, no injection): coloured active-window border, corner style (round/small/square), dark title bars, Mica/Acrylic/Tabbed backdrop, taskbar auto-hide. Event driven (`SetWinEventHook`), fully restored on stop.

## 1.0.0

- Automatic Light/Dark at sunrise/sunset (NOAA algorithm) or fixed times, accent colour (presets, custom, from the wallpaper), taskbar alignment/Search/Task View/Widgets, transparency effects, and a full restore of the original values (HKCU only).
