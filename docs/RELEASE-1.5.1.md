# Opsis Appearance 1.5.1

## English
- Security audit (SECURITY.md): full paths for `taskkill`/`explorer`, safe app launch from the family window, safer uninstall of a custom plugin folder. Downloads, theme import and dependencies reviewed without findings. Open: plugin integrity in the Opsis host; binaries are not code-signed (verify the SHA-256).
- Real icons of all 7 apps in the family window; installer pages show the app icon.
- README with real dark/light screenshots (English + Greek), social preview.
- Tests: 220 unit tests pass. Not verified: real 150/200 % DPI, RTL, every-control click audit (planned for 1.6.0); the installer was compiled, not run on a test PC.
- Assets: OpsisAppearance-Setup.exe and the portable zip with `.sha256` files.

## Ελληνικά
- Έλεγχος ασφαλείας (SECURITY.md): πλήρεις διαδρομές για `taskkill`/`explorer`, ασφαλές άνοιγμα εφαρμογών από το παράθυρο οικογένειας, ασφαλέστερη απεγκατάσταση. Λήψεις, εισαγωγή θεμάτων και εξαρτήσεις ελέγχθηκαν χωρίς ευρήματα. Ανοιχτά: ακεραιότητα plugin στο Opsis· τα εκτελέσιμα δεν είναι υπογεγραμμένα (έλεγξε το SHA-256).
- Τα πραγματικά εικονίδια των 7 εφαρμογών· εικονίδιο στις σελίδες του installer.
- README με πραγματικά screenshots (dark/light, EN + EL), social preview.
- Δοκιμές: 220 unit tests. Δεν ελέγχθηκαν: πραγματικό DPI 150/200 %, RTL, έλεγχος κάθε κουμπιού (1.6.0)· ο installer μεταγλωττίστηκε αλλά δεν δοκιμάστηκε σε δεύτερο PC.
