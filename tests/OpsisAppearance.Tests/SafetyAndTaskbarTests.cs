using OpsisAppearance;
using Xunit;

namespace OpsisAppearance.Tests;

// ---------------------------------------------------------------------------------------------------------------------
// BackupVault: κανένα τεστ δεν αγγίζει πραγματικά αρχεία των Windows — όλα γίνονται σε προσωρινούς φακέλους.
// ---------------------------------------------------------------------------------------------------------------------
public class BackupVaultTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oa_vault_" + Guid.NewGuid().ToString("N"));
    private readonly BackupVault _vault;
    public BackupVaultTests() { Directory.CreateDirectory(_dir); _vault = new BackupVault(Path.Combine(_dir, "vault")); }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private string File1(string name, string content) { var p = Path.Combine(_dir, name); File.WriteAllText(p, content); return p; }

    [Fact]
    public void Backup_copies_and_verifies_the_original()
    {
        var f = File1("explorer.exe", "ORIGINAL BYTES");
        var r = _vault.Backup(f);
        Assert.True(r.Ok, r.Message);
        Assert.Equal(BackupVault.Sha256Of(f), r.Entry!.Sha256);
        Assert.Equal("ORIGINAL BYTES", File.ReadAllText(r.Entry.BackupPath));
    }

    [Fact]
    public void A_second_backup_never_replaces_the_first_one_even_if_the_file_was_patched_meanwhile()
    {
        var f = File1("dwm.dll", "ORIGINAL");
        var first = _vault.Backup(f);
        File.WriteAllText(f, "PATCHED");                                  // η επέμβαση έγινε
        var second = _vault.Backup(f);                                    // κάποιος ξανακαλεί το backup
        Assert.True(second.Ok);
        Assert.Equal(first.Entry!.BackupPath, second.Entry!.BackupPath);
        Assert.Equal("ORIGINAL", File.ReadAllText(second.Entry.BackupPath));   // το γνήσιο αρχικό έμεινε
    }

    [Fact]
    public void Restore_puts_the_original_back_and_verifies_it()
    {
        var f = File1("shell.dll", "ORIGINAL");
        _vault.Backup(f);
        File.WriteAllText(f, "PATCHED BY SOMETHING");
        var r = _vault.Restore(f);
        Assert.True(r.Ok, r.Message);
        Assert.Equal("ORIGINAL", File.ReadAllText(f));
        Assert.False(File.Exists(f + ".opsis-restore.tmp"));                // δεν μένει προσωρινό αρχείο
    }

    [Fact]
    public void Restore_works_even_if_the_patched_file_was_deleted()
    {
        var f = File1("a.dll", "ORIGINAL");
        _vault.Backup(f);
        File.Delete(f);
        Assert.True(_vault.Restore(f).Ok);
        Assert.Equal("ORIGINAL", File.ReadAllText(f));
    }

    [Fact]
    public void A_damaged_backup_is_never_copied_over_the_file()
    {
        var f = File1("b.dll", "ORIGINAL");
        var e = _vault.Backup(f).Entry!;
        File.WriteAllText(f, "PATCHED");
        File.WriteAllText(e.BackupPath, "CORRUPTED BACKUP");
        var r = _vault.Restore(f);
        Assert.False(r.Ok);
        Assert.Contains("damaged", r.Message);
        Assert.Equal("PATCHED", File.ReadAllText(f));                      // ο στόχος δεν αγγίχτηκε
    }

    [Fact]
    public void Missing_backup_or_missing_file_is_a_clean_failure_not_an_exception()
    {
        Assert.False(_vault.Restore(Path.Combine(_dir, "unknown.dll")).Ok);
        Assert.False(_vault.Backup(Path.Combine(_dir, "does-not-exist.dll")).Ok);
        var f = File1("c.dll", "x"); var e = _vault.Backup(f).Entry!;
        File.Delete(e.BackupPath);
        Assert.False(_vault.Restore(f).Ok);
        Assert.False(_vault.Backup(f).Ok);                                  // το καταγεγραμμένο αντίγραφο λείπει: δεν αντικαθίσταται σιωπηλά
    }

    [Fact]
    public void Restore_of_an_untouched_file_reports_it_already_is_the_original()
    {
        var f = File1("d.dll", "SAME");
        _vault.Backup(f);
        var r = _vault.Restore(f);
        Assert.True(r.Ok);
        Assert.Contains("already", r.Message);
    }

    [Fact]
    public void The_manifest_survives_a_new_vault_instance_and_a_corrupt_manifest_is_survivable()
    {
        var f = File1("e.dll", "ORIGINAL");
        _vault.Backup(f);
        Assert.NotNull(new BackupVault(_vault.Root).Find(f));
        File.WriteAllText(Path.Combine(_vault.Root, "manifest.json"), "{ not json");
        Assert.Empty(new BackupVault(_vault.Root).All());                  // δεν ρίχνει exception
    }

    [Fact]
    public void Files_with_the_same_name_in_different_folders_do_not_collide()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "x")); Directory.CreateDirectory(Path.Combine(_dir, "y"));
        var a = Path.Combine(_dir, "x", "same.dll"); File.WriteAllText(a, "A");
        var b = Path.Combine(_dir, "y", "same.dll"); File.WriteAllText(b, "B");
        var ra = _vault.Backup(a); var rb = _vault.Backup(b);
        Assert.True(ra.Ok && rb.Ok);
        Assert.NotEqual(ra.Entry!.BackupPath, rb.Entry!.BackupPath);
        File.WriteAllText(a, "X"); File.WriteAllText(b, "Y");
        _vault.Restore(a); _vault.Restore(b);
        Assert.Equal("A", File.ReadAllText(a)); Assert.Equal("B", File.ReadAllText(b));
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Σημείο επαναφοράς: πολιτική (το πραγματικό API δεν καλείται ποτέ στα τεστ)
// ---------------------------------------------------------------------------------------------------------------------
internal sealed class FakeRestorePoints : IRestorePointService
{
    public bool IsAdministrator { get; set; } = true;
    public int Calls;
    public RestorePointStatus Result = RestorePointStatus.Created;
    public RestorePointResult Create(string description) { Calls++; return new RestorePointResult(Result, "fake: " + description); }
}

