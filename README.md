# Opsis Appearance

<p align="center">
  <img src="src/OpsisAppearance.App/Assets/app.ico" width="96" height="96" alt="Opsis Appearance" />
</p>

<p align="center">
  <strong>Άλλαξε την εμφάνιση των ίδιων των Windows 11 — ως plugin του Opsis ή ως αυτόνομη εφαρμογή.</strong><br/>
  <strong>Change the look of Windows 11 itself — as an Opsis plugin or as a stand-alone app.</strong>
</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2011-0078D6?logo=windows&logoColor=white" />
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="Version" src="https://img.shields.io/badge/version-1.3.0-00B4D8" />
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green" />
</p>

*English below / Αγγλικά παρακάτω.*

## Ελληνικά

Το **Opsis Appearance** αλλάζει την εμφάνιση του περιβάλλοντος των Windows με τεκμηριωμένα API (registry του τρέχοντος χρήστη και DWM) — **χωρίς injection, χωρίς drivers, χωρίς δικαιώματα διαχειριστή**.

| Λειτουργία | Τι κάνει |
|---|---|
| ☀ / ☾ Αυτόματο θέμα | Light/Dark με **ανατολή/δύση** (υπολογίζεται από γεωγραφικό πλάτος/μήκος· προεπιλογή Αθήνα) ή σταθερές ώρες, με μετατόπιση λεπτών |
| 🎨 Accent | 12 έτοιμα χρώματα, δικό σου χρώμα, ή **χρώμα από το wallpaper** (κυρίαρχο χρώμα της εικόνας) |
| 🧱 Taskbar | Στοίχιση αριστερά/κέντρο, Αναζήτηση, Task View, Widgets, διαφάνειες, accent σε Start/taskbar/τίτλους |
| 🪟 Εφέ παραθύρων | Χρωματιστό **border στο ενεργό παράθυρο**, **γωνίες** (στρογγυλές/μικρές/τετράγωνες), **σκούρες γραμμές τίτλου**, backdrop **Mica / Acrylic / Tabbed**, αυτόματη απόκρυψη taskbar |
| 📁 Εξερευνητής και taskbar | Επεκτάσεις αρχείων, κρυφά αρχεία, δευτερόλεπτα στο ρολόι, animations παραθύρων, συνδυασμός κουμπιών στο taskbar (πάντα / όταν γεμίσει / ποτέ με ετικέτες), επανεκκίνηση του Εξερευνητή με ένα κουμπί |
| 🖼 Wallpaper slideshow | Αλλάζει το wallpaper από φάκελο κάθε N λεπτά (τυχαία ή με σειρά, όλοι οι τρόποι προσαρμογής), «Επόμενο τώρα», και επαναφορά του αρχικού wallpaper όταν το σβήσεις |
| 🗂 Theme Manager | **Πλήρη θέματα** (εμφάνιση + εφέ + wallpaper): εφαρμογή με ένα κλικ, αποθήκευση του τρέχοντος ως θέμα, **εισαγωγή/εξαγωγή `.opsistheme`**, καθώς και λίστα των `.theme` των Windows |
| ↩ Επαναφορά | Την πρώτη αλλαγή αποθηκεύεται αντίγραφο των αρχικών τιμών· το κουμπί «Επαναφορά» τις γυρνά όλες πίσω. Τα εφέ παραθύρων επαναφέρονται πλήρως όταν σταματά η εφαρμογή |

**Ελαφρύ για αδύναμα PC:** κανένα background thread, κανένα polling. Ένας timer 60 δευτ. μόνο όσο είναι ενεργό αυτόματο θέμα· τα εφέ παραθύρων χρησιμοποιούν ειδοποιήσεις των Windows (`SetWinEventHook`) και όχι έλεγχο σε βρόχο.

### Εγκατάσταση

Κατέβασε από τα [Releases](../../releases/latest):

