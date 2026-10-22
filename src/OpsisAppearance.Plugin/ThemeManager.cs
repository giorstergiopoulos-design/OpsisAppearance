using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OpsisAppearance;

public enum WallpaperStyle { Fill, Fit, Stretch, Tile, Center, Span }

/// <summary>
/// Ένα ΠΛΗΡΕΣ θέμα του Opsis: η εμφάνιση των Windows (light/dark, accent, taskbar, διαφάνειες) + τα εφέ παραθύρων + προαιρετικά wallpaper.
/// Αποθηκεύεται ως φάκελος (theme.json + wallpaper.*) και ανταλλάσσεται ως ένα αρχείο .opsistheme (zip).
/// </summary>
public sealed class OpsisTheme
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
    public Look Look { get; set; } = new();
    public EffectsSettings? Effects { get; set; }              // null = δεν αλλάζει τα εφέ
    public string? WallpaperFile { get; set; }                  // wallpaper.png/.jpg/.bmp μέσα στο πακέτο
    public WallpaperStyle WallpaperStyle { get; set; } = WallpaperStyle.Fill;
    [System.Text.Json.Serialization.JsonIgnore] public bool BuiltIn { get; set; }
}

/// <summary>Wallpaper των Windows — interface ώστε η λογική να δοκιμάζεται χωρίς να αλλάζει τη πραγματική επιφάνεια εργασίας.</summary>
public interface IWallpaperApi
{
    (string? Path, WallpaperStyle Style) Current();
    void Set(string path, WallpaperStyle style);
}

public sealed class RealWallpaper : IWallpaperApi
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);
    private const uint SPI_SETDESKWALLPAPER = 0x0014, SPIF_UPDATEINIFILE = 1, SPIF_SENDCHANGE = 2;
    private const string Key = @"Control Panel\Desktop";

    public (string? Path, WallpaperStyle Style) Current()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        string? path = k?.GetValue("WallPaper") as string;
        string style = k?.GetValue("WallpaperStyle") as string ?? "10", tile = k?.GetValue("TileWallpaper") as string ?? "0";
        return (string.IsNullOrWhiteSpace(path) ? null : path, FromRegistry(style, tile));
    }

    public void Set(string path, WallpaperStyle style)
    {
        var (s, t) = ToRegistry(style);
        using (var k = Registry.CurrentUser.CreateSubKey(Key)) { k.SetValue("WallpaperStyle", s, RegistryValueKind.String); k.SetValue("TileWallpaper", t, RegistryValueKind.String); }
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }

    public static (string Style, string Tile) ToRegistry(WallpaperStyle s) => s switch
    {
        WallpaperStyle.Fill => ("10", "0"), WallpaperStyle.Fit => ("6", "0"), WallpaperStyle.Stretch => ("2", "0"),
        WallpaperStyle.Tile => ("0", "1"), WallpaperStyle.Center => ("0", "0"), WallpaperStyle.Span => ("22", "0"), _ => ("10", "0"),
    };

    public static WallpaperStyle FromRegistry(string style, string tile) =>
        tile == "1" ? WallpaperStyle.Tile : style switch { "10" => WallpaperStyle.Fill, "6" => WallpaperStyle.Fit, "2" => WallpaperStyle.Stretch, "22" => WallpaperStyle.Span, _ => WallpaperStyle.Center };
}