public class InvasiveGuardTests
{
    private static readonly DateTime T0 = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_first_invasive_change_requests_a_restore_point_and_records_it()
    {
        var svc = new FakeRestorePoints(); var s = new SafetySettings();
        var r = InvasiveGuard.BeforeInvasiveChange(s, svc, "Opsis Appearance: taskbar", T0);
        Assert.NotNull(r); Assert.Equal(1, svc.Calls);
        Assert.Equal(T0, s.LastAttemptUtc);
        Assert.Contains("Created", s.LastStatus);
    }

    [Fact]
    public void It_asks_at_most_once_per_day()
    {
        var svc = new FakeRestorePoints(); var s = new SafetySettings();
        InvasiveGuard.BeforeInvasiveChange(s, svc, "x", T0);
        Assert.Null(InvasiveGuard.BeforeInvasiveChange(s, svc, "x", T0.AddHours(3)));
        Assert.Equal(1, svc.Calls);
        Assert.NotNull(InvasiveGuard.BeforeInvasiveChange(s, svc, "x", T0.AddHours(25)));
        Assert.Equal(2, svc.Calls);
    }

    [Fact]
    public void A_clock_set_back_does_not_block_it_forever()
    {
        var svc = new FakeRestorePoints(); var s = new SafetySettings { LastAttemptUtc = T0.AddDays(30) };
        Assert.NotNull(InvasiveGuard.BeforeInvasiveChange(s, svc, "x", T0));
    }

    [Fact]
    public void The_user_can_switch_it_off_but_the_button_can_force_it()
    {
        var svc = new FakeRestorePoints(); var s = new SafetySettings { RestorePointBeforeInvasive = false };
        Assert.Null(InvasiveGuard.BeforeInvasiveChange(s, svc, "x", T0));
        Assert.Equal(0, svc.Calls);
        Assert.NotNull(InvasiveGuard.BeforeInvasiveChange(s, svc, "x", T0, force: true));
        Assert.Equal(1, svc.Calls);
    }