| Αρχείο | Τι είναι |
|---|---|
| `OpsisAppearance-Setup.exe` | Installer — επιλέγεις **«Ως plugin του Opsis»** ή **«Αυτόνομη εφαρμογή»** (προτείνει το plugin αν βρει το Opsis). Χωρίς admin |
| `OpsisAppearance-Standalone-<έκδοση>.zip` | Αυτόνομη εφαρμογή, χωρίς εγκατάσταση (θέλει .NET 10 Desktop Runtime) |
| `OpsisAppearance-Plugin-<έκδοση>.zip` | Μόνο το plugin: αντίγραψε τον φάκελο `Opsis Appearance` στο `%AppData%\Opsis\plugins\` και κάνε επανεκκίνηση του Opsis |

Ο installer του **Opsis** (2.1.1+) προσφέρει επίσης επιλογή «Λήψη και εγκατάσταση του Opsis Appearance» και το εγκαθιστά ως plugin.

Σιωπηλή εγκατάσταση: `OpsisAppearance-Setup.exe /VERYSILENT /MODE=plugin` ή `/MODE=standalone`.

### Ασφάλεια των θεμάτων `.opsistheme`

Ένα `.opsistheme` είναι zip με **μόνο** `theme.json` και προαιρετικά `wallpaper.png|jpg|bmp` (έως 40 MB). Η εισαγωγή απορρίπτει οποιοδήποτε άλλο αρχείο (διαδρομές, `.exe`, `.dll`), μετράει τα πραγματικά bytes (όχι το μέγεθος του header), και καθαρίζει όλες τις τιμές. Δεν εκτελείται κώδικας.

### Χτίσιμο

```powershell
powershell -File build.ps1          # δοκιμές + standalone + plugin + zips + installer (Inno Setup 7)
dotnet test OpsisAppearance.sln     # 83 unit tests
```

Απαιτείται: Windows 11, [.NET 10 SDK](https://dotnet.microsoft.com/download), [Inno Setup 7](https://jrsoftware.org/isdl.php).

### Δομή

```
src/OpsisAppearance.Plugin   η μηχανή + η σελίδα (το DLL που φορτώνει το Opsis)
src/OpsisAppearance.App      η αυτόνομη εφαρμογή (tray + παράθυρο, ίδια σελίδα)
tests/                       unit tests (ψεύτικο registry/παράθυρα/wallpaper — δεν αγγίζουν τις ρυθμίσεις σου)
installer/                   Inno Setup 7 (Opsis plugin ή αυτόνομο)
libs/                        το κοινό συμβόλαιο plugin του Opsis
```

## English

**Opsis Appearance** changes the look of the Windows environment through documented APIs (per-user registry and DWM) — **no injection, no drivers, no administrator rights**.

| Feature | What it does |
|---|---|
| ☀ / ☾ Automatic theme | Light/Dark at **sunrise/sunset** (computed from latitude/longitude, Athens by default) or fixed times, with a minute offset |
| 🎨 Accent | 12 presets, your own colour, or the **colour of your wallpaper** (dominant colour of the image) |
| 🧱 Taskbar | Left/centre alignment, Search, Task View, Widgets, transparency, accent on Start/taskbar/title bars |
| 🪟 Window effects | Coloured **border on the active window**, **corners** (round/small/square), **dark title bars**, **Mica / Acrylic / Tabbed** backdrop, taskbar auto-hide |
| 🗂 Theme Manager | **Full themes** (look + effects + wallpaper): one-click apply, save the current look as a theme, **import/export `.opsistheme`**, plus a list of the Windows `.theme` files |
| ↩ Restore | The first change saves a copy of your original values; "Restore" puts them all back. Window effects are fully undone when the app stops |

**Light on weak PCs:** no background threads, no polling. One 60 s timer only while an automatic schedule is on; the window effects use Windows notifications (`SetWinEventHook`), not a polling loop.

### Install

Download from [Releases](../../releases/latest):

| File | What it is |
|---|---|
| `OpsisAppearance-Setup.exe` | Installer — choose **"As a plugin of Opsis"** or **"Stand-alone app"** (it proposes the plugin when it finds Opsis). No admin needed |
| `OpsisAppearance-Standalone-<version>.zip` | The stand-alone app, no installation (needs the .NET 10 Desktop Runtime) |
| `OpsisAppearance-Plugin-<version>.zip` | The plugin only: copy the `Opsis Appearance` folder to `%AppData%\Opsis\plugins\` and restart Opsis |

The **Opsis** installer (2.1.1+) also offers "Download and install Opsis Appearance" and installs it as a plugin.

Silent install: `OpsisAppearance-Setup.exe /VERYSILENT /MODE=plugin` or `/MODE=standalone`.

### Safety of `.opsistheme` packages

A `.opsistheme` is a zip with **only** `theme.json` and an optional `wallpaper.png|jpg|bmp` (up to 40 MB). Import rejects any other file (paths, `.exe`, `.dll`), counts the real bytes (not the size claimed in the header) and sanitises every value. Nothing is executed.

### Build

```powershell
powershell -File build.ps1          # tests + stand-alone + plugin + zips + installer (Inno Setup 7)
dotnet test OpsisAppearance.sln     # 72 unit tests
```

Requires Windows 11, the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Inno Setup 7](https://jrsoftware.org/isdl.php).

## Άδεια / License

MIT — see [LICENSE](LICENSE). Part of the Opsis family: [Opsis](https://github.com/giorstergiopoulos-design/Opsis) · [Opsis News Crawler](https://github.com/giorstergiopoulos-design/OpsisNewsCrawler) · [Waveframe](https://github.com/giorstergiopoulos-design/waveframe).
