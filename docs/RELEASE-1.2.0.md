# Opsis Appearance 1.2.0

First public release. Change the look of Windows 11 itself — as an **Opsis plugin** or a **stand-alone app**. No injection, no drivers, no administrator rights.

## English
- **Automatic Light/Dark** at sunrise/sunset (computed from latitude/longitude, Athens by default) or at fixed times.
- **Accent colour**: 12 presets, custom, or taken from your wallpaper.
- **Taskbar**: alignment, Search, Task View, Widgets, transparency, accent on Start/taskbar/title bars.
- **Window effects** (while the app runs, fully undone when it stops): coloured active-window border, corner style, dark title bars, Mica/Acrylic/Tabbed backdrop, taskbar auto-hide.
- **Theme Manager**: full themes (look + effects + wallpaper) — apply, save the current look, delete, import/export `.opsistheme`, plus the Windows `.theme` list.
- **Restore my original settings**: a copy of the original values is saved before the first change.

**Downloads**: `OpsisAppearance-Setup.exe` (choose plugin or stand-alone; no admin), `OpsisAppearance-Standalone-1.2.0.zip`, `OpsisAppearance-Plugin-1.2.0.zip`. The Opsis installer (2.1.1+) can also download and install it as a plugin.

**Testing**: 72 unit tests (solar maths, schedule, registry mapping, snapshot/restore, colour palette, window-effects engine, Theme Manager including hostile `.opsistheme` packages). The border and corner effects were also verified against the real DWM on a live window (pixel check: the border turned red, square corners appeared, and everything was restored). The installer was run end to end in both modes (silent install and uninstall; files and registry are removed).

**Known limits** (not verified): registry writes of the appearance settings, wallpaper change, taskbar auto-hide and Mica/Acrylic backdrop were verified only against fakes/by unit tests, not on a real machine — try "Restore my original settings" first; window effects apply to normal app windows only (not elevated windows or the Windows shell); the Theme Manager does not change cursors or sounds (use the Windows `.theme` list for that).

## Ελληνικά
- **Αυτόματο Light/Dark** με ανατολή/δύση (υπολογίζεται από γεωγραφικό πλάτος/μήκος, προεπιλογή Αθήνα) ή σταθερές ώρες.
- **Χρώμα accent**: 12 έτοιμα, δικό σου, ή από το wallpaper.
- **Taskbar**: στοίχιση, Αναζήτηση, Task View, Widgets, διαφάνειες, accent σε Start/taskbar/τίτλους.
- **Εφέ παραθύρων** (όσο τρέχει η εφαρμογή, επαναφέρονται πλήρως όταν σταματά): χρωματιστό border ενεργού παραθύρου, γωνίες, σκούρες γραμμές τίτλου, backdrop Mica/Acrylic/Tabbed, αυτόματη απόκρυψη taskbar.
- **Theme Manager**: πλήρη θέματα (εμφάνιση + εφέ + wallpaper) — εφαρμογή, αποθήκευση του τρέχοντος, διαγραφή, εισαγωγή/εξαγωγή `.opsistheme` και λίστα των `.theme` των Windows.
- **Επαναφορά στις αρχικές μου ρυθμίσεις**: αποθηκεύεται αντίγραφο των αρχικών τιμών πριν την πρώτη αλλαγή.

**Δοκιμές**: 72 unit tests· τα εφέ border/γωνιών επαληθεύτηκαν και στο πραγματικό DWM σε ζωντανό παράθυρο· ο installer δοκιμάστηκε end to end και στους δύο τρόπους (σιωπηλή εγκατάσταση/απεγκατάσταση).
**Όρια** (δεν επαληθεύτηκαν): εγγραφές registry, αλλαγή wallpaper, auto-hide taskbar και backdrop Mica/Acrylic δοκιμάστηκαν μόνο με unit tests/ψεύτικα αντικείμενα — δοκίμασε πρώτα το «Επαναφορά».
