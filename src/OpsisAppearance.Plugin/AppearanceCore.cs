using System.Drawing;
using System.Text.Json;
using Microsoft.Win32;

namespace OpsisAppearance;

/// <summary>Sunrise / sunset (NOAA algorithm, good to about a minute) - drives the automatic Light/Dark switch.</summary>
public static class Solar
{
    /// <summary>Local sunrise and sunset for a date. Returns null at polar day/night (the sun never rises or never sets).</summary>
    public static (DateTime Sunrise, DateTime Sunset)? Compute(DateTime localDate, double latitude, double longitude, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        int dayOfYear = localDate.DayOfYear;
        double gamma = 2 * Math.PI / 365 * (dayOfYear - 1);
        double eqTime = 229.18 * (0.000075 + 0.001868 * Math.Cos(gamma) - 0.032077 * Math.Sin(gamma)
                                  - 0.014615 * Math.Cos(2 * gamma) - 0.040849 * Math.Sin(2 * gamma));
        double decl = 0.006918 - 0.399912 * Math.Cos(gamma) + 0.070257 * Math.Sin(gamma)
                      - 0.006758 * Math.Cos(2 * gamma) + 0.000907 * Math.Sin(2 * gamma)
                      - 0.002697 * Math.Cos(3 * gamma) + 0.00148 * Math.Sin(3 * gamma);
        double lat = latitude * Math.PI / 180;
        double cosHa = Math.Cos(90.833 * Math.PI / 180) / (Math.Cos(lat) * Math.Cos(decl)) - Math.Tan(lat) * Math.Tan(decl);
        if (cosHa is > 1 or < -1) return null;
        double ha = Math.Acos(cosHa) * 180 / Math.PI;
        double sunriseUtcMin = 720 - 4 * (longitude + ha) - eqTime;
        double sunsetUtcMin = 720 - 4 * (longitude - ha) - eqTime;
        var midnightUtc = new DateTime(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, DateTimeKind.Utc);
        DateTime ToLocal(double utcMin)
        {
            var utc = midnightUtc.AddMinutes(utcMin);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        }
        return (ToLocal(sunriseUtcMin), ToLocal(sunsetUtcMin));
    }
}

/// <summary>Where a registry value lives. An interface so the engine can be unit-tested without touching the real registry.</summary>
public interface IRegistryStore
{
    object? Get(string subKey, string name);
    void Set(string subKey, string name, object value, RegistryValueKind kind);
    void Delete(string subKey, string name);
}

