# Opsis Appearance 1.4.0

## English
- **Taskbar look** (TranslucentTB-style): clear / blur / acrylic / solid, tint and strength, a different look for a maximized window and for Start/Search, all monitors. No injection, no file changes.
- **Rounded / floating taskbar** (RoundedTB-style) with radius and gaps.
- **Mica for every window**: per-process exclusions and title bar / title text colours.
- **Safety**: system restore point before the first invasive setting, "Create a restore point now", and a verified `BackupVault` for the original of any file before it is ever changed. "Restore my original settings" now also turns the taskbar look off.

**Not included, on purpose**: Windhawk mods and ExplorerBlurMica need DLL injection into `explorer.exe`; TaskbarX icon animations. This release does not claim parity with those programs.

**Tests**: 128 unit tests pass (45 new: vault, restore-point policy, taskbar look and shape engines, exclusions, title colours). The settings page was rendered to an image. On this PC the taskbar was driven for real by a test harness: clear, acrylic with a blue tint and the rounded shape were all visible in screenshots, and after the restore the taskbar looked like before. Not verified: multi-monitor, a vertical/auto-hidden taskbar, the 25H2 Explorer build on other PCs, and creating a restore point (it needs administrator rights, which the test session did not have).

## Ελληνικά
- **Εμφάνιση taskbar** (στο πνεύμα του TranslucentTB): διαφανές / blur / acrylic / συμπαγές με χρώμα και ένταση, διαφορετική κατάσταση για μεγιστοποιημένο παράθυρο και για Start/Αναζήτηση, κάθε οθόνη. Χωρίς injection, χωρίς αλλαγή αρχείων.
- **Στρογγυλό / «επιπλέον» taskbar** (στο πνεύμα του RoundedTB).
- **Mica για όλα τα παράθυρα**: εξαιρέσεις ανά διεργασία και χρώματα γραμμής/κειμένου τίτλου.
- **Ασφάλεια**: σημείο επαναφοράς πριν από την πρώτη παρεμβατική ρύθμιση, κουμπί «Δημιουργία σημείου επαναφοράς τώρα» και `BackupVault` με επαληθευμένο αντίγραφο του αρχικού κάθε αρχείου πριν από οποιαδήποτε επέμβαση.

**Δεν περιλαμβάνονται, εσκεμμένα**: Windhawk mods και ExplorerBlurMica (injection DLL στο `explorer.exe`) και οι κινήσεις εικονιδίων του TaskbarX. Δεν υπάρχει πλήρης ισοδυναμία με αυτά τα προγράμματα.
