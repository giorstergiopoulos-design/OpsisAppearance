using System.Reflection;

namespace Opsis.Suite;

/// <summary>Το καλωσόρισμα, η «οικογένεια Opsis» και η ειδοποίηση νέας έκδοσης για το Opsis Appearance. Χρησιμοποιείται από την αυτόνομη εφαρμογή (αυτόματα) και
/// από τη σελίδα του plugin μέσα στο Opsis (ΜΟΝΟ όταν το ζητήσει ο χρήστης — το Opsis δείχνει το δικό του splash και ειδοποίηση).</summary>
public static class AppearanceSuite
{
    public const string AppId = "appearance", AppName = "Opsis Appearance", Repo = "OpsisAppearance", Owner = "giorstergiopoulos-design";

    public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opsis", "appearance");

    public static string Version
    {
        get { var v = Assembly.GetExecutingAssembly().GetName().Version; return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}"; }
    }

    public static SuiteContext CreateContext(SuiteColors colors, Func<Rectangle>? iconTarget = null, bool offline = false, string? dataDir = null)
    {
        dataDir ??= DataDir;
        return new SuiteContext
        {
            Owner = Owner, AppId = AppId, AppName = AppName, Repo = Repo, Version = Version, Colors = colors, AppIcon = SuiteResources.AppIcon(), IconTarget = iconTarget,
            EmbeddedCatalogue = SuiteResources.Catalogue(), CacheFile = Path.Combine(dataDir, "suite-apps.json"),
            Store = offline ? new MemoryStore() : new JsonFileStore(Path.Combine(dataDir, "suite.json")),
            Http = offline ? new OfflineHttp() : new RealHttp(),
        };
    }

