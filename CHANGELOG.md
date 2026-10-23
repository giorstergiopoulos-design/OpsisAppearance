# Changelog

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