/// <summary>Βιβλιοθήκη θεμάτων στο δίσκο (%AppData%\Opsis\themes) + ασφαλές import/export.</summary>
public sealed class ThemeLibrary
{
    public const long MaxWallpaperBytes = 40L * 1024 * 1024;
    private const long MaxJsonBytes = 256 * 1024;
    private static readonly string[] WallpaperExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };
    private static readonly Regex WallpaperEntry = new(@"^wallpaper\.(png|jpg|jpeg|bmp)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Root { get; }
    public ThemeLibrary(string root) { Root = root; }

    public static string DefaultRoot
    {
        get
        {
            string? dir = Environment.GetEnvironmentVariable("OPSIS_APPDATA");
            if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opsis");
            return Path.Combine(dir, "themes");
        }
    }

    /// <summary>Όνομα ασφαλές για φάκελο: μόνο γράμματα/αριθμοί/κενά/._-() , έως 60 χαρακτήρες, χωρίς ".." και χωρίς τελείες στην αρχή.</summary>
    public static string SafeName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name.Trim()) sb.Append(char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.' or '(' or ')' ? c : '_');
        string s = Regex.Replace(sb.ToString().Trim(), @"\.{2,}", ".").TrimStart('.');       // no ".." sequences at all, even though separators are already gone
        if (s.Length > 60) s = s[..60].TrimEnd();
        return s;
    }

    public string FolderOf(string name) => Path.Combine(Root, SafeName(name));

    public static List<OpsisTheme> BuiltIn() => new()
    {
        new OpsisTheme { Name = "Day", BuiltIn = true, Description = "Light, blue accent, transparency on", Look = new Look { Dark = false, Transparency = true, AccentRgb = 0x0078D4 }, Effects = new EffectsSettings() },
        new OpsisTheme { Name = "Night", BuiltIn = true, Description = "Dark, purple accent", Look = new Look { Dark = true, Transparency = true, AccentRgb = 0x8764B8 }, Effects = new EffectsSettings() },
        new OpsisTheme { Name = "Neon", BuiltIn = true, Description = "Dark, cyan accent, coloured active-window border, small round corners",
            Look = new Look { Dark = true, Transparency = true, AccentRgb = 0x00B7C3, AccentOnSurfaces = true }, Effects = new EffectsSettings { ActiveBorder = true, BorderRgb = 0x00E5FF, Corners = CornerStyle.RoundSmall, DarkTitle = DarkTitleBars.On } },
        new OpsisTheme { Name = "Focus", BuiltIn = true, Description = "Dark, no transparency, minimal taskbar", Look = new Look { Dark = true, Transparency = false, AccentOnSurfaces = false, ShowTaskView = false, ShowWidgets = false, ShowSearch = false }, Effects = new EffectsSettings() },
        new OpsisTheme { Name = "Classic taskbar", BuiltIn = true, Description = "Taskbar on the left with Search and Task View", Look = new Look { TaskbarCentered = false, ShowTaskView = true, ShowSearch = true } },
    };

    public List<OpsisTheme> List()
    {
        var list = BuiltIn();
        if (!Directory.Exists(Root)) return list;
        foreach (var dir in Directory.EnumerateDirectories(Root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var t = Load(dir);
            if (t != null && !list.Any(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase))) list.Add(t);
        }
        return list;
    }

    private static OpsisTheme? Load(string dir)
    {
        try
        {
            string file = Path.Combine(dir, "theme.json");
            if (!File.Exists(file) || new FileInfo(file).Length > MaxJsonBytes) return null;
            var t = JsonSerializer.Deserialize<OpsisTheme>(File.ReadAllText(file));
            return t == null ? null : Sanitize(t, dir);
        }
        catch (Exception) { return null; }       // JSON sbistos/μη συμβατός: το θέμα απλώς δεν εμφανίζεται
    }

    /// <summary>Κάθε τιμή από αρχείο (ή από import) θεωρείται αναξιόπιστη: όνομα, enums, wallpaper path.</summary>
    private static OpsisTheme Sanitize(OpsisTheme t, string dir)
    {
        t.Name = SafeName(string.IsNullOrWhiteSpace(t.Name) ? Path.GetFileName(dir) : t.Name);
        if (t.Name.Length == 0) t.Name = "Theme";
        t.Description = (t.Description ?? "").Length > 200 ? t.Description![..200] : t.Description ?? "";
        if (!Enum.IsDefined(t.WallpaperStyle)) t.WallpaperStyle = WallpaperStyle.Fill;
        if (t.Look.AccentRgb is int a) t.Look.AccentRgb = a & 0xFFFFFF;
        if (t.Effects != null)
        {
            if (!Enum.IsDefined(t.Effects.Corners)) t.Effects.Corners = CornerStyle.Default;
            if (!Enum.IsDefined(t.Effects.DarkTitle)) t.Effects.DarkTitle = DarkTitleBars.Off;
            if (!Enum.IsDefined(t.Effects.Backdrop)) t.Effects.Backdrop = BackdropStyle.Default;
            if (t.Effects.BorderRgb is int b) t.Effects.BorderRgb = b & 0xFFFFFF;
        }
        // Το wallpaper πρέπει να είναι ΑΠΛΟ όνομα αρχείου μέσα στο φάκελο του θέματος — ποτέ διαδρομή.
        if (t.WallpaperFile != null && (!WallpaperEntry.IsMatch(t.WallpaperFile) || !File.Exists(Path.Combine(dir, t.WallpaperFile)))) t.WallpaperFile = null;
        return t;
    }

    public string? WallpaperPath(OpsisTheme t) => t.WallpaperFile == null || t.BuiltIn ? null : Path.Combine(FolderOf(t.Name), t.WallpaperFile);

    /// <summary>Αποθηκεύει (ή αντικαθιστά) ένα θέμα. Αν δοθεί wallpaperSource, αντιγράφεται μέσα στο πακέτο (όριο 40 MB, png/jpg/bmp).</summary>
    public string Save(OpsisTheme t, string? wallpaperSource = null)
    {
        t.Name = SafeName(t.Name);
        if (t.Name.Length == 0) throw new ArgumentException("Empty theme name");
        if (BuiltIn().Any(b => string.Equals(b.Name, t.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("A built-in theme has this name");
        string dir = FolderOf(t.Name);
        Directory.CreateDirectory(dir);
        if (wallpaperSource != null)
        {
            string ext = Path.GetExtension(wallpaperSource).ToLowerInvariant();
            if (!WallpaperExtensions.Contains(ext)) throw new InvalidOperationException("Unsupported wallpaper type");
            if (new FileInfo(wallpaperSource).Length > MaxWallpaperBytes) throw new InvalidOperationException("Wallpaper is larger than 40 MB");
            foreach (var old in Directory.GetFiles(dir, "wallpaper.*")) File.Delete(old);
            t.WallpaperFile = "wallpaper" + (ext == ".jpeg" ? ".jpg" : ext);
            File.Copy(wallpaperSource, Path.Combine(dir, t.WallpaperFile), overwrite: true);
        }
        File.WriteAllText(Path.Combine(dir, "theme.json"), JsonSerializer.Serialize(t, Json));
        return dir;
    }

    public bool Delete(string name)
    {
        if (BuiltIn().Any(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase))) return false;
        string dir = FolderOf(name);
        // Ασφάλεια: διαγράφουμε ΜΟΝΟ φάκελο που βρίσκεται ακριβώς κάτω από τη ρίζα και περιέχει theme.json.
        if (!Directory.Exists(dir) || !File.Exists(Path.Combine(dir, "theme.json"))) return false;
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(dir)), Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase)) return false;
        Directory.Delete(dir, recursive: true);
        return true;
    }

    // ---- .opsistheme (zip) -------------------------------------------------------------------------------------

    public void Export(OpsisTheme t, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var copy = new OpsisTheme { Name = t.Name, Description = t.Description, Created = t.Created, Look = t.Look, Effects = t.Effects, WallpaperFile = t.WallpaperFile, WallpaperStyle = t.WallpaperStyle };
        var e = zip.CreateEntry("theme.json");
        using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(JsonSerializer.Serialize(copy, Json));
        string? wp = WallpaperPath(t);
        if (wp != null && File.Exists(wp)) zip.CreateEntryFromFile(wp, t.WallpaperFile!);
    }

    /// <summary>
    /// Ασφαλές import: δέχεται ΜΟΝΟ τα αρχεία theme.json και wallpaper.(png|jpg|jpeg|bmp), τίποτα άλλο (καμία διαδρομή, κανένα .exe/.dll),
    /// με όρια μεγέθους ανά αρχείο — ένα κακόβουλο zip δεν μπορεί να γράψει έξω από το φάκελο του θέματος.
    /// </summary>
    public OpsisTheme Import(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count is 0 or > 4) throw new InvalidDataException("Not an Opsis theme package");
        var json = zip.Entries.FirstOrDefault(e => e.FullName == "theme.json") ?? throw new InvalidDataException("theme.json is missing");
        if (json.Length > MaxJsonBytes) throw new InvalidDataException("theme.json is too large");
        OpsisTheme? theme;
        using (var r = new StreamReader(json.Open(), Encoding.UTF8, true)) theme = JsonSerializer.Deserialize<OpsisTheme>(r.ReadToEnd());
        if (theme == null) throw new InvalidDataException("theme.json is empty");
        foreach (var e in zip.Entries)
            if (e.FullName != "theme.json" && !WallpaperEntry.IsMatch(e.FullName)) throw new InvalidDataException("Unexpected file in the package: " + e.FullName);

        var wall = zip.Entries.FirstOrDefault(e => WallpaperEntry.IsMatch(e.FullName));
        if (wall != null && wall.Length > MaxWallpaperBytes) throw new InvalidDataException("Wallpaper is larger than 40 MB");
        theme.WallpaperFile = wall?.FullName.ToLowerInvariant();
        theme.Name = SafeName(theme.Name);
        if (theme.Name.Length == 0) theme.Name = Path.GetFileNameWithoutExtension(zipPath);
        theme.Name = SafeName(theme.Name);
        if (BuiltIn().Any(b => string.Equals(b.Name, theme.Name, StringComparison.OrdinalIgnoreCase))) theme.Name += " (imported)";

        string dir = FolderOf(theme.Name);
        Directory.CreateDirectory(dir);
        if (wall != null)
        {
            string target = Path.Combine(dir, theme.WallpaperFile!);
            using var src = wall.Open(); using var dst = File.Create(target);
            CopyLimited(src, dst, MaxWallpaperBytes);                 // το μέγεθος στο header του zip δεν εμπιστεύεται: μετράμε τα πραγματικά bytes
        }
        var clean = Sanitize(theme, dir);
        File.WriteAllText(Path.Combine(dir, "theme.json"), JsonSerializer.Serialize(clean, Json));
        return clean;
    }

    private static void CopyLimited(Stream src, Stream dst, long limit)
    {
        var buf = new byte[81920]; long total = 0; int n;
        while ((n = src.Read(buf, 0, buf.Length)) > 0)
        {
            total += n;
            if (total > limit) throw new InvalidDataException("Wallpaper is larger than 40 MB");
            dst.Write(buf, 0, n);
        }
    }
}

