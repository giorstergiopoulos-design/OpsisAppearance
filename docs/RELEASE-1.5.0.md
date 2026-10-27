# Opsis Appearance 1.5.0

## English
- **Welcome guide** (6 pages) on the first start of each new minor version of the stand-alone app, and on demand from the tray menu and the plugin page. Animated real app icon; closes by shrinking towards the tray / window icon.
- **"The Opsis family"**: a card per app with capabilities, installed / update-available state, Open, Download (confirmation + SHA-256 check, refuses a mismatch, never runs anything) and Details.
- **New-version balloon** in the tray of the stand-alone app: one request a day to GitHub, once per version, with a switch in the tray menu. No telemetry.
- Inside Opsis the plugin adds only menu/page entries; no automatic splash or balloon.
- Also in this release (committed to the repository after 1.4.0 and not released before): the pre-flight check of Windows `.theme` files (unsigned visual styles) and the Opsis look for every control and dialog of the settings page.

**Tests**: 217 unit tests pass (81 new for the shared core). The welcome guide (page 1), the family window and the shrinking animation were captured in screenshots of the real app and checked. **Not verified**: the tray balloon itself (it did not appear in the screenshots and depends on Windows notification settings), a real download of another app's installer, a real newer release arriving, the plugin entries inside a running Opsis, other display scalings. The installer was compiled but not run on a test PC.

## Ελληνικά
- **Καλωσόρισμα με οδηγό** (6 σελίδες) στην πρώτη εκκίνηση κάθε νέας minor έκδοσης της αυτόνομης εφαρμογής και κατά παραγγελία από το μενού του tray και τη σελίδα του plugin. Κινούμενο πραγματικό εικονίδιο· κλείνει μικραίνοντας προς το tray / το εικονίδιο του παραθύρου.
- **«Οι εφαρμογές της οικογένειας Opsis»**: κάρτα ανά εφαρμογή με δυνατότητες, κατάσταση εγκατάστασης/ενημέρωσης, Άνοιγμα, Λήψη (επιβεβαίωση + SHA-256, αρνείται σε ασυμφωνία, δεν εκτελεί τίποτα) και Λεπτομέρειες.
- **Balloon νέας έκδοσης** στο tray της αυτόνομης εφαρμογής: ένα αίτημα την ημέρα στο GitHub, μία φορά ανά έκδοση, με διακόπτη στο μενού. Χωρίς τηλεμετρία.
- Μέσα στο Opsis το plugin προσθέτει μόνο εγγραφές μενού/σελίδας· κανένα αυτόματο splash ή balloon.
- Περιλαμβάνονται επίσης: ο έλεγχος πριν την εφαρμογή αρχείων `.theme` (μη υπογεγραμμένα visual styles) και η εμφάνιση Opsis σε όλα τα στοιχεία και διαλόγους της σελίδας (είχαν γίνει commit στο repo μετά το 1.4.0).

**Δοκιμές**: 217 unit tests· screenshots του πραγματικού παραθύρου. **Δεν ελέγχθηκε**: το ίδιο το balloon (δεν φάνηκε στα screenshots), πραγματική λήψη installer, πραγματική νέα έκδοση, οι εγγραφές μέσα σε τρέχον Opsis.