public sealed class CurrentUserRegistry : IRegistryStore
{
    public object? Get(string subKey, string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(subKey);
        return k?.GetValue(name);
    }
    public void Set(string subKey, string name, object value, RegistryValueKind kind)
    {
        using var k = Registry.CurrentUser.CreateSubKey(subKey);
        k.SetValue(name, value, kind);
    }
    public void Delete(string subKey, string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
        k?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public enum ThemeMode { Manual, AutoSun, AutoFixed }

/// <summary>A complete "look": everything the plugin can change. A null member means "leave as it is".</summary>
public sealed class Look
{
    public string Name { get; set; } = "";
    public bool? Dark { get; set; }
    public int? AccentRgb { get; set; }          // 0xRRGGBB
    public bool? Transparency { get; set; }
    public bool? AccentOnSurfaces { get; set; }  // accent colour on Start / taskbar / title bars
    public bool? TaskbarCentered { get; set; }
    public bool? ShowTaskView { get; set; }
    public bool? ShowWidgets { get; set; }
    public bool? ShowSearch { get; set; }
    // Εξερευνητής / shell (Seelen-style ρυθμίσεις συμπεριφοράς της εμφάνισης)
    public bool? ShowFileExtensions { get; set; }
    public bool? ShowHiddenFiles { get; set; }
    public bool? ClockSeconds { get; set; }       // δευτερόλεπτα στο ρολόι του taskbar
    public int? TaskbarCombine { get; set; }      // 0 = πάντα συνδυασμός, 1 = όταν γεμίσει, 2 = ποτέ (με ετικέτες)
    public bool? Animations { get; set; }         // animation ελαχιστοποίησης/μεγιστοποίησης παραθύρων
}

public sealed class AppearanceSettings
{
    public ThemeMode Mode { get; set; } = ThemeMode.Manual;
    public double Latitude { get; set; } = 37.9838;     // Athens, like the Opsis weather defaults
    public double Longitude { get; set; } = 23.7275;
    public string LightAt { get; set; } = "07:00";
    public string DarkAt { get; set; } = "19:00";
    public int SunOffsetMinutes { get; set; } = 0;      // + later, - earlier
    public EffectsSettings Effects { get; set; } = new();     // εφέ παραθύρων (βλ. WindowEffects.cs)
    public SlideshowSettings Slideshow { get; set; } = new(); // wallpaper slideshow (βλ. Slideshow.cs)
    public TaskbarLookSettings TaskbarLook { get; set; } = new();    // TranslucentTB-style (βλ. TaskbarLook.cs)
    public TaskbarShapeSettings TaskbarShape { get; set; } = new();  // RoundedTB-style
    public SafetySettings Safety { get; set; } = new();              // σημείο επαναφοράς / αντίγραφα ασφαλείας (βλ. Safety.cs)
}

/// <summary>Reads/writes the Windows appearance values. Everything is HKCU and reversible (see <see cref="Snapshot"/>).</summary>
public sealed class AppearanceEngine
{
    public const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    public const string Search = @"Software\Microsoft\Windows\CurrentVersion\Search";
    public const string Dwm = @"Software\Microsoft\Windows\DWM";
    public const string Accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    public const string Desktop = @"Control Panel\Desktop";
    public const string WindowMetrics = @"Control Panel\Desktop\WindowMetrics";

    private readonly IRegistryStore _reg;
    private readonly Action _broadcast;

    public AppearanceEngine(IRegistryStore reg, Action? broadcast = null)
    {
        _reg = reg;
        _broadcast = broadcast ?? Broadcaster.SettingsChanged;
    }

    // ---- reading ---------------------------------------------------------------------------------------------

    private bool Flag(string key, string name, bool whenMissing) => _reg.Get(key, name) is int v ? v != 0 : whenMissing;

    public bool IsDark => !Flag(Personalize, "AppsUseLightTheme", true);

    public Look ReadCurrent() => new()
    {
        Name = "Current",
        Dark = IsDark,
        Transparency = Flag(Personalize, "EnableTransparency", true),
        AccentOnSurfaces = Flag(Personalize, "ColorPrevalence", false),
        TaskbarCentered = _reg.Get(Advanced, "TaskbarAl") is not int al || al == 1,
        ShowTaskView = Flag(Advanced, "ShowTaskViewButton", true),
        ShowWidgets = Flag(Advanced, "TaskbarDa", true),
        ShowSearch = !(_reg.Get(Search, "SearchboxTaskbarMode") is int sm && sm == 0),
        ShowFileExtensions = !Flag(Advanced, "HideFileExt", true),
        ShowHiddenFiles = _reg.Get(Advanced, "Hidden") is int hid && hid == 1,
        ClockSeconds = Flag(Advanced, "ShowSecondsInSystemClock", false),
        TaskbarCombine = _reg.Get(Advanced, "TaskbarGlomLevel") is int glom ? Math.Clamp(glom, 0, 2) : 0,
        Animations = !(_reg.Get(WindowMetrics, "MinAnimate") is string ma && ma == "0"),
        AccentRgb = ReadAccent(),
    };

    public int? ReadAccent()
    {
        // DWM AccentColor is stored as 0xAABBGGRR.
        if (_reg.Get(Dwm, "AccentColor") is int abgr) return ((abgr & 0xFF) << 16) | (abgr & 0xFF00) | ((abgr >> 16) & 0xFF);
        return null;
    }

    // ---- writing ---------------------------------------------------------------------------------------------

    /// <summary>The values this engine can touch: used to back them up before the first change.</summary>
    private static readonly (string Key, string Name)[] Touched =
    {
        (Personalize, "AppsUseLightTheme"), (Personalize, "SystemUsesLightTheme"), (Personalize, "EnableTransparency"),
        (Personalize, "ColorPrevalence"), (Advanced, "TaskbarAl"), (Advanced, "ShowTaskViewButton"), (Advanced, "TaskbarDa"),
        (Search, "SearchboxTaskbarMode"), (Dwm, "AccentColor"), (Dwm, "ColorizationColor"), (Dwm, "AccentColorInactive"),
        (Accent, "AccentColorMenu"), (Accent, "StartColorMenu"), (Accent, "AccentPalette"), (Desktop, "AutoColorization"),
        (Advanced, "HideFileExt"), (Advanced, "Hidden"), (Advanced, "ShowSecondsInSystemClock"), (Advanced, "TaskbarGlomLevel"), (WindowMetrics, "MinAnimate"),
    };

    public sealed class SnapshotEntry { public string Key { get; set; } = ""; public string Name { get; set; } = ""; public string? Kind { get; set; } public string? Text { get; set; } }

    /// <summary>Captures every value this engine can change. Missing values are recorded as missing and deleted again on restore.</summary>
    public List<SnapshotEntry> Snapshot()
    {
        var list = new List<SnapshotEntry>();
        foreach (var (key, name) in Touched)
        {
            var v = _reg.Get(key, name);
            list.Add(v switch
            {
                null => new SnapshotEntry { Key = key, Name = name },
                int i => new SnapshotEntry { Key = key, Name = name, Kind = "DWord", Text = i.ToString() },
                byte[] b => new SnapshotEntry { Key = key, Name = name, Kind = "Binary", Text = Convert.ToHexString(b) },
                _ => new SnapshotEntry { Key = key, Name = name, Kind = "String", Text = v.ToString() },
            });
        }
        return list;
    }

    public void Restore(IEnumerable<SnapshotEntry> snapshot)
    {
        foreach (var e in snapshot)
        {
            try
            {
                switch (e.Kind)
                {
                    case null: _reg.Delete(e.Key, e.Name); break;
                    case "DWord": _reg.Set(e.Key, e.Name, int.Parse(e.Text!), RegistryValueKind.DWord); break;
                    case "Binary": _reg.Set(e.Key, e.Name, Convert.FromHexString(e.Text!), RegistryValueKind.Binary); break;
                    default: _reg.Set(e.Key, e.Name, e.Text ?? "", RegistryValueKind.String); break;
                }
            }
            catch (Exception) { /* a value we cannot restore must not stop the others */ }
        }
        _broadcast();
    }

    private void SetFlag(string key, string name, bool value) => _reg.Set(key, name, value ? 1 : 0, RegistryValueKind.DWord);

    public void SetDark(bool dark)
    {
        SetFlag(Personalize, "AppsUseLightTheme", !dark);
        SetFlag(Personalize, "SystemUsesLightTheme", !dark);
    }

    /// <summary>Applies a look. Null members are left untouched. Broadcasts the change once at the end so Windows redraws live.</summary>
    public void Apply(Look look)
    {
        if (look.Dark is bool d) SetDark(d);
        if (look.Transparency is bool t) SetFlag(Personalize, "EnableTransparency", t);
        if (look.AccentOnSurfaces is bool a) SetFlag(Personalize, "ColorPrevalence", a);
        if (look.AccentRgb is int rgb) SetAccent(rgb);
        if (look.TaskbarCentered is bool c) _reg.Set(Advanced, "TaskbarAl", c ? 1 : 0, RegistryValueKind.DWord);
        if (look.ShowTaskView is bool tv) SetFlag(Advanced, "ShowTaskViewButton", tv);
        if (look.ShowWidgets is bool w) SetFlag(Advanced, "TaskbarDa", w);
        if (look.ShowSearch is bool s) _reg.Set(Search, "SearchboxTaskbarMode", s ? 2 : 0, RegistryValueKind.DWord);
        if (look.ShowFileExtensions is bool fe) SetFlag(Advanced, "HideFileExt", !fe);
        if (look.ShowHiddenFiles is bool hf) _reg.Set(Advanced, "Hidden", hf ? 1 : 2, RegistryValueKind.DWord);
        if (look.ClockSeconds is bool cs) SetFlag(Advanced, "ShowSecondsInSystemClock", cs);
        if (look.TaskbarCombine is int tc) _reg.Set(Advanced, "TaskbarGlomLevel", Math.Clamp(tc, 0, 2), RegistryValueKind.DWord);
        if (look.Animations is bool an) _reg.Set(WindowMetrics, "MinAnimate", an ? "1" : "0", RegistryValueKind.String);
        _broadcast();
    }

    /// <summary>Sets the system accent colour the way Settings does (DWM + Explorer\Accent + the 8-colour palette).</summary>
    public void SetAccent(int rgb)
    {
        int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        int abgr = unchecked((int)0xFF000000) | (b << 16) | (g << 8) | r;     // AccentColor / AccentColorMenu / StartColorMenu: AABBGGRR
        int argb = unchecked((int)0xC4000000) | (r << 16) | (g << 8) | b;     // ColorizationColor: AARRGGBB
        _reg.Set(Dwm, "AccentColor", abgr, RegistryValueKind.DWord);
        _reg.Set(Dwm, "ColorizationColor", argb, RegistryValueKind.DWord);
        _reg.Set(Dwm, "AccentColorInactive", unchecked((int)0xFF000000) | (0x40 << 16) | (0x40 << 8) | 0x40, RegistryValueKind.DWord);
        _reg.Set(Accent, "AccentColorMenu", abgr, RegistryValueKind.DWord);
        _reg.Set(Accent, "StartColorMenu", Shade(rgb, -0.25), RegistryValueKind.DWord);
        _reg.Set(Accent, "AccentPalette", Palette(rgb), RegistryValueKind.Binary);
        _reg.Set(Desktop, "AutoColorization", "0", RegistryValueKind.String);   // do not let Windows re-pick from the wallpaper
    }

    // ---- colour maths ----------------------------------------------------------------------------------------

    /// <summary>The 8 x RGBA accent palette Windows keeps: 3 lighter shades, the accent, 3 darker shades, one neutral.</summary>
    public static byte[] Palette(int rgb)
    {
        var shades = new[] { 0.60, 0.40, 0.20, 0.0, -0.20, -0.40, -0.60 };
        var bytes = new List<byte>();
        foreach (var amount in shades)
        {
            int c = amount == 0 ? rgb : ShadeRgb(rgb, amount);
            bytes.Add((byte)((c >> 16) & 0xFF)); bytes.Add((byte)((c >> 8) & 0xFF)); bytes.Add((byte)(c & 0xFF)); bytes.Add(0);
        }
        bytes.AddRange(new byte[] { 0x88, 0x17, 0x98, 0x00 });
        return bytes.ToArray();
    }

    /// <summary>Lightens (amount > 0) or darkens (amount &lt; 0) an 0xRRGGBB colour by mixing with white or black.</summary>
    public static int ShadeRgb(int rgb, double amount)
    {
        int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        double target = amount >= 0 ? 255 : 0, k = Math.Abs(amount);
        int Mix(int v) => (int)Math.Round(v + (target - v) * k);
        return (Mix(r) << 16) | (Mix(g) << 8) | Mix(b);
    }

    private static int Shade(int rgb, double amount)
    {
        int c = ShadeRgb(rgb, amount);
        int r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
        return unchecked((int)0xFF000000) | (b << 16) | (g << 8) | r;
    }

    /// <summary>Dominant, pleasant accent of an image: the most "colourful" hue bucket, brightened if it is too dark to read.</summary>
    public static int? DominantAccent(Bitmap source)
    {
        const int size = 48;
        using var small = new Bitmap(size, size);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(source, 0, 0, size, size);
        }
        var weight = new double[12];
        var rs = new double[12]; var gs = new double[12]; var bs = new double[12];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = small.GetPixel(x, y);
                float sat = c.GetSaturation(), light = c.GetBrightness();
                if (sat < 0.25f || light < 0.12f || light > 0.92f) continue;   // ignore greys, near-black and near-white
                int bucket = Math.Min(11, (int)(c.GetHue() / 30f));
                double w = sat * (1 - Math.Abs(light - 0.5) * 1.2);
                weight[bucket] += w; rs[bucket] += c.R * w; gs[bucket] += c.G * w; bs[bucket] += c.B * w;
            }
        int best = Array.IndexOf(weight, weight.Max());
        if (weight[best] <= 0) return null;
        int r = (int)(rs[best] / weight[best]), gr = (int)(gs[best] / weight[best]), b = (int)(bs[best] / weight[best]);
        var avg = Color.FromArgb(r, gr, b);
        if (avg.GetBrightness() < 0.35f) { int lifted = ShadeRgb((r << 16) | (gr << 8) | b, 0.25); return lifted; }
        return (r << 16) | (gr << 8) | b;
    }

