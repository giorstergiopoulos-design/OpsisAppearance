# Opsis Appearance 1.7.5

## English
- The update no longer uses a hidden PowerShell: SHA-256 check from a locked handle, then the installer starts directly (UAC) and Inno closes/reopens the app.
- Standard MIT license text; release information (publisher, product, version, copyright) in all binaries and in the installer; Inno Setup 7 required; no PDB and no local paths, user names or e-mail in binaries and zips (tests scan them).
- New installer end-to-end workflow for clean Windows runners.
- NOT VERIFIED: the update flow against a real newer release, the new workflows (not run yet; security-scan must be green), installer on a clean PC.
- Assets with `.sha256`: Setup.exe, Plugin zip, Stand-alone zip.

## Ελληνικά
- Η ενημέρωση δεν χρησιμοποιεί πια κρυφό PowerShell: έλεγχος SHA-256 από κλειδωμένο handle και άμεση εκκίνηση του installer (UAC)· το Inno κλείνει/ανοίγει ξανά την εφαρμογή.
- Τυπικό κείμενο MIT· πληροφορίες έκδοσης (εκδότης, προϊόν, έκδοση, copyright) σε όλα τα binaries και στον installer· απαιτείται Inno Setup 7· χωρίς PDB και χωρίς τοπικές διαδρομές, ονόματα χρήστη ή email σε binaries και zip (tests τα σαρώνουν).
- Νέο workflow installer end-to-end σε καθαρά Windows runners.
- ΔΕΝ ελέγχθηκαν: η ενημέρωση με πραγματικό νεότερο release, τα νέα workflows (δεν έχουν τρέξει· το security-scan πρέπει να είναι πράσινο), installer σε καθαρό PC.
