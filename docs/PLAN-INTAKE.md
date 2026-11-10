# Opsis Appearance — plan intake (Opsis 2.6.0 strategy, 2026-11-10)

Source: `C:\w\OPSIS_UNIFIED_MASTER_PLAN.md` (Strategy decision, 0B, 0D, section 5 Appearance) and TASKS-MASTER section 26. This file is a **priority plan only — no code is changed by it**.

## Strategy that applies to Appearance
- Appearance stays **compatible with Opsis 2.4.6 / 2.4.6.1 (plugin contract 0.8.0.0)** while Opsis 2.6.0 is in beta and pre-release. No contract-breaking change until phase P.
- The **2.6.0 upgrade of the plugin is phase P** (after the 2.6.0 pre-release checks): re-target the new contract, re-test inside Opsis 2.6.0, then release.
- Until then only fixes for our own bugs or serious findings; no new features beyond the plan items below.

## Priorities (in this order)
| # | Item (plan ref.) | Why first | Risk / note |
|---|---|---|---|
| 1 | **Performance: slideshow folder scanned repeatedly** (5.2) — cache the image list, refresh only when the folder/slideshow setting changes | `RefreshAll()` and the slideshow timer enumerate, size and sort the folder every time | low; pure caching |
| 2 | **Performance: taskbar monitoring every 800 ms** (5.2) — replace polling by event notifications (`SetWinEventHook`), skip unchanged state | constant UI-thread work while the plugin runs inside Opsis | medium; needs a real-Windows test (NOT VERIFIABLE on CI) |
| 3 | **`WallpaperAccent()` loads the whole image** (5.2) — check size, decode at reduced size | big wallpapers allocate hundreds of MB for a 48x48 sample | low |
| 4 | **`ReloadThemes()` synchronous, also on language change** (5.2) — cache, reload only on a change | UI hiccup when opening the page / switching language | low |
| 5 | **Settings search rebuilds control text on every key** (5.2) — cache the searchable text | tiny | trivial |
| 6 | **Integration: "Personalization Plus" inside Opsis' Personalization** (5.1, 2.1.3) instead of a separate page; check duplicates with the Opsis theme packs | depends on the 2.6.0 host UI → **phase P** | needs the 2.6.0 contract |
| 7 | **Updates only from the Opsis update button when hosted** (5.1, 2.1.1) | our plugin-mode update popup (1.7.1+) must be switched off under 2.6.0; the stand-alone app keeps its own | **phase P**; until then 2.4.6 has no such button, so the current behaviour stays |
| 8 | Restart-needed settings: pop-up "Restart now / Later" (0B.2) | review below | see below |

Items 1–5 can be done on 2.4.6 now (one patch release, with tests where the logic is testable: cache invalidation, accent sampling size); items 6–7 wait for phase P.

## 0B.2 checks done now
- **Keyboard shortcuts vs Windows defaults — no conflict found.** Source search (`src`, all projects): no `RegisterHotKey`, no global hotkeys, no menu accelerators / `ShortcutKeys`, no `Win+` / `Alt+` / `Ctrl+` combinations. The only key handling is local to dialogs: Enter/Escape (inputs, welcome guide, family window) and the arrow keys of the welcome guide and the shared Opsis.UI controls (Space, arrows, Home/End). Nothing to change; the Opsis.UI controls were reviewed in their own repo.
- **Settings that need a restart:** every Explorer-related setting shows the badge "Needs Explorer restart" and the page has a "Restart Explorer" button with a confirmation; no Appearance setting needs a restart of Appearance itself. The shared restart pop-up of `opsisui` (when it exists) should be adopted for the Explorer case in phase P — no change now.

## Not in scope now
No code, no release, no change to the contract reference. Open items from earlier releases stay as listed in the CHANGELOG (real DPI/RTL on the page, installer on a clean PC, update flow against a real release).
