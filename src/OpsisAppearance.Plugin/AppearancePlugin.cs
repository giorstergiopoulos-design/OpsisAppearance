using OpsisNewsCrawler.Plugin.Abstractions;

namespace OpsisAppearance;

/// <summary>
/// "Opsis Appearance" - changes the look of the Windows environment itself: automatic Light/Dark at sunrise/sunset,
/// accent colour (also from the wallpaper), taskbar alignment and buttons, transparency, one-click looks and a full
/// restore of the user's original values. Everything is per-user (HKCU) and reversible.
/// Low-end friendly: no threads, no polling unless an automatic schedule is on (then ONE 60 s timer).
/// </summary>
public sealed class OpsisAppearancePlugin : IOpsisNewsCrawlerPlugin, IPluginProfileAware
{
    public const string NavigationKey = "OpsisAppearance";

    private readonly AppearanceStore _store = new();
    private readonly AppearanceEngine _engine;
    private AppearanceSettings _settings = new();
    private System.Windows.Forms.Timer? _timer;
    private bool _greek = true;
    private Palette _pal = Palette.Dark;
    private AppearancePage? _page;

    private readonly WindowEffectsEngine _effects;
    private ThemeLibrary _themes = new(ThemeLibrary.DefaultRoot);
    private ThemeApplier _applier = null!;
    private WinEventWatcher? _watcher;
    private readonly IWallpaperApi _wallpaperApi;
    private System.Windows.Forms.Timer? _slideTimer;

    public OpsisAppearancePlugin() : this(new CurrentUserRegistry()) { }
    internal OpsisAppearancePlugin(IRegistryStore registry, IDwmWindows? dwm = null, ITaskbarControl? taskbar = null, IWallpaperApi? wallpaper = null, string? themesRoot = null)
    {
        if (themesRoot != null) _themes = new ThemeLibrary(themesRoot);
        _wallpaperApi = wallpaper ?? new RealWallpaper();
        _engine = new AppearanceEngine(registry);
        _effects = new WindowEffectsEngine(dwm ?? new RealDwmWindows(), taskbar ?? new RealTaskbar(), () => _engine.IsDark);
        _applier = new ThemeApplier(_engine, fx => { _settings.Effects = fx; ApplyEffects(); }, wallpaper ?? new RealWallpaper(), _themes);
    }

    public event Action<string>? NavigationRequested;

    public string Id => "giorstergiopoulos.opsisappearance";
    public string Name => "Opsis Appearance";
    public string Version => "1.3.0";

    public void Start()
    {
        _settings = _store.Load();
        ApplySchedule(force: true);
        UpdateTimer();
        UpdateSlideTimer();
        ApplyEffects();
    }

    public void Stop()
    {
        _timer?.Stop(); _timer?.Dispose(); _timer = null;
        _slideTimer?.Stop(); _slideTimer?.Dispose(); _slideTimer = null;
        _watcher?.Dispose(); _watcher = null;
        try { _effects.RestoreAll(); } catch (Exception) { }     // τα εφέ ζουν μόνο όσο τρέχει το plugin: επαναφορά πλήρης
    }
    public void Pause() { _timer?.Stop(); _slideTimer?.Stop(); }
    public void Resume() { UpdateTimer(); UpdateSlideTimer(); ApplySchedule(force: false); }
    public void Refresh() { _settings = _store.Load(); ApplySchedule(force: true); UpdateTimer(); UpdateSlideTimer(); _page?.RefreshAll(); }
    public void SetMode(string mode) { }
    public void SetPosition(string position) { }
    public void SetHardwareProfile(string tier) { }

    public void SetLanguage(string code) { _greek = code == "el"; _page?.ApplyLanguage(_greek); }

    public void SetTheme(bool dark, int background, int surface, int border, int text, int textMuted, int accent, double opacity)
    {
        static Color Opaque(int argb) => Color.FromArgb(255, Color.FromArgb(argb));
        _pal = new Palette(Opaque(background), Opaque(surface), Opaque(border), Opaque(text), Opaque(textMuted), Opaque(accent));
        _page?.ApplyPalette(_pal);
    }

    public PluginAboutInfo GetAboutInfo() => new(
        Name, Version,
        "Αλλάζει την εμφάνιση των ίδιων των Windows: αυτόματο Light/Dark με ανατολή/δύση, χρώμα accent (και από το wallpaper), taskbar, διαφάνειες, έτοιμες εμφανίσεις και πλήρης επαναφορά.",
        "Changes the look of Windows itself: automatic Light/Dark at sunrise/sunset, accent colour (also from the wallpaper), taskbar, transparency, one-click looks and full restore.",
        "Opsis Team", "MIT",
        new[]
        {
            new PluginAboutSection("Ασφάλεια", "Safety",
                "Αλλάζει μόνο ρυθμίσεις του τρέχοντος χρήστη (HKCU). Την πρώτη αλλαγή αποθηκεύει αντίγραφο των αρχικών τιμών· το κουμπί «Επαναφορά» τις γυρνά πίσω.\n" +
                "Only per-user (HKCU) settings. The first change saves a copy of your original values; Restore puts them back."),
            new PluginAboutSection("Εφέ παραθύρων", "Window effects",
                "Χρωματιστό border ενεργού παραθύρου, γωνίες, σκούρες γραμμές τίτλου, Mica/Acrylic backdrop, αυτόματη απόκρυψη taskbar. Ισχύουν όσο τρέχει το Opsis και επαναφέρονται πλήρως όταν σταματήσει το plugin.\n" +
                "Coloured active-window border, corners, dark title bars, Mica/Acrylic backdrop, taskbar auto-hide. They apply while Opsis runs and are fully restored when the plugin stops."),
            new PluginAboutSection("Αυτόματο θέμα", "Automatic theme",
                "Υπολογίζει ανατολή/δύση από γεωγραφικό πλάτος/μήκος (προεπιλογή Αθήνα) ή χρησιμοποιεί σταθερές ώρες. Ένας μόνο timer ανά λεπτό, μόνο όταν είναι ενεργό.\n" +
                "Sunrise/sunset from latitude/longitude (Athens by default) or fixed times. One 60 s timer, only while automatic mode is on.")
        });