    public int? WallpaperAccent()
    {
        try
        {
            if (_reg.Get(Desktop, "WallPaper") is not string path || !File.Exists(path)) return null;
            using var bmp = new Bitmap(path);
            return DominantAccent(bmp);
        }
        catch (Exception) { return null; }
    }

    /// <summary>Whether it is "night" now for the chosen schedule.</summary>
    public static bool ShouldBeDark(AppearanceSettings s, DateTime now, TimeZoneInfo? zone = null)
    {
        TimeSpan lightAt, darkAt;
        if (s.Mode == ThemeMode.AutoSun && Solar.Compute(now.Date, s.Latitude, s.Longitude, zone) is { } sun)
        {
            lightAt = sun.Sunrise.TimeOfDay + TimeSpan.FromMinutes(s.SunOffsetMinutes);
            darkAt = sun.Sunset.TimeOfDay + TimeSpan.FromMinutes(s.SunOffsetMinutes);
        }
        else
        {
            TimeSpan.TryParse(s.LightAt, out lightAt); if (lightAt == default) lightAt = TimeSpan.FromHours(7);
            TimeSpan.TryParse(s.DarkAt, out darkAt); if (darkAt == default) darkAt = TimeSpan.FromHours(19);
        }
        var t = now.TimeOfDay;
        return lightAt <= darkAt ? !(t >= lightAt && t < darkAt) : (t >= darkAt && t < lightAt);
    }

