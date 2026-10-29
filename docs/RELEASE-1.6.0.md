# Opsis Appearance 1.6.0

## English
- The settings page now uses the shared **Opsis.UI** controls (check box, button, drop-down) instead of its own copies; they follow the host theme through one live adapter and add a keyboard focus ring, Space toggle, DPI scaling and right-to-left support.
- `Opsis.UI.dll` is installed next to the plugin DLL (installer, plugin zip, stand-alone zip). Inside Opsis it must keep the same assembly version as Opsis's own copy.
- Verified: 220 unit tests, settings page captured in dark and light, English and Greek. Not verified: real 150/200 % DPI and RTL on this page, the plugin loaded inside a running Opsis.
- Assets with `.sha256`: Setup.exe, Plugin zip, Stand-alone zip.

## Ελληνικά
- Η σελίδα ρυθμίσεων χρησιμοποιεί πλέον τα κοινά controls **Opsis.UI** (checkbox, κουμπί, drop-down) αντί για δικά της αντίγραφα· ακολουθούν το θέμα του host και προσθέτουν focus ring πληκτρολογίου, Space, κλιμάκωση DPI και RTL.
- Το `Opsis.UI.dll` εγκαθίσταται δίπλα στο DLL του plugin. Μέσα στο Opsis πρέπει να έχει την ίδια έκδοση assembly με το δικό του αντίγραφο.
- Ελέγχθηκαν: 220 unit tests, σελίδα σε dark/light, EN/EL. Δεν ελέγχθηκαν: πραγματικό DPI 150/200 % και RTL σε αυτή τη σελίδα, το plugin μέσα σε τρέχον Opsis.