/// <summary>Εφαρμογή θέματος: εμφάνιση Windows + εφέ παραθύρων + wallpaper. Δεν πετάει exception για ένα βήμα που αποτυγχάνει — επιστρέφει αναφορά.</summary>
public sealed class ThemeApplier
{
    private readonly AppearanceEngine _engine;
    private readonly Action<EffectsSettings> _applyEffects;
    private readonly IWallpaperApi _wallpaper;
    private readonly ThemeLibrary _library;

    public ThemeApplier(AppearanceEngine engine, Action<EffectsSettings> applyEffects, IWallpaperApi wallpaper, ThemeLibrary library)
    { _engine = engine; _applyEffects = applyEffects; _wallpaper = wallpaper; _library = library; }

    public sealed record Report(bool LookApplied, bool EffectsApplied, bool WallpaperApplied, List<string> Errors);

    public Report Apply(OpsisTheme t)
    {
        var errors = new List<string>();
        bool look = false, fx = false, wp = false;
        try { _engine.Apply(t.Look); look = true; } catch (Exception ex) { errors.Add("Look: " + ex.Message); }
        if (t.Effects != null) { try { _applyEffects(t.Effects); fx = true; } catch (Exception ex) { errors.Add("Effects: " + ex.Message); } }
        string? path = _library.WallpaperPath(t);
        if (path != null)
        {
            try { if (File.Exists(path)) { _wallpaper.Set(path, t.WallpaperStyle); wp = true; } else errors.Add("Wallpaper file is missing"); }
            catch (Exception ex) { errors.Add("Wallpaper: " + ex.Message); }
        }
        return new Report(look, fx, wp, errors);
    }

    /// <summary>Φωτογραφία της ΤΡΕΧΟΥΣΑΣ εμφάνισης ως θέμα (προαιρετικά με το wallpaper των Windows).</summary>
    public (OpsisTheme Theme, string? WallpaperSource) CaptureCurrent(string name, EffectsSettings currentEffects, bool includeWallpaper)
    {
        var t = new OpsisTheme { Name = name, Look = _engine.ReadCurrent(), Effects = Clone(currentEffects) };
        t.Look.Name = name;
        string? source = null;
        if (includeWallpaper)
        {
            var (path, style) = _wallpaper.Current();
            if (path != null && File.Exists(path) && new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(Path.GetExtension(path).ToLowerInvariant()) && new FileInfo(path).Length <= ThemeLibrary.MaxWallpaperBytes)
            { source = path; t.WallpaperStyle = style; }
        }
        return (t, source);
    }

    private static EffectsSettings Clone(EffectsSettings e) => new()
    { ActiveBorder = e.ActiveBorder, BorderRgb = e.BorderRgb, Corners = e.Corners, DarkTitle = e.DarkTitle, Backdrop = e.Backdrop, AutoHideTaskbar = e.AutoHideTaskbar };
}
