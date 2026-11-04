# Opsis Appearance 1.7.4

## English
- Inside Opsis the tray menu no longer lists the welcome guide and the Opsis family (Opsis shows them once); the stand-alone app keeps them.
- Binaries contain no local paths or user names: the plugin contract DLL is rebuilt from our own source, Opsis.UI is rebuilt deterministically, all projects use deterministic builds with a path map; tests scan the outputs.
- NOT VERIFIED: the plugin inside a running Opsis (tray), installer on a clean PC, real 150/200 % DPI.
- Assets with `.sha256`: Setup.exe, Plugin zip, Stand-alone zip.

## Ελληνικά
- Μέσα στο Opsis το μενού tray δεν δείχνει πια τον οδηγό και την οικογένεια Opsis (τα δείχνει μία φορά το Opsis)· το αυτόνομο τα κρατά.
- Τα binaries δεν περιέχουν τοπικές διαδρομές ή ονόματα χρήστη: το DLL συμβολαίου ξαναχτίστηκε από τον δικό μας κώδικα, το Opsis.UI ξαναχτίστηκε deterministic, όλα τα projects έχουν deterministic builds με path map· tests σαρώνουν τα αποτελέσματα.
- ΔΕΝ ελέγχθηκαν: το plugin μέσα σε τρέχον Opsis (tray), installer σε καθαρό PC, πραγματικό DPI 150/200 %.