    public IReadOnlyList<PluginMenuItem> GetTrayMenuItems() => new[]
    {
        new PluginMenuItem("appearance.toggle", "Εναλλαγή Light/Dark", "Toggle Light/Dark", () => { try { Backup(); _engine.SetDark(!_engine.IsDark); Broadcaster.SettingsChanged(); _page?.RefreshAll(); } catch (Exception) { } }),
        new PluginMenuItem("appearance.open", "Εμφάνιση Windows…", "Windows appearance…", () => NavigationRequested?.Invoke(NavigationKey)),
    };

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() => new[]
    {
        new PluginNavigationItem(NavigationKey, "Εμφάνιση Windows", "Windows appearance", CreatePage, Order: 310)
    };

    private Control CreatePage()
    {
        _page = new AppearancePage(this, _pal, _greek);
        return _page;
    }

    // ---- shared by the page ------------------------------------------------------------------------------------

    internal AppearanceEngine Engine => _engine;
    internal WindowEffectsEngine Effects => _effects;
    internal ThemeLibrary Themes => _themes;
    internal ThemeApplier Applier => _applier;

    /// <summary>Εφαρμόζει ένα θέμα (πρώτα αντίγραφο ασφαλείας των αρχικών τιμών) και αποθηκεύει τα εφέ στις ρυθμίσεις.</summary>
    internal ThemeApplier.Report ApplyTheme(OpsisTheme theme)
    {
        Backup();
        var report = _applier.Apply(theme);
        if (theme.Effects != null) _store.Save(_settings);
        _page?.RefreshAll();
        return report;
    }

    /// <summary>Εφαρμόζει τα εφέ παραθύρων από τις ρυθμίσεις. Το hook ζει ΜΟΝΟ όσο υπάρχει ενεργό εφέ παραθύρου (καμία κατανάλωση αλλιώς).</summary>
    internal void ApplyEffects()
    {
        try
        {
            _effects.AccentRgb = _engine.ReadAccent() ?? 0x0078D4;
            _effects.Apply(_settings.Effects);
            bool need = _settings.Effects.AnyWindowEffect;
            if (need && _watcher == null) _watcher = new WinEventWatcher(h => _effects.OnForegroundChanged(h), h => _effects.ApplyToWindow(h));
            else if (!need && _watcher != null) { _watcher.Dispose(); _watcher = null; }
        }
        catch (Exception) { /* ένα εφέ που αποτυγχάνει δεν πρέπει να ρίξει το plugin ούτε το Opsis */ }
    }
    internal AppearanceStore Store => _store;
    internal AppearanceSettings Settings => _settings;

    /// <summary>Saves the user's original values once, before the plugin changes anything.</summary>
    internal void Backup()
    {
        if (!_store.HasBackup) _store.SaveBackupOnce(_engine.Snapshot());
    }

    internal void SaveSettings() { _store.Save(_settings); UpdateTimer(); UpdateSlideTimer(); ApplySchedule(force: true); }

    // ---- wallpaper slideshow -------------------------------------------------------------------------------------

    private void UpdateSlideTimer()
    {
        if (!_settings.Slideshow.Enabled) { _slideTimer?.Stop(); return; }
        if (_slideTimer == null)
        {
            _slideTimer = new System.Windows.Forms.Timer { Interval = 60_000 };      // one cheap check a minute, only while the slideshow is on
            _slideTimer.Tick += (_, _) => SlideshowTick(DateTime.UtcNow, force: false);
        }
        _slideTimer.Start();
        SlideshowTick(DateTime.UtcNow, force: false);
    }

    /// <summary>Αλλάζει το wallpaper αν πέρασε το διάστημα (ή πάντα με force). Επιστρέφει true αν άλλαξε.</summary>
    internal bool SlideshowTick(DateTime utcNow, bool force)
    {
        var s = _settings.Slideshow;
        try
        {
            if (!force && !WallpaperSlideshow.Due(s, utcNow)) return false;
            var images = WallpaperSlideshow.Images(s.Folder);
            var next = WallpaperSlideshow.Pick(images, s.LastFile, s.Shuffle);
            if (next == null) return false;
            if (s.OriginalPath == null)
            {
                var cur = _wallpaperApi.Current();
                s.OriginalPath = cur.Path ?? ""; s.OriginalStyle = cur.Style;
            }
            _wallpaperApi.Set(next, s.Style);
            s.LastFile = next; s.LastChangeUtc = utcNow;
            _store.Save(_settings);
            _page?.RefreshAll();
            return true;
        }
        catch (Exception) { return false; }     // a missing/locked picture must not stop the plugin; the next tick tries again
    }

    /// <summary>Σταματά το slideshow και γυρνά το wallpaper που είχες πριν ξεκινήσει.</summary>
    internal void StopSlideshow(bool restoreWallpaper)
    {
        var s = _settings.Slideshow;
        s.Enabled = false;
        try { if (restoreWallpaper && !string.IsNullOrEmpty(s.OriginalPath) && File.Exists(s.OriginalPath)) _wallpaperApi.Set(s.OriginalPath, s.OriginalStyle); } catch (Exception) { }
        s.OriginalPath = null;
        SaveSettings();
    }

    internal void RestoreOriginal()
    {
        var backup = _store.LoadBackup();
        if (backup != null) _engine.Restore(backup);   // Restore skips values it cannot write instead of throwing
    }

    internal void ApplySchedule(bool force)
    {
        if (_settings.Mode == ThemeMode.Manual) return;
        try
        {
            bool wantDark = AppearanceEngine.ShouldBeDark(_settings, DateTime.Now);
            if (wantDark == _engine.IsDark) return;
            Backup();
            _engine.SetDark(wantDark);
            Broadcaster.SettingsChanged();
            _page?.RefreshAll();
        }
        catch (Exception)
        {
            // A locked-down PC (policy, no registry write access) must never make the host drop the plugin: the page shows the
            // current state and the manual buttons report the error to the user.
        }
    }

    private void UpdateTimer()
    {
        bool need = _settings.Mode != ThemeMode.Manual;
        if (!need) { _timer?.Stop(); return; }
        if (_timer == null)
        {
            _timer = new System.Windows.Forms.Timer { Interval = 60_000 };
            _timer.Tick += (_, _) => ApplySchedule(force: false);
        }
        _timer.Start();
    }
}

internal sealed record Palette(Color Bg, Color Surface, Color Border, Color Text, Color Muted, Color Accent)
{
    public static readonly Palette Dark = new(Color.FromArgb(18, 22, 32), Color.FromArgb(28, 34, 48), Color.FromArgb(52, 62, 84),
        Color.FromArgb(235, 240, 250), Color.FromArgb(150, 160, 180), Color.FromArgb(0, 200, 230));
}

internal sealed class AppearancePage : UserControl
{
    private readonly OpsisAppearancePlugin _p;
    private bool _greek;
    private Palette _pal;
    private bool _loading;