    // ---- built-in looks --------------------------------------------------------------------------------------

    public static List<Look> BuiltInLooks() => new()
    {
        new Look { Name = "Day", Dark = false, Transparency = true, AccentRgb = 0x0078D4 },
        new Look { Name = "Night", Dark = true, Transparency = true, AccentRgb = 0x8764B8 },
        new Look { Name = "Focus", Dark = true, Transparency = false, AccentOnSurfaces = false, ShowTaskView = false, ShowWidgets = false, ShowSearch = false },
        new Look { Name = "Classic taskbar", TaskbarCentered = false, ShowTaskView = true, ShowSearch = true },
    };
}

public static class Broadcaster
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

    /// <summary>Tells every window the theme/colours changed so apps and Explorer redraw immediately.</summary>
    public static void SettingsChanged()
    {
        const uint WM_SETTINGCHANGE = 0x1A, SMTO_ABORTIFHUNG = 0x2;
        var hwndBroadcast = new IntPtr(0xFFFF);
        foreach (var area in new[] { "ImmersiveColorSet", "WindowsThemeElement", "TraySettings" })
            SendMessageTimeout(hwndBroadcast, WM_SETTINGCHANGE, IntPtr.Zero, area, SMTO_ABORTIFHUNG, 1500, out _);
    }
}