    [Fact]
    public void Missing_administrator_rights_is_reported_not_thrown()
    {
        var svc = new FakeRestorePoints { Result = RestorePointStatus.NeedsAdministrator }; var s = new SafetySettings();
        var r = InvasiveGuard.BeforeInvasiveChange(s, svc, "x", T0);
        Assert.Equal(RestorePointStatus.NeedsAdministrator, r!.Status);
        Assert.Contains("NeedsAdministrator", s.LastStatus);
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Taskbar: ψεύτικη επιφάνεια
// ---------------------------------------------------------------------------------------------------------------------
internal sealed class FakeSurface : ITaskbarSurface
{
    public readonly List<IntPtr> Bars = new() { (IntPtr)100 };
    public readonly HashSet<IntPtr> Dead = new();
    public readonly Dictionary<IntPtr, (int State, int Color)> Accent = new();
    public readonly Dictionary<IntPtr, (ScreenRect? Region, int Radius)> Region = new();
    public readonly Dictionary<IntPtr, ScreenRect> Rects = new() { [(IntPtr)100] = new ScreenRect(0, 1040, 1920, 1080) };
    public bool Maximized, StartActive;
    public int AccentCalls, RegionCalls;

    public IReadOnlyList<IntPtr> Taskbars() => Bars.ToList();
    public bool IsAlive(IntPtr h) => !Dead.Contains(h);
    public void SetAccent(IntPtr h, int s, int c) { Accent[h] = (s, c); AccentCalls++; }
    public bool AnyMaximizedWindow() => Maximized;
    public bool StartOrSearchActive() => StartActive;
    public ScreenRect GetRect(IntPtr h) => Rects[h];
    public void SetRegion(IntPtr h, ScreenRect? r, int radius) { Region[h] = (r, radius); RegionCalls++; }
}

public class TaskbarLookTests
{
    private static TaskbarLookSettings Cfg(TaskbarState normal = TaskbarState.Clear, TaskbarState max = TaskbarState.Opaque, TaskbarState start = TaskbarState.Default) =>
        new() { Enabled = true, Normal = normal, Maximized = max, StartOpen = start, TintRgb = 0x112233, OpacityPercent = 50 };

    [Fact]
    public void Clear_is_the_transparent_gradient_with_zero_alpha()
    {
        var s = new FakeSurface(); var e = new TaskbarLookEngine(s);
        e.Apply(Cfg());
        Assert.Equal((2, 0x00332211), s.Accent[(IntPtr)100]);              // ACCENT_ENABLE_TRANSPARENTGRADIENT, alpha 0, ABGR
    }

    [Theory]
    [InlineData(TaskbarState.Blur, 3)]
    [InlineData(TaskbarState.Acrylic, 4)]
    [InlineData(TaskbarState.Opaque, 1)]
    [InlineData(TaskbarState.Clear, 2)]
    [InlineData(TaskbarState.Default, 0)]
    public void Accent_state_numbers_match_the_windows_enum(TaskbarState st, int expected) => Assert.Equal(expected, TaskbarLookEngine.AccentState(st));

    [Fact]
    public void Opacity_percent_becomes_the_alpha_byte_for_blur_and_acrylic_and_opaque_is_always_solid()
    {
        var c = Cfg(); c.OpacityPercent = 50;
        Assert.Equal(0x80, (TaskbarLookEngine.GradientColor(c, TaskbarState.Blur) >> 24) & 0xFF);
        Assert.Equal(0xFF, (TaskbarLookEngine.GradientColor(c, TaskbarState.Opaque) >> 24) & 0xFF);
        c.OpacityPercent = 999; Assert.Equal(0xFF, (TaskbarLookEngine.GradientColor(c, TaskbarState.Acrylic) >> 24) & 0xFF);     // εκτός ορίων: κόβεται
        c.OpacityPercent = -5; Assert.Equal(0, (TaskbarLookEngine.GradientColor(c, TaskbarState.Acrylic) >> 24) & 0xFF);
    }

    [Fact]
    public void A_maximized_window_or_the_start_menu_switch_the_state_and_it_switches_back()
    {
        var s = new FakeSurface(); var e = new TaskbarLookEngine(s);
        e.Apply(Cfg(TaskbarState.Clear, TaskbarState.Opaque, TaskbarState.Acrylic));
        Assert.Equal(2, s.Accent[(IntPtr)100].State);
        s.Maximized = true; e.Tick(); Assert.Equal(1, s.Accent[(IntPtr)100].State);
        s.StartActive = true; e.Tick(); Assert.Equal(4, s.Accent[(IntPtr)100].State);       // το Start υπερισχύει
        s.StartActive = false; e.Tick(); Assert.Equal(1, s.Accent[(IntPtr)100].State);
        s.Maximized = false; e.Tick(); Assert.Equal(2, s.Accent[(IntPtr)100].State);
    }

    [Fact]
    public void No_timer_is_needed_when_every_state_is_the_same()
    {
        var e = new TaskbarLookEngine(new FakeSurface());
        e.Apply(Cfg(TaskbarState.Blur, TaskbarState.Blur, TaskbarState.Blur));
        Assert.False(e.NeedsTimer);
        e.Apply(Cfg(TaskbarState.Blur, TaskbarState.Opaque, TaskbarState.Blur));
        Assert.True(e.NeedsTimer);
    }

    [Fact]
    public void A_tick_with_nothing_changed_does_not_touch_the_taskbar_until_the_periodic_refresh()
    {
        var s = new FakeSurface(); var e = new TaskbarLookEngine(s);
        e.Apply(Cfg());
        int after = s.AccentCalls;
        for (int i = 0; i < 10; i++) e.Tick();
        Assert.Equal(after, s.AccentCalls);                                  // καμία περιττή κλήση
        for (int i = 0; i < 15; i++) e.Tick();
        Assert.True(s.AccentCalls > after);                                  // περιοδική επανεφαρμογή (ο Explorer μπορεί να ξαναβάψει το taskbar)
    }

    [Fact]
    public void A_new_taskbar_after_an_explorer_restart_or_a_second_monitor_is_styled_at_once()
    {
        var s = new FakeSurface(); var e = new TaskbarLookEngine(s);
        e.Apply(Cfg());
        s.Bars.Add((IntPtr)200);                                              // δεύτερη οθόνη / νέο παράθυρο taskbar
        e.Tick();
        Assert.Equal(2, s.Accent[(IntPtr)200].State);
        s.Bars[0] = (IntPtr)101;                                              // ο Explorer ξανάνοιξε: νέο handle
        e.Tick();
        Assert.Equal(2, s.Accent[(IntPtr)101].State);
    }

    [Fact]
    public void RestoreAll_returns_every_touched_taskbar_to_the_windows_default()
    {
        var s = new FakeSurface(); var e = new TaskbarLookEngine(s);
        s.Bars.Add((IntPtr)200);
        e.Apply(Cfg());
        e.RestoreAll();
        Assert.Equal((0, 0), s.Accent[(IntPtr)100]);
        Assert.Equal((0, 0), s.Accent[(IntPtr)200]);
        Assert.Empty(e.Touched);
        Assert.False(e.Settings.Enabled);
    }

    [Fact]
    public void Disabling_the_feature_restores_and_dead_windows_are_skipped()
    {
        var s = new FakeSurface(); var e = new TaskbarLookEngine(s);
        e.Apply(Cfg());
        s.Dead.Add((IntPtr)100); int calls = s.AccentCalls;
        e.Apply(new TaskbarLookSettings { Enabled = false });
        Assert.Equal(calls, s.AccentCalls);                                   // το νεκρό handle δεν πειράχτηκε
        Assert.Empty(e.Touched);
    }

    [Fact]
    public void Default_state_means_the_taskbar_is_left_as_windows_draws_it()
    {
        var s = new FakeSurface(); var e = new TaskbarLookEngine(s);
        e.Apply(Cfg(TaskbarState.Default, TaskbarState.Clear));
        Assert.Equal((0, 0), s.Accent[(IntPtr)100]);
        s.Maximized = true; e.Tick(); Assert.Equal(2, s.Accent[(IntPtr)100].State);
    }
}

public class TaskbarShapeTests
{
    [Fact]
    public void The_region_is_the_window_shrunk_by_the_margins_in_window_coordinates()
    {
        var r = TaskbarShapeEngine.RegionFor(new TaskbarShapeSettings { MarginX = 8, MarginY = 4 }, new ScreenRect(0, 1040, 1920, 1080));
        Assert.Equal(new ScreenRect(8, 4, 1912, 36), r);
    }

    [Fact]
    public void A_region_that_would_be_absurdly_small_is_refused()
    {
        Assert.Null(TaskbarShapeEngine.RegionFor(new TaskbarShapeSettings { MarginX = 100, MarginY = 4 }, new ScreenRect(0, 0, 150, 40)));      // πολύ στενό
        Assert.Null(TaskbarShapeEngine.RegionFor(new TaskbarShapeSettings { MarginX = 0, MarginY = 30 }, new ScreenRect(0, 0, 1920, 48)));     // πολύ χαμηλό
        Assert.Null(TaskbarShapeEngine.RegionFor(new TaskbarShapeSettings(), default));                                                          // αποτυχία ανάγνωσης rect
    }

    [Fact]
    public void Apply_sets_the_region_and_restore_clears_it()
    {
        var s = new FakeSurface(); var e = new TaskbarShapeEngine(s);
        e.Apply(new TaskbarShapeSettings { Enabled = true, Radius = 14, MarginX = 8, MarginY = 4 });
        Assert.Equal(14, s.Region[(IntPtr)100].Radius);
        Assert.NotNull(s.Region[(IntPtr)100].Region);
        e.RestoreAll();
        Assert.Null(s.Region[(IntPtr)100].Region);
        Assert.Empty(e.Touched);
    }

    [Fact]
    public void The_region_is_recomputed_only_when_the_taskbar_size_changes()
    {
        var s = new FakeSurface(); var e = new TaskbarShapeEngine(s);
        e.Apply(new TaskbarShapeSettings { Enabled = true });
        int calls = s.RegionCalls;
        e.Tick(); e.Tick();
        Assert.Equal(calls, s.RegionCalls);
        s.Rects[(IntPtr)100] = new ScreenRect(0, 1030, 2560, 1080);           // άλλαξε η ανάλυση / το μέγεθος του taskbar
        e.Tick();
        Assert.Equal(calls + 1, s.RegionCalls);
        Assert.Equal(new ScreenRect(8, 4, 2552, 46), s.Region[(IntPtr)100].Region);
    }

    [Fact]
    public void Radius_is_clamped_and_a_vertical_or_hidden_taskbar_gets_no_region()
    {
        var s = new FakeSurface(); var e = new TaskbarShapeEngine(s);
        s.Rects[(IntPtr)100] = new ScreenRect(0, 0, 48, 1080);                // taskbar στην άκρη (κάθετο)
        e.Apply(new TaskbarShapeSettings { Enabled = true, Radius = 9999 });
        Assert.Null(s.Region[(IntPtr)100].Region);
        s.Rects[(IntPtr)100] = new ScreenRect(0, 1040, 1920, 1080); e.Apply(new TaskbarShapeSettings { Enabled = true, Radius = 9999 });
        Assert.Equal(40, s.Region[(IntPtr)100].Radius);
    }

    [Fact]
    public void A_disappeared_taskbar_is_forgotten_and_a_new_one_is_shaped()
    {
        var s = new FakeSurface(); var e = new TaskbarShapeEngine(s);
        e.Apply(new TaskbarShapeSettings { Enabled = true });
        s.Bars[0] = (IntPtr)150; s.Rects[(IntPtr)150] = new ScreenRect(0, 1040, 1920, 1080);
        e.Tick();
        Assert.NotNull(s.Region[(IntPtr)150].Region);
        Assert.DoesNotContain((IntPtr)100, e.Touched);
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Mica για όλα τα παράθυρα: εξαιρέσεις και χρώματα τίτλου
// ---------------------------------------------------------------------------------------------------------------------
internal sealed class NamedDwm : IDwmWindows
{
    public readonly Dictionary<IntPtr, string> Names = new();
    public readonly Dictionary<IntPtr, (int Backdrop, int Caption, int Text)> State = new();
    public IntPtr Foreground { get; set; }
    public NamedDwm Add(int id, string name) { var h = (IntPtr)id; Names[h] = name; State[h] = (0, WindowEffectsEngine.ColorDefault, WindowEffectsEngine.ColorDefault); return this; }
    public IReadOnlyList<IntPtr> TopLevelWindows() => State.Keys.ToList();
    public bool IsEligible(IntPtr h) => State.ContainsKey(h);
    public bool IsAlive(IntPtr h) => State.ContainsKey(h);
    public void SetCorner(IntPtr h, int p) { }
    public void SetBorderColor(IntPtr h, int c) { }
    public void SetDarkTitle(IntPtr h, bool d) { }
    public void SetBackdrop(IntPtr h, int t) => State[h] = State[h] with { Backdrop = t };
    public string? ProcessName(IntPtr h) => Names.TryGetValue(h, out var n) ? n : null;
    public void SetCaptionColor(IntPtr h, int c) => State[h] = State[h] with { Caption = c };
    public void SetCaptionTextColor(IntPtr h, int c) => State[h] = State[h] with { Text = c };
}

public class EverywhereTests
{
    private static (WindowEffectsEngine E, NamedDwm D) Make()
    {
        var d = new NamedDwm().Add(1, "notepad.exe").Add(2, "chrome.exe").Add(3, "game.exe");
        return (new WindowEffectsEngine(d, new FakeTaskbar(false), () => false), d);
    }

    [Theory]
    [InlineData("chrome", "chrome.exe", true)]
    [InlineData("CHROME.EXE", "chrome.exe", true)]
    [InlineData(" \"Chrome.exe\" ", "chrome.exe", true)]
    [InlineData("firefox", "chrome.exe", false)]
    [InlineData("", "chrome.exe", false)]
    public void Process_names_match_without_case_and_with_or_without_exe(string entry, string process, bool excluded)
    {
        var s = new EffectsSettings { ExcludedProcesses = new List<string> { entry } };
        Assert.Equal(excluded, s.IsExcluded(process));
    }

    [Fact]
    public void Excluded_processes_are_never_touched_and_the_others_are()
    {
        var (e, d) = Make();
        e.Apply(new EffectsSettings { Backdrop = BackdropStyle.Mica, ExcludedProcesses = new List<string> { "chrome", "game.exe" } });
        Assert.Equal(2, d.State[(IntPtr)1].Backdrop);
        Assert.Equal(0, d.State[(IntPtr)2].Backdrop);
        Assert.Equal(0, d.State[(IntPtr)3].Backdrop);
    }

    [Fact]
    public void Adding_a_process_to_the_exclusions_restores_its_windows_at_once()
    {
        var (e, d) = Make();
        e.Apply(new EffectsSettings { Backdrop = BackdropStyle.Acrylic });
        Assert.Equal(3, d.State[(IntPtr)2].Backdrop);
        e.Apply(new EffectsSettings { Backdrop = BackdropStyle.Acrylic, ExcludedProcesses = new List<string> { "chrome" } });
        Assert.Equal(0, d.State[(IntPtr)2].Backdrop);
        Assert.Equal(3, d.State[(IntPtr)1].Backdrop);
        Assert.DoesNotContain((IntPtr)2, e.Touched);
    }

    [Fact]
    public void A_window_created_later_for_an_excluded_process_is_skipped()
    {
        var (e, d) = Make();
        e.Apply(new EffectsSettings { Backdrop = BackdropStyle.Mica, ExcludedProcesses = new List<string> { "later.exe" } });
        d.Add(9, "later.exe"); d.Add(10, "other.exe");
        e.ApplyToWindow((IntPtr)9); e.ApplyToWindow((IntPtr)10);
        Assert.Equal(0, d.State[(IntPtr)9].Backdrop);
        Assert.Equal(2, d.State[(IntPtr)10].Backdrop);
    }

    [Fact]
    public void Title_bar_colours_are_applied_converted_and_restored()
    {
        var (e, d) = Make();
        e.Apply(new EffectsSettings { CaptionRgb = 0x112233, CaptionTextRgb = 0xFFEEDD });
        Assert.Equal(0x332211, d.State[(IntPtr)1].Caption);                     // RGB -> COLORREF (BGR)
        Assert.Equal(0xDDEEFF, d.State[(IntPtr)1].Text);
        e.Apply(new EffectsSettings());                                         // σβήνονται από τις ρυθμίσεις
        Assert.Equal(WindowEffectsEngine.ColorDefault, d.State[(IntPtr)1].Caption);
        Assert.Equal(WindowEffectsEngine.ColorDefault, d.State[(IntPtr)1].Text);
    }

    [Fact]
    public void RestoreAll_also_restores_the_title_colours()
    {
        var (e, d) = Make();
        e.Apply(new EffectsSettings { CaptionRgb = 0x112233, Backdrop = BackdropStyle.Tabbed });
        e.RestoreAll();
        Assert.Equal(WindowEffectsEngine.ColorDefault, d.State[(IntPtr)3].Caption);
        Assert.Equal(0, d.State[(IntPtr)3].Backdrop);
    }

    [Fact]
    public void Old_settings_files_without_the_new_fields_still_load()
    {
        var s = System.Text.Json.JsonSerializer.Deserialize<EffectsSettings>("{\"ActiveBorder\":true,\"Corners\":1}")!;
        Assert.True(s.ActiveBorder);
        Assert.NotNull(s.ExcludedProcesses);
        Assert.Null(s.CaptionRgb);
    }
}

public class TrustedExeTests
{
    [Fact] public void SystemExecutablesUseFullPaths()
    {
        Assert.True(System.IO.Path.IsPathFullyQualified(OpsisAppearance.TrustedExe.System32("taskkill.exe")));
        Assert.EndsWith(@"System32\taskkill.exe", OpsisAppearance.TrustedExe.System32("taskkill.exe"), System.StringComparison.OrdinalIgnoreCase);
        Assert.True(OpsisAppearance.TrustedExe.IsBareName("taskkill")); Assert.False(OpsisAppearance.TrustedExe.IsBareName(@"C:\x\a.exe"));
    }
    [Fact] public void OnlyExistingLocalExesLaunchable()
    {
        string exe = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.System), "notepad.exe");
        Assert.True(OpsisAppearance.TrustedExe.IsLaunchable(exe));
        Assert.False(OpsisAppearance.TrustedExe.IsLaunchable(@"\server\share\x.exe")); Assert.False(OpsisAppearance.TrustedExe.IsLaunchable("notepad.exe"));
        Assert.False(OpsisAppearance.TrustedExe.IsLaunchable(@"C:\Windows\win.ini")); Assert.False(OpsisAppearance.TrustedExe.IsLaunchable(null)); Assert.False(OpsisAppearance.TrustedExe.IsLaunchable(@"C:\nope\x.exe"));
    }
}

public class FamilyIconTests
{
    [Fact] public void EveryCatalogueAppHasAnEmbeddedIcon()
    {
        var doc = System.Text.Json.JsonDocument.Parse(Opsis.Suite.SuiteResources.Catalogue());
        var ids = doc.RootElement.GetProperty("apps").EnumerateArray().Select(a => a.GetProperty("id").GetString()!).ToList();
        Assert.Equal(7, ids.Count);
        foreach (var id in ids) { using var ic = Opsis.Suite.SuiteResources.AppIconFor(id, 64); Assert.NotNull(ic); Assert.True(ic!.Width >= 32, id); }
        Assert.Null(Opsis.Suite.SuiteResources.AppIconFor("unknown-app"));
    }
}
