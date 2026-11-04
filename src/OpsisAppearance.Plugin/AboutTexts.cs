using System.Reflection;

namespace OpsisAppearance;

/// <summary>Βοήθεια, Οδηγίες χρήσης, Ιστορικό εκδόσεων και Άδεια χρήσης (ελληνικά και αγγλικά) — τα ίδια τέσσερα τμήματα με το Crawler. Εμφανίζονται στο «Σχετικά» του Opsis και στην καρτέλα «Σχετικά» της σελίδας.</summary>
public static class AboutTexts
{
    public static string Help(bool el) => el
        ? "Θέμα και χρώμα: Light/Dark χειροκίνητα ή αυτόματα με ανατολή/δύση ή σταθερές ώρες, και χρώμα accent (έτοιμα χρώματα, δικό σου ή από το wallpaper).\n\nTaskbar: διάφανο, blur, acrylic, στρογγυλεμένο· κουμπιά αναζήτησης, Task View, Widgets και κεντραρισμένα εικονίδια.\n\nΠαράθυρα: Mica/Acrylic, γωνίες, χρώμα τίτλου, σκούρες γραμμές τίτλου και λίστα εξαιρέσεων.\n\nΣύστημα: σημείο επαναφοράς, αντίγραφα ασφαλείας και ρυθμίσεις Εξερευνητή (επεκτάσεις, κρυφά αρχεία, δευτερόλεπτα ρολογιού, συνδυασμός κουμπιών).\n\nWallpaper και θέματα: slideshow από φάκελο, Theme Manager (.opsistheme) και θέματα Windows (.theme).\n\nΕνημερώσεις: οδηγός χρήσης, οι εφαρμογές της οικογένειας Opsis και έλεγχος ενημερώσεων.\n\nΚάθε ρύθμιση έχει ένδειξη «Άμεσα» ή «Χρειάζεται Explorer». Η αναζήτηση βρίσκει ρυθμίσεις σε όλες τις καρτέλες."
        : "Theme & colour: Light/Dark manually or automatically at sunrise/sunset or fixed times, and the accent colour (presets, your own or from the wallpaper).\n\nTaskbar: clear, blur, acrylic, rounded; search, Task View, Widgets buttons and centred icons.\n\nWindows: Mica/Acrylic, corners, title colours, dark title bars and an exclusion list.\n\nSystem: restore point, file backups and Explorer settings (extensions, hidden files, clock seconds, button grouping).\n\nWallpaper & themes: folder slideshow, Theme Manager (.opsistheme) and Windows themes (.theme).\n\nUpdates: the welcome guide, the Opsis family and update checks.\n\nEvery setting shows \"Immediate\" or \"Needs Explorer restart\". The search box finds settings in every tab.";

    public static string Instructions(bool el) => el
        ? "Γρήγορη εκκίνηση\n1. Διάλεξε Light/Dark ή «Αυτόματα με ανατολή/δύση» στην καρτέλα Θέμα και χρώμα.\n2. Διάλεξε χρώμα accent.\n3. Στο Taskbar δοκίμασε «Διαφανές» ή «Acrylic» και δες το αποτέλεσμα αμέσως.\n\nΣημείο επαναφοράς και αντίγραφα\n• Πριν από την πρώτη παρεμβατική ρύθμιση ζητείται σημείο επαναφοράς συστήματος (θέλει δικαιώματα διαχειριστή· χωρίς αυτά παραλείπεται).\n• Η πρώτη αλλαγή αποθηκεύει αντίγραφο των αρχικών τιμών. Το κουμπί «Επαναφορά στις αρχικές μου ρυθμίσεις» (καρτέλα Ενημερώσεις) τις γυρνά όλες πίσω.\n• Κάθε καρτέλα έχει «Επαναφορά κατηγορίας» για τις προεπιλογές της.\n• Τα εφέ παραθύρων και το taskbar ζουν μόνο όσο τρέχει η εφαρμογή και επαναφέρονται όταν σταματήσει.\n\nΘέματα\n«Αποθήκευση τρέχουσας ως θέμα…» κρατά τη ρύθμιση που σου αρέσει· εισαγωγή/εξαγωγή αρχείων .opsistheme."
        : "Quick start\n1. Pick Light/Dark or \"Automatic (sunrise/sunset)\" in the Theme & colour tab.\n2. Pick an accent colour.\n3. In Taskbar try \"Clear\" or \"Acrylic\" and see the result at once.\n\nRestore point and backups\n• Before the first invasive setting a system restore point is requested (needs administrator rights; skipped without them).\n• The first change saves a copy of your original values. \"Restore my original settings\" (Updates tab) puts them all back.\n• Every tab has \"Reset this category\" for its defaults.\n• Window effects and the taskbar look only live while the app runs and are reverted when it stops.\n\nThemes\n\"Save current as theme...\" keeps a setup you like; import/export .opsistheme files.";

