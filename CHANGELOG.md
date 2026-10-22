# Changelog

## 1.2.0 — 2026-10-22 (first public release)

- **Theme Manager**: full themes (Windows look + window effects + wallpaper) — apply, save the current look, delete, **import/export `.opsistheme`** (safe zip: only `theme.json` and a plain wallpaper, 40 MB cap, values sanitised), and a list of the Windows `.theme` files. Replaces the simple "Looks" buttons.
- **Stand-alone app** (tray icon + window, follows the Windows light/dark mode and language) and an **installer** that installs it either as a stand-alone app or as an Opsis plugin; `/MODE=plugin|standalone` for silent installs.

## 1.1.0

- **Window effects** (documented DWM APIs, no injection): coloured active-window border, corner style (round/small/square), dark title bars, Mica/Acrylic/Tabbed backdrop, taskbar auto-hide. Event driven (`SetWinEventHook`), fully restored on stop.

## 1.0.0

- Automatic Light/Dark at sunrise/sunset (NOAA algorithm) or fixed times, accent colour (presets, custom, from the wallpaper), taskbar alignment/Search/Task View/Widgets, transparency effects, and a full restore of the original values (HKCU only).