/// <summary>Plugin data on disk: settings + the one-time backup of the original values.</summary>
public sealed class AppearanceStore
{
    private static string Folder
    {
        get
        {
            string? dir = Environment.GetEnvironmentVariable("OPSIS_APPDATA");
            if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opsis");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static string SettingsPath => Path.Combine(Folder, "appearance.json");
    private static string BackupPath => Path.Combine(Folder, "appearance-original.json");

    public AppearanceSettings Load()
    {
        try { if (File.Exists(SettingsPath)) return JsonSerializer.Deserialize<AppearanceSettings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch (Exception) { }
        return new();
    }

    public void Save(AppearanceSettings s)
    {
        try
        {
            string tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
            if (File.Exists(SettingsPath)) File.Replace(tmp, SettingsPath, null); else File.Move(tmp, SettingsPath);
        }
        catch (Exception) { }
    }

    public bool HasBackup => File.Exists(BackupPath);

    /// <summary>Saved ONCE (the first time the plugin changes anything) so the user can always return to what they had.</summary>
    public void SaveBackupOnce(List<AppearanceEngine.SnapshotEntry> snapshot)
    {
        if (HasBackup) return;
        try { File.WriteAllText(BackupPath, JsonSerializer.Serialize(snapshot, Json)); } catch (Exception) { }
    }

    public List<AppearanceEngine.SnapshotEntry>? LoadBackup()
    {
        try { return HasBackup ? JsonSerializer.Deserialize<List<AppearanceEngine.SnapshotEntry>>(File.ReadAllText(BackupPath)) : null; }
        catch (Exception) { return null; }
    }
}