    /// <summary>Ιστορικό εκδόσεων: ΠΑΝΤΑ ενημερώνεται μαζί με το CHANGELOG (το test ελέγχει ότι η τρέχουσα έκδοση υπάρχει και στις δύο γλώσσες).</summary>
    public static readonly (string Version, string El, string En)[] HistoryEntries =
    {
        ("1.7.5", "Υγιεινή κυκλοφορίας: τυπικό MIT, πληροφορίες έκδοσης και εκδότης, Inno Setup 7, χωρίς κρυφό PowerShell στην ενημέρωση, χωρίς ίχνη στα binaries και στα zip, CI για installer.", "Release hygiene: standard MIT, version info and publisher, Inno Setup 7, no hidden PowerShell in the update, no traces in binaries and zips, installer CI."),
        ("1.7.4", "Μέσα στο Opsis το tray δεν δείχνει διπλά τον οδηγό και την οικογένεια Opsis· χωρίς τοπικές διαδρομές στα DLL· ξαναχτίστηκαν τα κοινά libs.", "Inside Opsis the tray no longer shows the guide and the Opsis family twice; no local paths in the DLLs; the shared libs were rebuilt."),
        ("1.7.3", "Ενότητα «Σχετικά»: Βοήθεια, Οδηγίες χρήσης, Ιστορικό εκδόσεων, Άδεια χρήσης (και καρτέλα Σχετικά)· ενημέρωση Opsis.UI (κομμένο checkbox/διακόπτης).", "About section: Help, Instructions, Version history, License (and an About tab); Opsis.UI update (cut-off check box / switch)."),
        ("1.7.2", "Αναζήτηση ρυθμίσεων, «Επαναφορά κατηγορίας», log ενημέρωσης.", "Settings search, \"Reset this category\", update log."),
        ("1.7.1", "Popup ενημέρωσης και μέσα στο Opsis· ρυθμίσεις σε έξι καρτέλες.", "Update popup also inside Opsis; settings in six tabs."),
        ("1.7.0", "Ρυθμίσεις τύπου Windows 11, ένας κινητήρας τη φορά, installer σε Program Files, ενιαίο popup ενημέρωσης.", "Windows 11 style settings, one engine at a time, Program Files installer, unified update popup."),
        ("1.6.0", "Κοινά controls Opsis.UI στη θέση των δικών μας.", "Shared Opsis.UI controls instead of our own."),
        ("1.5.1", "Έλεγχος ασφαλείας, πραγματικά εικονίδια οικογένειας, εικονίδιο στον installer.", "Security audit, real family icons, installer icon."),
        ("1.5.0", "Οδηγός χρήσης, οι εφαρμογές της οικογένειας Opsis, ειδοποίηση νέας έκδοσης.", "Welcome guide, the Opsis family, new-version notice."),
        ("1.4.0", "Έλεγχος αρχείων .theme πριν την εφαρμογή.", "Pre-flight check of .theme files."),
        ("1.3.0", "Εμφάνιση taskbar, στρογγυλεμένο taskbar, αντίγραφα ασφαλείας.", "Taskbar look, rounded taskbar, backups."),
        ("1.2.0", "Πρώτη δημόσια έκδοση.", "First public release."),
    };

    public static string History(bool el) => string.Join("\n\n", HistoryEntries.Select(h => h.Version + " — " + (el ? h.El : h.En)));

    private static string ReadLicense()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("about.license.txt"); if (s == null) return "MIT License";
            using var r = new StreamReader(s); return r.ReadToEnd();
        }
        catch (Exception) { return "MIT License"; }
    }

    public static string License(bool el) =>
        ReadLicense().TrimEnd() + "\n\n" + (el ? "Συστατικά τρίτων" : "Third-party components") + "\n• Opsis.UI (MIT, Opsis Team)\n• .NET / Windows Forms (MIT, Microsoft)";
}
