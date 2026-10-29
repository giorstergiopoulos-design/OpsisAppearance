# Opsis Appearance 1.7.0

## English
- Windows 11 style settings rows (label, description, switch, "Immediate" / "Needs Explorer restart" badge) with the shared Opsis.UI 0.2.1 controls.
- One engine at a time: stand-alone app and Opsis plugin never apply effects together.
- Installer now installs into Program Files (administrator), removes an old per-user install silently and keeps your settings.
- Unified update popup: Install / Later / Skip this version / What's new; SHA-256 verified download, silent install, app reopens.
- Fixed the plugin version shown in Opsis. CI workflow added.
- Verified: 229 unit tests, settings page rendered in dark/light, English/Greek. NOT VERIFIED: the installer on a clean and on an upgraded Windows machine, the update popup against a real newer release, real 150/200 % DPI and RTL. Planned for 1.7.1: per-card Reset, settings search.
- Assets with `.sha256`: Setup.exe, Plugin zip, Stand-alone zip.

## Ελληνικά
- Ρυθμίσεις τύπου Windows 11 (ετικέτα, περιγραφή, διακόπτης, ένδειξη «Άμεσα» / «Χρειάζεται Explorer») με τα κοινά Opsis.UI 0.2.1.
- Ένας κινητήρας τη φορά: αυτόνομο και plugin δεν εφαρμόζουν ποτέ μαζί εφέ.
- Ο installer εγκαθιστά σε Program Files (διαχειριστής), αφαιρεί σιωπηλά παλιά per-user εγκατάσταση και κρατά τις ρυθμίσεις.
- Ενιαίο popup ενημέρωσης: Εγκατάσταση / Αργότερα / Παράλειψη έκδοσης / Τι νέο υπάρχει· λήψη με SHA-256, σιωπηλή εγκατάσταση, η εφαρμογή ανοίγει ξανά.
- Διορθώθηκε η έκδοση του plugin στο Opsis. Προστέθηκε CI.
- Ελέγχθηκαν: 229 unit tests, σελίδα σε dark/light, EN/EL. ΔΕΝ ελέγχθηκαν: ο installer σε καθαρό και σε αναβαθμισμένο υπολογιστή, το popup με πραγματική νεότερη έκδοση, πραγματικό DPI 150/200 % και RTL. Για το 1.7.1: Reset ανά κάρτα, αναζήτηση ρυθμίσεων.