    public static IReadOnlyList<WelcomeForm.Page> Pages()
    {
        string T(string el, string en) => SuiteLang.Current() == "el" ? el : en;
        return new List<WelcomeForm.Page>
        {
            new(T("Καλώς ήρθες στο Opsis Appearance", "Welcome to Opsis Appearance"),
                T("Αλλάζει την εμφάνιση των ίδιων των Windows 11: light/dark, χρώμα accent, γραμμή εργασιών, εφέ παραθύρων, θέματα και wallpaper.\n\nΛειτουργεί είτε ως αυτόνομη εφαρμογή στο tray είτε ως plugin μέσα στο Opsis, με τεκμηριωμένα API των Windows — χωρίς injection και χωρίς δικαιώματα διαχειριστή.",
                  "Changes the look of Windows 11 itself: light/dark, accent colour, taskbar, window effects, themes and wallpaper.\n\nIt works as a stand-alone tray app or as a plugin inside Opsis, using documented Windows APIs — no injection and no administrator rights.")),
            new(T("Οι κύριες περιοχές", "The main areas"),
                T("•  Light / Dark: χειροκίνητα ή αυτόματα με ανατολή/δύση.\n•  Accent: έτοιμα χρώματα, δικό σου ή από το wallpaper.\n•  Taskbar: διάφανο, blur, acrylic, στρογγυλεμένο.\n•  Εφέ παραθύρων: Mica/Acrylic, γωνίες, χρώμα τίτλου, με λίστα εξαιρέσεων.\n•  Theme Manager, slideshow wallpaper και ρυθμίσεις Εξερευνητή.",
                  "•  Light / Dark: manual or automatic at sunrise/sunset.\n•  Accent: presets, your own or from the wallpaper.\n•  Taskbar: translucent, blur, acrylic, rounded.\n•  Window effects: Mica/Acrylic, corners, title colours, with an exclusion list.\n•  Theme Manager, wallpaper slideshow and Explorer settings.")),
            new(T("Οι 3 πιο χρήσιμες ενέργειες", "The 3 most useful actions"),
                T("1.  Θέμα Light/Dark → «Αυτόματα με ανατολή/δύση».\n2.  Εμφάνιση taskbar → διάλεξε «Διαφανές» ή «Acrylic» και δες το αποτέλεσμα αμέσως.\n3.  Theme Manager → «Αποθήκευση τρέχουσας ως θέμα…» για να επιστρέφεις με ένα κλικ στη ρύθμιση που σου αρέσει, ή εισαγωγή/εξαγωγή θεμάτων .opsistheme.",
                  "1.  Light/Dark theme → \"Automatic (sunrise/sunset)\".\n2.  Taskbar look → pick \"Clear\" or \"Acrylic\" and see it at once.\n3.  Theme Manager → \"Save current as theme…\" to come back to a setup you like with one click, or import/export .opsistheme files.")),
            new(T("Ασφάλεια και επαναφορά", "Safety and restore"),
                T("•  Η πρώτη αλλαγή αποθηκεύει αντίγραφο των αρχικών τιμών· το κουμπί «Επαναφορά στις αρχικές μου ρυθμίσεις» τις γυρνά όλες πίσω.\n•  Τα εφέ παραθύρων και το taskbar ζουν μόνο όσο τρέχει η εφαρμογή και επαναφέρονται όταν σταματήσει.\n•  Πριν από την πρώτη παρεμβατική ρύθμιση ζητείται σημείο επαναφοράς συστήματος (θέλει διαχειριστή)· υπάρχει και αντίγραφο ασφαλείας του αρχικού κάθε αρχείου πριν από οποιαδήποτε επέμβαση σε αρχείο. Σήμερα δεν τροποποιείται κανένα αρχείο των Windows.",
                  "•  The first change saves a copy of your original values; \"Restore my original settings\" puts them all back.\n•  Window effects and the taskbar look only live while the app runs and are reverted when it stops.\n•  Before the first invasive setting a system restore point is requested (needs administrator rights), and a verified backup of the original of any file is kept before it is ever changed. Today no Windows file is modified.")),
            new(T("Πού βρίσκονται οι ρυθμίσεις", "Where the settings are"),
                T("Αυτόνομη εφαρμογή: εικονίδιο στο tray (διπλό κλικ για το παράθυρο, δεξί κλικ για το μενού, «Έξοδος» επαναφέρει τα εφέ).\nΩς plugin: σελίδα «Εμφάνιση Windows» μέσα στο Opsis.\n\nΔεδομένα: %AppData%\\Opsis\\appearance. Ενημερώσεις: κάθε ~24 ώρες ένα αίτημα προς το GitHub για νέα έκδοση (η αυτόνομη εφαρμογή, με ρύθμιση για απενεργοποίηση στο μενού του tray).",
                  "Stand-alone: tray icon (double click for the window, right click for the menu, \"Exit\" restores the effects).\nAs a plugin: the \"Windows appearance\" page inside Opsis.\n\nData: %AppData%\\Opsis\\appearance. Updates: about every 24 hours one request goes to GitHub to look for a new version (stand-alone app only, switch it off in the tray menu).")),
            new(T("Οι εφαρμογές της οικογένειας Opsis", "The Opsis family"),
                T("Το Opsis Appearance είναι μέρος μιας οικογένειας εφαρμογών: Opsis, News Crawler, GearWin, Opsis Media Player Pro, Kombos και Waveframe.\n\nΑπό το μενού βλέπεις τι κάνει η καθεμία, ποιες έχεις και μπορείς να τις ανοίξεις ή να τις κατεβάσεις (με έλεγχο SHA-256).",
                  "Opsis Appearance is part of a family of apps: Opsis, News Crawler, GearWin, Opsis Media Player Pro, Kombos and Waveframe.\n\nThe menu shows what each does, which ones you have, and lets you open or download them (with a SHA-256 check).")),
        };
    }

    public static WelcomeForm ShowWelcome(SuiteContext ctx, Action<WelcomeForm>? onClosed = null, IWin32Window? owner = null)
    {
        var w = new WelcomeForm(ctx, Pages());
        if (onClosed != null) w.FormClosed += (_, _) => onClosed(w);
        if (owner != null) w.Show(owner); else w.Show();
        return w;
    }

    public static async Task<SuiteForm> ShowFamilyAsync(SuiteContext ctx, IWin32Window? owner = null)
    {
        var cat = await ctx.LoadCatalogueAsync();
        var f = new SuiteForm(ctx, cat);
        if (owner != null) f.Show(owner); else f.Show();
        return f;
    }

    /// <summary>Αυτόματο καλωσόρισμα: μόνο όταν δεν έχει δει αυτή τη major/minor έκδοση και δεν έχει πει «να μην εμφανιστεί ξανά».</summary>
    public static bool ShouldAutoWelcome(IKeyValueStore store, string version) =>
        store.Get("welcome.never") != "1" && store.Get("welcome.shown") != WelcomeForm.MajorMinor(version);
}