    private readonly FlowLayoutPanel _flow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16, 12, 16, 12) };
    private readonly List<Label> _headings = new(), _texts = new();
    private readonly List<Button> _buttons = new();
    private readonly List<CheckBox> _checks = new();
    private readonly List<RadioButton> _radios = new();

    private readonly Label _status = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11f), MaximumSize = new Size(760, 0) };
    private readonly RadioButton _manual = new() { AutoSize = true }, _autoSun = new() { AutoSize = true }, _autoFixed = new() { AutoSize = true };
    private readonly Button _lightNow = new(), _darkNow = new();
    private readonly TextBox _lightAt = new() { Width = 64 }, _darkAt = new() { Width = 64 };
    private readonly NumericUpDown _lat = new() { DecimalPlaces = 4, Minimum = -90, Maximum = 90, Width = 90 }, _lon = new() { DecimalPlaces = 4, Minimum = -180, Maximum = 180, Width = 90 }, _offset = new() { Minimum = -180, Maximum = 180, Width = 64 };
    private readonly FlowLayoutPanel _swatches = new() { AutoSize = true, MaximumSize = new Size(760, 0), WrapContents = true };
    private readonly Button _pick = new(), _fromWall = new();
    private readonly CheckBox _transp = new(), _accentSurf = new(), _centered = new(), _search = new(), _taskView = new(), _widgets = new();
    private readonly ListBox _themeList = new() { IntegralHeight = false, Height = 190, Width = 440, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 38 };
    private readonly Label _themeInfo = new() { AutoSize = true, MaximumSize = new Size(760, 0) };
    private readonly Button _themeApply = new(), _themeSave = new(), _themeDelete = new(), _themeImport = new(), _themeExport = new(), _themeFolder = new(), _winThemeApply = new(), _restore = new();
    private readonly ComboBox _winThemes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly Label _hWinThemes = new(), _tWinThemes = new();
    private List<OpsisTheme> _themeItems = new();
    private List<(string Name, string Path)> _winThemeItems = new();
    private readonly Label _hSchedule = new(), _hAccent = new(), _hTaskbar = new(), _hLooks = new(), _hEffects = new();   // _hLooks = "Theme manager" heading
    private readonly CheckBox _borderCheck = new(), _hideTaskbar = new();
    private readonly Button _borderColor = new();
    private readonly ComboBox _corners = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 }, _titleBars = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 }, _backdrop = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly Label _tCorners = new(), _tTitle = new(), _tBackdrop = new();
    private readonly Label _hShell = new(), _hSlide = new(), _tCombine = new(), _tSlideEvery = new(), _tSlideStyle = new(), _slideInfo = new() { AutoSize = true, MaximumSize = new Size(760, 0) }, _tShellNote = new();
    private readonly CheckBox _fileExt = new(), _hiddenFiles = new(), _clockSecs = new(), _animations = new(), _slideOn = new(), _slideShuffle = new();
    private readonly ComboBox _combine = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 }, _slideStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly NumericUpDown _slideMin = new() { Minimum = 1, Maximum = 1440, Width = 70, Value = 30 };
    private readonly Button _slideFolder = new(), _slideNext = new(), _restartExplorer = new();
    private readonly Label _tLight = new(), _tDark = new(), _tLat = new(), _tLon = new(), _tOff = new(), _tSafety = new();

    private static readonly int[] SwatchColors =
    {
        0x0078D4, 0x0099BC, 0x00B294, 0x10893E, 0x7A7574, 0xFF8C00, 0xF7630C, 0xE74856, 0xE3008C, 0x8764B8, 0x744DA9, 0x6B69D6,
    };

    public AppearancePage(OpsisAppearancePlugin plugin, Palette pal, bool greek)
    {
        _p = plugin; _pal = pal; _greek = greek;
        DoubleBuffered = true; Dock = DockStyle.Fill;
        Controls.Add(_flow);
        _headings.AddRange(new[] { _hSchedule, _hAccent, _hTaskbar, _hLooks, _hEffects, _hWinThemes, _hShell, _hSlide });
        _texts.AddRange(new[] { _tLight, _tDark, _tLat, _tLon, _tOff, _tSafety, _tCorners, _tTitle, _tBackdrop, _tCombine, _tSlideEvery, _tSlideStyle, _slideInfo, _tShellNote });
        _radios.AddRange(new[] { _manual, _autoSun, _autoFixed });
        _checks.AddRange(new[] { _transp, _accentSurf, _centered, _search, _taskView, _widgets, _borderCheck, _hideTaskbar, _fileExt, _hiddenFiles, _clockSecs, _animations, _slideOn, _slideShuffle });
        _buttons.AddRange(new[] { _slideFolder, _slideNext, _restartExplorer, _lightNow, _darkNow, _pick, _fromWall, _restore, _borderColor, _themeApply, _themeSave, _themeDelete, _themeImport, _themeExport, _themeFolder, _winThemeApply });
        foreach (var h in _headings) { h.AutoSize = true; h.Font = new Font("Segoe UI Semibold", 12f); h.Margin = new Padding(0, 14, 0, 4); }
        foreach (var t in _texts) { t.AutoSize = true; t.Margin = new Padding(0, 6, 4, 0); }
        _tShellNote.MaximumSize = new Size(760, 0); _slideInfo.Margin = new Padding(0, 4, 0, 6);
        _tSafety.MaximumSize = new Size(760, 0); _tWinThemes.MaximumSize = new Size(760, 0); _themeInfo.Margin = new Padding(0, 4, 0, 6);

        _flow.Controls.Add(_status);
        _flow.Controls.Add(_hSchedule);
        _flow.Controls.Add(Row(_manual, _autoSun, _autoFixed));
        _flow.Controls.Add(Row(_lightNow, _darkNow));
        _flow.Controls.Add(Row(_tLight, _lightAt, _tDark, _darkAt));
        _flow.Controls.Add(Row(_tLat, _lat, _tLon, _lon, _tOff, _offset));
        _flow.Controls.Add(_hAccent);
        _flow.Controls.Add(_swatches);
        _flow.Controls.Add(Row(_pick, _fromWall));
        _flow.Controls.Add(_hTaskbar);
        _flow.Controls.Add(Row(_transp, _accentSurf));
        _flow.Controls.Add(Row(_centered, _search, _taskView, _widgets));
        _flow.Controls.Add(_hEffects);
        _flow.Controls.Add(Row(_borderCheck, _borderColor));
        _flow.Controls.Add(Row(_tCorners, _corners, _tTitle, _titleBars));
        _flow.Controls.Add(Row(_tBackdrop, _backdrop));
        _flow.Controls.Add(Row(_hideTaskbar));
        _flow.Controls.Add(_hShell);
        _flow.Controls.Add(Row(_fileExt, _hiddenFiles, _clockSecs, _animations));
        _flow.Controls.Add(Row(_tCombine, _combine, _restartExplorer));
        _flow.Controls.Add(_tShellNote);
        _flow.Controls.Add(_hSlide);
        _flow.Controls.Add(Row(_slideOn, _slideShuffle));
        _flow.Controls.Add(Row(_slideFolder, _tSlideEvery, _slideMin, _tSlideStyle, _slideStyle, _slideNext));
        _flow.Controls.Add(_slideInfo);
        _flow.Controls.Add(_hLooks);
        _flow.Controls.Add(_themeList);
        _flow.Controls.Add(_themeInfo);
        _flow.Controls.Add(Row(_themeApply, _themeSave, _themeDelete));
        _flow.Controls.Add(Row(_themeImport, _themeExport, _themeFolder));
        _flow.Controls.Add(_hWinThemes);
        _flow.Controls.Add(Row(_winThemes, _winThemeApply));
        _flow.Controls.Add(_tWinThemes);
        _flow.Controls.Add(Row(_restore));
        _flow.Controls.Add(_tSafety);

        foreach (int rgb in SwatchColors)
        {
            var b = new Button { Width = 34, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, Color.FromArgb(rgb)), Margin = new Padding(0, 0, 6, 6), Tag = rgb };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { AccentRgb = rgb }));
            _swatches.Controls.Add(b);
        }

        _manual.CheckedChanged += (_, _) => ModeChanged(ThemeMode.Manual, _manual);
        _autoSun.CheckedChanged += (_, _) => ModeChanged(ThemeMode.AutoSun, _autoSun);
        _autoFixed.CheckedChanged += (_, _) => ModeChanged(ThemeMode.AutoFixed, _autoFixed);
        _lightNow.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { Dark = false }));
        _darkNow.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { Dark = true }));
        _lightAt.Leave += (_, _) => SaveSchedule(); _darkAt.Leave += (_, _) => SaveSchedule();
        _lat.ValueChanged += (_, _) => SaveSchedule(); _lon.ValueChanged += (_, _) => SaveSchedule(); _offset.ValueChanged += (_, _) => SaveSchedule();
        _pick.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK) Do(() => _p.Engine.Apply(new Look { AccentRgb = (dlg.Color.R << 16) | (dlg.Color.G << 8) | dlg.Color.B }));
        };
        _fromWall.Click += (_, _) =>
        {
            int? c = _p.Engine.WallpaperAccent();
            if (c == null) { MessageBox.Show(this, _greek ? "Δεν βρέθηκε χρώμα στο wallpaper (συμπαγές χρώμα ή αρχείο που λείπει)." : "No usable colour found in the wallpaper (solid colour or missing file).", "Opsis Appearance"); return; }
            Do(() => _p.Engine.Apply(new Look { AccentRgb = c }));
        };
        _transp.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { Transparency = _transp.Checked }));
        _accentSurf.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { AccentOnSurfaces = _accentSurf.Checked }));
        _centered.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { TaskbarCentered = _centered.Checked }));
        _search.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { ShowSearch = _search.Checked }));
        _taskView.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { ShowTaskView = _taskView.Checked }));
        _widgets.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { ShowWidgets = _widgets.Checked }));
        _fileExt.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { ShowFileExtensions = _fileExt.Checked }));
        _hiddenFiles.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { ShowHiddenFiles = _hiddenFiles.Checked }));
        _clockSecs.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { ClockSeconds = _clockSecs.Checked }));
        _animations.Click += (_, _) => Do(() => _p.Engine.Apply(new Look { Animations = _animations.Checked }));
        _combine.SelectedIndexChanged += (_, _) => { if (!_loading) Do(() => _p.Engine.Apply(new Look { TaskbarCombine = _combine.SelectedIndex })); };
        _restartExplorer.Click += (_, _) =>
        {
            var ask = _greek ? "Ο Εξερευνητής των Windows θα κλείσει και θα ξανανοίξει (η γραμμή εργασιών θα εξαφανιστεί για λίγα δευτερόλεπτα). Συνέχεια;" : "Windows Explorer will close and start again (the taskbar disappears for a few seconds). Continue?";
            if (MessageBox.Show(this, ask, "Opsis Appearance", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { using var k = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskkill", "/f /im explorer.exe") { CreateNoWindow = true, UseShellExecute = false }); k?.WaitForExit(5000); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opsis Appearance"); }
        };
        _slideOn.Click += (_, _) =>
        {
            var sl = _p.Settings.Slideshow;
            if (_slideOn.Checked)
            {
                if (WallpaperSlideshow.Images(sl.Folder).Count == 0 && !PickSlideFolder()) { _slideOn.Checked = false; return; }
                sl.Enabled = true; sl.LastChangeUtc = null; _p.SaveSettings();
            }
            else _p.StopSlideshow(restoreWallpaper: true);
            RefreshAll();
        };
        _slideShuffle.Click += (_, _) => { _p.Settings.Slideshow.Shuffle = _slideShuffle.Checked; _p.SaveSettings(); };
        _slideMin.ValueChanged += (_, _) => { if (_loading) return; _p.Settings.Slideshow.IntervalMinutes = (int)_slideMin.Value; _p.SaveSettings(); };
        _slideStyle.SelectedIndexChanged += (_, _) => { if (_loading) return; _p.Settings.Slideshow.Style = (WallpaperStyle)_slideStyle.SelectedIndex; _p.SaveSettings(); };
        _slideFolder.Click += (_, _) => { if (PickSlideFolder()) RefreshAll(); };
        _slideNext.Click += (_, _) => { if (!_p.SlideshowTick(DateTime.UtcNow, force: true)) MessageBox.Show(this, _greek ? "Δεν βρέθηκαν εικόνες (png, jpg, bmp) στον φάκελο." : "No pictures (png, jpg, bmp) found in the folder.", "Opsis Appearance"); RefreshAll(); };
        _borderCheck.Click += (_, _) => ChangeEffects(e => e.ActiveBorder = _borderCheck.Checked);
        _borderColor.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK) ChangeEffects(e => { e.BorderRgb = (dlg.Color.R << 16) | (dlg.Color.G << 8) | dlg.Color.B; e.ActiveBorder = true; });
        };
        _hideTaskbar.Click += (_, _) => ChangeEffects(e => e.AutoHideTaskbar = _hideTaskbar.Checked);
        _corners.SelectedIndexChanged += (_, _) => { if (!_loading) ChangeEffects(e => e.Corners = (CornerStyle)_corners.SelectedIndex); };
        _titleBars.SelectedIndexChanged += (_, _) => { if (!_loading) ChangeEffects(e => e.DarkTitle = (DarkTitleBars)_titleBars.SelectedIndex); };
        _backdrop.SelectedIndexChanged += (_, _) => { if (!_loading) ChangeEffects(e => e.Backdrop = (BackdropStyle)_backdrop.SelectedIndex); };
        _themeList.DrawItem += DrawThemeItem;
        _themeList.SelectedIndexChanged += (_, _) => ShowThemeInfo();
        _themeList.DoubleClick += (_, _) => ApplySelectedTheme();
        _themeApply.Click += (_, _) => ApplySelectedTheme();
        _themeSave.Click += (_, _) => SaveCurrentAsTheme();
        _themeDelete.Click += (_, _) => DeleteSelectedTheme();
        _themeImport.Click += (_, _) => ImportTheme();
        _themeExport.Click += (_, _) => ExportSelectedTheme();
        _themeFolder.Click += (_, _) => { try { Directory.CreateDirectory(_p.Themes.Root); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_p.Themes.Root) { UseShellExecute = true }); } catch (Exception) { } };
        _winThemeApply.Click += (_, _) =>
        {
            if (_winThemes.SelectedIndex < 0 || _winThemes.SelectedIndex >= _winThemeItems.Count) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_winThemeItems[_winThemes.SelectedIndex].Path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opsis Appearance"); }
        };
        _restore.Click += (_, _) =>
        {
            if (!_p.Store.HasBackup) { MessageBox.Show(this, _greek ? "Δεν έχει γίνει καμία αλλαγή από το plugin ακόμη." : "The plugin has not changed anything yet.", "Opsis Appearance"); return; }
            _p.RestoreOriginal(); RefreshAll();
        };

        ApplyLanguage(greek);
        ApplyPalette(pal);
        RefreshAll();
    }

    private static FlowLayoutPanel Row(params Control[] items)
    {
        var r = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(760, 0), Margin = new Padding(0, 2, 0, 2) };
        r.Controls.AddRange(items);
        return r;
    }

    /// <summary>Backup the original values (first time only), run a change, refresh the page.</summary>
    private void Do(Action change)
    {
        try { _p.Backup(); change(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opsis Appearance"); }
        RefreshAll();
    }

    private void ChangeEffects(Action<EffectsSettings> change)
    {
        try { change(_p.Settings.Effects); _p.Store.Save(_p.Settings); _p.ApplyEffects(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opsis Appearance"); }
        RefreshAll();
    }

    private bool PickSlideFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = _greek ? "Φάκελος με εικόνες για το slideshow" : "Folder with pictures for the slideshow", UseDescriptionForTitle = true, SelectedPath = _p.Settings.Slideshow.Folder };
        if (dlg.ShowDialog(this) != DialogResult.OK) return false;
        _p.Settings.Slideshow.Folder = dlg.SelectedPath; _p.SaveSettings();
        return true;
    }

    private void ModeChanged(ThemeMode mode, RadioButton rb)
    {
        if (_loading || !rb.Checked) return;
        _p.Settings.Mode = mode; _p.SaveSettings(); RefreshAll();
    }

    private void SaveSchedule()
    {
        if (_loading) return;
        var s = _p.Settings;
        s.LightAt = TimeSpan.TryParse(_lightAt.Text, out _) ? _lightAt.Text : s.LightAt;
        s.DarkAt = TimeSpan.TryParse(_darkAt.Text, out _) ? _darkAt.Text : s.DarkAt;
        s.Latitude = (double)_lat.Value; s.Longitude = (double)_lon.Value; s.SunOffsetMinutes = (int)_offset.Value;
        _p.SaveSettings(); RefreshAll();
    }

    // ---- Theme manager ----------------------------------------------------------------------------------------------
    private OpsisTheme? SelectedTheme => _themeList.SelectedIndex >= 0 && _themeList.SelectedIndex < _themeItems.Count ? _themeItems[_themeList.SelectedIndex] : null;

    private void ReloadThemes(string? select = null)
    {
        _themeItems = _p.Themes.List();
        _themeList.BeginUpdate();
        _themeList.Items.Clear();
        foreach (var t in _themeItems) _themeList.Items.Add(t.Name);
        _themeList.EndUpdate();
        int idx = select == null ? -1 : _themeItems.FindIndex(t => t.Name == select);
        _themeList.SelectedIndex = idx >= 0 ? idx : (_themeItems.Count > 0 ? 0 : -1);

        _winThemeItems = new();
        foreach (var dir in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Resources", "Themes"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Themes") })
        {
            try { if (Directory.Exists(dir)) foreach (var f in Directory.EnumerateFiles(dir, "*.theme")) _winThemeItems.Add((Path.GetFileNameWithoutExtension(f), f)); }
            catch (Exception) { }
        }
        _winThemeItems = _winThemeItems.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        _winThemes.Items.Clear();
        foreach (var w in _winThemeItems) _winThemes.Items.Add(w.Name);
        if (_winThemes.Items.Count > 0) _winThemes.SelectedIndex = 0;
        _winThemeApply.Enabled = _winThemes.Items.Count > 0;
        ShowThemeInfo();
    }

    private string Describe(OpsisTheme t)
    {
        var parts = new List<string>();
        if (t.Look.Dark is bool d) parts.Add(d ? (_greek ? "σκούρο" : "dark") : (_greek ? "φωτεινό" : "light"));
        if (t.Look.AccentRgb is int a) parts.Add("accent #" + a.ToString("X6"));
        if (t.Effects is { } e && e.AnyWindowEffect) parts.Add(_greek ? "εφέ παραθύρων" : "window effects");
        if (t.WallpaperFile != null) parts.Add("wallpaper");
        if (t.Look.TaskbarCentered == false) parts.Add(_greek ? "taskbar αριστερά" : "left taskbar");
        string text = string.Join(" · ", parts);
        return t.Description.Length > 0 ? t.Description + (text.Length > 0 ? "  [" + text + "]" : "") : text;
    }

    private void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox cb || e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var bg = new SolidBrush(sel ? _pal.Accent : _pal.Surface)) e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, cb.Items[e.Index]?.ToString() ?? "", cb.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), sel ? Color.Black : _pal.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void ShowThemeInfo()
    {
        var t = SelectedTheme;
        _themeInfo.Text = t == null ? "" : Describe(t);
        _themeApply.Enabled = t != null;
        _themeDelete.Enabled = t is { BuiltIn: false };
        _themeExport.Enabled = t is { BuiltIn: false } || t != null;
        _themeList.Invalidate();
    }

    private void DrawThemeItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _themeItems.Count) return;
        var t = _themeItems[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(sel ? Color.FromArgb(60, _pal.Accent) : _pal.Surface)) e.Graphics.FillRectangle(bg, e.Bounds);
        // Κάρτα: swatch (accent / light-dark) + όνομα + περιγραφή
        var sw = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 7, 24, 24);
        Color accent = t.Look.AccentRgb is int a ? Color.FromArgb(255, Color.FromArgb(a)) : _pal.Muted;
        using (var ab = new SolidBrush(accent)) e.Graphics.FillEllipse(ab, sw);
        if (t.Look.Dark is bool dark) { using var half = new SolidBrush(dark ? Color.FromArgb(235, 18, 22, 32) : Color.FromArgb(235, 245, 246, 250)); e.Graphics.FillPie(half, sw, 270, 180); }
        using var f1 = new Font("Segoe UI Semibold", 9.5f); using var f2 = new Font("Segoe UI", 8f);
        using var fg = new SolidBrush(_pal.Text); using var mu = new SolidBrush(_pal.Muted);
        string name = t.BuiltIn ? LookTitle(t.Name) : t.Name;
        e.Graphics.DrawString(name, f1, fg, e.Bounds.X + 42, e.Bounds.Y + 3);
        e.Graphics.DrawString(Describe(t), f2, mu, new RectangleF(e.Bounds.X + 42, e.Bounds.Y + 21, e.Bounds.Width - 50, 15), new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
    }

    private void ApplySelectedTheme()
    {
        var t = SelectedTheme; if (t == null) return;
        var r = _p.ApplyTheme(t);
        if (r.Errors.Count > 0) MessageBox.Show(this, string.Join("\n", r.Errors), "Opsis Appearance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshAll();
    }

    private void SaveCurrentAsTheme()
    {
        string? name = Prompt(_greek ? "Όνομα θέματος:" : "Theme name:", _greek ? "Το θέμα μου" : "My theme");
        if (string.IsNullOrWhiteSpace(name)) return;
        bool withWall = MessageBox.Show(this, _greek ? "Να συμπεριληφθεί και το τρέχον wallpaper των Windows;" : "Include the current Windows wallpaper too?", "Opsis Appearance", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        try
        {
            _p.Backup();
            var (theme, wall) = _p.Applier.CaptureCurrent(name.Trim(), _p.Settings.Effects, withWall);
            _p.Themes.Save(theme, wall);
            ReloadThemes(theme.Name);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opsis Appearance"); }
    }

    private void DeleteSelectedTheme()
    {
        var t = SelectedTheme; if (t == null || t.BuiltIn) return;
        if (MessageBox.Show(this, _greek ? $"Διαγραφή του θέματος «{t.Name}»;" : $"Delete the theme \"{t.Name}\"?", "Opsis Appearance", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { _p.Themes.Delete(t.Name); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opsis Appearance"); }
        ReloadThemes();
    }

    private void ImportTheme()
    {
        using var dlg = new OpenFileDialog { Filter = "Opsis theme (*.opsistheme)|*.opsistheme|Zip (*.zip)|*.zip", Title = _greek ? "Εισαγωγή θέματος" : "Import theme" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { var t = _p.Themes.Import(dlg.FileName); ReloadThemes(t.Name); }
        catch (Exception ex) { MessageBox.Show(this, (_greek ? "Μη έγκυρο αρχείο θέματος: " : "Not a valid theme package: ") + ex.Message, "Opsis Appearance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ExportSelectedTheme()
    {
        var t = SelectedTheme; if (t == null) return;
        using var dlg = new SaveFileDialog { Filter = "Opsis theme (*.opsistheme)|*.opsistheme", FileName = t.Name + ".opsistheme", Title = _greek ? "Εξαγωγή θέματος" : "Export theme" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { _p.Themes.Export(t, dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opsis Appearance"); }
    }

    private string? Prompt(string text, string initial)
    {
        using var f = new Form { Text = "Opsis Appearance", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, Width = 360, Height = 150, MinimizeBox = false, MaximizeBox = false };
        var l = new Label { Text = text, Left = 12, Top = 12, AutoSize = true };
        var t = new TextBox { Left = 12, Top = 34, Width = 320, Text = initial };
        var ok = new Button { Text = "OK", Left = 176, Top = 70, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = _greek ? "Άκυρο" : "Cancel", Left = 257, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { l, t, ok, cancel }); f.AcceptButton = ok; f.CancelButton = cancel;
        return f.ShowDialog(this) == DialogResult.OK ? t.Text : null;
    }

    public void ApplyLanguage(bool greek)
    {
        _greek = greek;
        string T(string el, string en) => greek ? el : en;
        _hSchedule.Text = T("Θέμα Light / Dark", "Light / Dark theme");
        _manual.Text = T("Χειροκίνητα", "Manual"); _autoSun.Text = T("Αυτόματα με ανατολή/δύση", "Automatic (sunrise/sunset)"); _autoFixed.Text = T("Αυτόματα με σταθερές ώρες", "Automatic (fixed times)");
        _lightNow.Text = T("☀ Light τώρα", "☀ Light now"); _darkNow.Text = T("☾ Dark τώρα", "☾ Dark now");
        _tLight.Text = T("Light από", "Light at"); _tDark.Text = T("Dark από", "Dark at");
        _tLat.Text = T("Γ. πλάτος", "Latitude"); _tLon.Text = T("Γ. μήκος", "Longitude"); _tOff.Text = T("Μετατόπιση (λεπτά)", "Offset (min)");
        _hAccent.Text = T("Χρώμα accent", "Accent colour");
        _pick.Text = T("Άλλο χρώμα…", "Custom colour…"); _fromWall.Text = T("Από το wallpaper", "From wallpaper");
        _hTaskbar.Text = T("Taskbar και διαφάνειες", "Taskbar & transparency");
        _transp.Text = T("Εφέ διαφάνειας", "Transparency effects"); _accentSurf.Text = T("Accent σε Start/taskbar/τίτλους", "Accent on Start/taskbar/title bars");
        _centered.Text = T("Κεντραρισμένο taskbar", "Centered taskbar"); _search.Text = T("Αναζήτηση", "Search"); _taskView.Text = T("Task View", "Task View"); _widgets.Text = T("Widgets", "Widgets");
        _hEffects.Text = T("Εφέ παραθύρων (όσο τρέχει το Opsis)", "Window effects (while Opsis runs)");
        _borderCheck.Text = T("Χρωματιστό border στο ενεργό παράθυρο", "Coloured border on the active window");
        _borderColor.Text = T("Χρώμα border…", "Border colour…");
        _tCorners.Text = T("Γωνίες", "Corners"); _tTitle.Text = T("Σκούρες γραμμές τίτλου", "Dark title bars"); _tBackdrop.Text = T("Backdrop τίτλου", "Title backdrop");
        string[] cs = greek ? new[] { "Προεπιλογή", "Στρογγυλές", "Μικρές στρογγυλές", "Τετράγωνες" } : new[] { "Default", "Round", "Small round", "Square" };
        string[] ds = greek ? new[] { "Ανενεργό", "Πάντα σκούρες", "Ακολουθεί το θέμα" } : new[] { "Off", "Always dark", "Follow theme" };
        string[] bs = greek ? new[] { "Προεπιλογή", "Κανένα", "Mica", "Acrylic", "Tabbed" } : new[] { "Default", "None", "Mica", "Acrylic", "Tabbed" };
        void Fill(ComboBox cb, string[] items) { int i = cb.SelectedIndex; cb.Items.Clear(); cb.Items.AddRange(items); cb.SelectedIndex = Math.Max(0, i); }
        _loading = true; Fill(_corners, cs); Fill(_titleBars, ds); Fill(_backdrop, bs); _loading = false;
        _hideTaskbar.Text = T("Αυτόματη απόκρυψη του taskbar των Windows (χρήσιμο με το DeskStrip)", "Auto-hide the Windows taskbar (handy with DeskStrip)");
        _hShell.Text = T("Εξερευνητής και taskbar (συμπεριφορά)", "Explorer & taskbar (behaviour)");
        _fileExt.Text = T("Εμφάνιση επεκτάσεων αρχείων", "Show file extensions"); _hiddenFiles.Text = T("Εμφάνιση κρυφών αρχείων", "Show hidden files");
        _clockSecs.Text = T("Δευτερόλεπτα στο ρολόι", "Seconds in the clock"); _animations.Text = T("Animations παραθύρων", "Window animations");
        _tCombine.Text = T("Κουμπιά εφαρμογών στο taskbar", "Taskbar buttons");
        _restartExplorer.Text = T("↻ Επανεκκίνηση Εξερευνητή", "↻ Restart Explorer");
        _tShellNote.Text = T("Κάποιες αλλαγές (συνδυασμός κουμπιών, κρυφά αρχεία) φαίνονται αμέσως ή μετά από επανεκκίνηση του Εξερευνητή.", "Some changes (button grouping, hidden files) apply at once or after Explorer is restarted.");
        _hSlide.Text = T("Wallpaper slideshow", "Wallpaper slideshow");
        _slideOn.Text = T("Αλλαγή wallpaper από φάκελο", "Change the wallpaper from a folder"); _slideShuffle.Text = T("Τυχαία σειρά", "Shuffle");
        _slideFolder.Text = T("Φάκελος…", "Folder…"); _slideNext.Text = T("Επόμενο τώρα", "Next now");
        _tSlideEvery.Text = T("κάθε (λεπτά)", "every (min)"); _tSlideStyle.Text = T("Προσαρμογή", "Fit");
        _loading = true;
        { int i = _combine.SelectedIndex; _combine.Items.Clear(); _combine.Items.AddRange(greek ? new[] { "Πάντα συνδυασμός", "Συνδυασμός όταν γεμίσει", "Ποτέ (με ετικέτες)" } : new[] { "Always combine", "Combine when full", "Never (with labels)" }); _combine.SelectedIndex = Math.Max(0, i); }
        { int i = _slideStyle.SelectedIndex; _slideStyle.Items.Clear(); _slideStyle.Items.AddRange(greek ? new[] { "Γέμισμα", "Προσαρμογή", "Έκταση", "Πλακίδια", "Κέντρο", "Span" } : new[] { "Fill", "Fit", "Stretch", "Tile", "Center", "Span" }); _slideStyle.SelectedIndex = Math.Max(0, i); }
        _loading = false;
        _hLooks.Text = T("Theme manager — πλήρη θέματα (εμφάνιση Windows + εφέ + wallpaper)", "Theme manager — full themes (Windows look + effects + wallpaper)");
        _themeApply.Text = T("Εφαρμογή", "Apply"); _themeSave.Text = T("Αποθήκευση τρέχουσας ως θέμα…", "Save current as theme…"); _themeDelete.Text = T("Διαγραφή", "Delete");
        _themeImport.Text = T("Εισαγωγή (.opsistheme)…", "Import (.opsistheme)…"); _themeExport.Text = T("Εξαγωγή…", "Export…"); _themeFolder.Text = T("Φάκελος θεμάτων", "Themes folder");
        _hWinThemes.Text = T("Θέματα των Windows (.theme)", "Windows themes (.theme)"); _winThemeApply.Text = T("Εφαρμογή", "Apply");
        _tWinThemes.Text = T("Τα .theme των Windows αλλάζουν και δείκτες/ήχους, αλλά όχι τα εφέ του Opsis. Τα θέματα Opsis παραπάνω αλλάζουν light/dark, accent, taskbar, εφέ παραθύρων και wallpaper.",
            "Windows .theme files also change cursors/sounds but not Opsis window effects. The Opsis themes above change light/dark, accent, taskbar, window effects and wallpaper.");
        _restore.Text = T("↩ Επαναφορά στις αρχικές μου ρυθμίσεις", "↩ Restore my original settings");
        _tSafety.Text = T("Αλλάζουν μόνο ρυθμίσεις του τρέχοντος χρήστη. Η πρώτη αλλαγή αποθηκεύει αντίγραφο των αρχικών τιμών.", "Only per-user settings change. The first change saves a copy of your original values.");
        ReloadThemes(SelectedTheme?.Name);
    }

    public void ApplyPalette(Palette p)
    {
        _pal = p;
        BackColor = p.Bg; _flow.BackColor = p.Bg; _swatches.BackColor = p.Bg;
        _themeList.BackColor = p.Surface; _themeList.ForeColor = p.Text; _themeInfo.ForeColor = p.Muted; _themeInfo.BackColor = p.Bg;
        _winThemes.BackColor = p.Surface; _winThemes.ForeColor = p.Text; _winThemes.FlatStyle = FlatStyle.Flat;
        foreach (var l in _headings) { l.ForeColor = p.Text; l.BackColor = p.Bg; }
        foreach (var l in _texts) { l.ForeColor = p.Muted; l.BackColor = p.Bg; }
        _status.ForeColor = p.Accent; _status.BackColor = p.Bg;
        foreach (var c in _checks) { c.ForeColor = p.Text; c.BackColor = p.Bg; c.AutoSize = true; c.Margin = new Padding(0, 4, 18, 4); }
        foreach (var r in _radios) { r.ForeColor = p.Text; r.BackColor = p.Bg; }
        foreach (var b in _buttons) StyleButton(b);
        _themeList.Invalidate();
        foreach (var cb in new ComboBox[] { _corners, _titleBars, _backdrop, _winThemes, _combine, _slideStyle })
        {
            cb.BackColor = p.Surface; cb.ForeColor = p.Text; cb.FlatStyle = FlatStyle.Flat;
            cb.DrawMode = DrawMode.OwnerDrawFixed; cb.ItemHeight = 20;
            cb.DrawItem -= DrawComboItem; cb.DrawItem += DrawComboItem;      // dark list + dark face instead of the white system one
        }
        foreach (var t in new TextBox[] { _lightAt, _darkAt }) { t.BackColor = p.Surface; t.ForeColor = p.Text; }
        foreach (var n in new NumericUpDown[] { _lat, _lon, _offset, _slideMin }) { n.BackColor = p.Surface; n.ForeColor = p.Text; }
        foreach (Control c in _flow.Controls) if (c is FlowLayoutPanel row) row.BackColor = p.Bg;
    }

    private void StyleButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat; b.BackColor = _pal.Surface; b.ForeColor = _pal.Text; b.FlatAppearance.BorderColor = _pal.Border;
        b.AutoSize = true; b.Padding = new Padding(8, 3, 8, 3); b.Margin = new Padding(0, 2, 8, 2);
    }

    public void RefreshAll()
    {
        if (IsDisposed) return;
        _loading = true;
        var s = _p.Settings; var cur = _p.Engine.ReadCurrent();
        _manual.Checked = s.Mode == ThemeMode.Manual; _autoSun.Checked = s.Mode == ThemeMode.AutoSun; _autoFixed.Checked = s.Mode == ThemeMode.AutoFixed;
        _lightAt.Text = s.LightAt; _darkAt.Text = s.DarkAt;
        _lat.Value = (decimal)Math.Clamp(s.Latitude, -90, 90); _lon.Value = (decimal)Math.Clamp(s.Longitude, -180, 180); _offset.Value = Math.Clamp(s.SunOffsetMinutes, -180, 180);
        bool sun = s.Mode == ThemeMode.AutoSun, fixedT = s.Mode == ThemeMode.AutoFixed;
        _lat.Enabled = _lon.Enabled = _offset.Enabled = sun; _lightAt.Enabled = _darkAt.Enabled = fixedT;
        _transp.Checked = cur.Transparency == true; _accentSurf.Checked = cur.AccentOnSurfaces == true; _centered.Checked = cur.TaskbarCentered == true;
        _search.Checked = cur.ShowSearch == true; _taskView.Checked = cur.ShowTaskView == true; _widgets.Checked = cur.ShowWidgets == true;
        _fileExt.Checked = cur.ShowFileExtensions == true; _hiddenFiles.Checked = cur.ShowHiddenFiles == true; _clockSecs.Checked = cur.ClockSeconds == true;
        _animations.Checked = cur.Animations == true; _combine.SelectedIndex = Math.Clamp(cur.TaskbarCombine ?? 0, 0, 2);
        var sl = s.Slideshow;
        _slideOn.Checked = sl.Enabled; _slideShuffle.Checked = sl.Shuffle; _slideMin.Value = Math.Clamp(sl.IntervalMinutes, 1, 1440); _slideStyle.SelectedIndex = Math.Clamp((int)sl.Style, 0, 5);
        int pics = WallpaperSlideshow.Images(sl.Folder).Count;
        _slideInfo.Text = string.IsNullOrWhiteSpace(sl.Folder)
            ? (_greek ? "Διάλεξε φάκελο με εικόνες (png, jpg, bmp)." : "Choose a folder with pictures (png, jpg, bmp).")
            : $"{sl.Folder}  ·  {pics} " + (_greek ? "εικόνες" : "pictures") + (sl.LastFile != null ? $"  ·  {(_greek ? "τελευταία" : "last")}: {Path.GetFileName(sl.LastFile)}" : "");
        _restore.Enabled = _p.Store.HasBackup;
        var fx = s.Effects;
        _borderCheck.Checked = fx.ActiveBorder; _hideTaskbar.Checked = fx.AutoHideTaskbar;
        _corners.SelectedIndex = (int)fx.Corners; _titleBars.SelectedIndex = (int)fx.DarkTitle; _backdrop.SelectedIndex = (int)fx.Backdrop;

        string mode = cur.Dark == true ? (_greek ? "Σκούρο" : "Dark") : (_greek ? "Φωτεινό" : "Light");
        string next = "";
        if (s.Mode != ThemeMode.Manual)
        {
            bool dark = AppearanceEngine.ShouldBeDark(s, DateTime.Now);
            if (s.Mode == ThemeMode.AutoSun && Solar.Compute(DateTime.Today, s.Latitude, s.Longitude) is { } sunT)
                next = _greek ? $"   ·   ανατολή {sunT.Sunrise:HH:mm}, δύση {sunT.Sunset:HH:mm}" : $"   ·   sunrise {sunT.Sunrise:HH:mm}, sunset {sunT.Sunset:HH:mm}";
            if (dark != (cur.Dark == true)) next += _greek ? "   ·   αλλάζει αυτόματα" : "   ·   switching automatically";
        }
        _status.Text = (_greek ? "Τώρα: " : "Now: ") + mode + next;
        _loading = false;
    }

    private string LookTitle(string name) => !_greek ? name : name switch { "Day" => "Ημέρα", "Night" => "Νύχτα", "Focus" => "Συγκέντρωση", "Classic taskbar" => "Κλασικό taskbar", _ => name };
}
