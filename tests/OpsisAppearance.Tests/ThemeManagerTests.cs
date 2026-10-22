using System.IO.Compression;
using System.Text;
using Microsoft.Win32;
using OpsisAppearance;
using Xunit;

namespace OpsisAppearance.Tests;

internal sealed class FakeWallpaper : IWallpaperApi
{
    public string? Path; public WallpaperStyle Style = WallpaperStyle.Fill; public int SetCalls;
    public (string? Path, WallpaperStyle Style) Current() => (Path, Style);
    public void Set(string path, WallpaperStyle style) { Path = path; Style = style; SetCalls++; }
}

public class ThemeManagerTests : IDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "opsis_theme_test_" + Guid.NewGuid().ToString("N")[..8]);
    private readonly ThemeLibrary _lib;
    public ThemeManagerTests() { Directory.CreateDirectory(_root); _lib = new ThemeLibrary(System.IO.Path.Combine(_root, "themes")); }
    public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }

    private string MakeWallpaper(string name = "pic.png", int bytes = 2048)
    {
        string p = System.IO.Path.Combine(_root, name);
        File.WriteAllBytes(p, new byte[bytes]);
        return p;
    }

    [Theory]
    [InlineData("My Theme", "My Theme")]
    [InlineData("../evil", "_evil")]
    [InlineData("a/b\\c:d*e?f", "a_b_c_d_e_f")]
    [InlineData("..", "")]
    [InlineData("   ", "")]
    [InlineData("Νύχτα", "Νύχτα")]
    public void SafeName_never_lets_a_path_through(string input, string expected) => Assert.Equal(expected, ThemeLibrary.SafeName(input));

    [Fact]
    public void SafeName_caps_the_length() => Assert.True(ThemeLibrary.SafeName(new string('x', 500)).Length <= 60);

    [Fact]
    public void Builtin_themes_are_listed_first_and_are_not_deletable()
    {
        var list = _lib.List();
        Assert.Contains(list, t => t.Name == "Night" && t.BuiltIn);
        Assert.Contains(list, t => t.Name == "Neon" && t.Effects!.ActiveBorder);
        Assert.False(_lib.Delete("Night"));
    }

    [Fact]
    public void Save_then_list_roundtrips_everything_including_the_wallpaper()
    {
        var t = new OpsisTheme
        {
            Name = "Sunset", Description = "warm", WallpaperStyle = WallpaperStyle.Fit,
            Look = new Look { Dark = true, AccentRgb = 0xE74856, Transparency = false, TaskbarCentered = false },
            Effects = new EffectsSettings { ActiveBorder = true, BorderRgb = 0xFF8C00, Corners = CornerStyle.Square, Backdrop = BackdropStyle.Mica },
        };
        _lib.Save(t, MakeWallpaper("sunset.jpeg"));
        var back = _lib.List().Single(x => x.Name == "Sunset");
        Assert.True(back.Look.Dark); Assert.Equal(0xE74856, back.Look.AccentRgb); Assert.False(back.Look.TaskbarCentered);
        Assert.Equal(CornerStyle.Square, back.Effects!.Corners); Assert.Equal(0xFF8C00, back.Effects.BorderRgb);
        Assert.Equal(WallpaperStyle.Fit, back.WallpaperStyle);
        Assert.Equal("wallpaper.jpg", back.WallpaperFile);                         // .jpeg normalised
        Assert.True(File.Exists(_lib.WallpaperPath(back)));
    }

    [Fact]
    public void Save_rejects_unsafe_names_builtin_names_bad_wallpaper_types_and_huge_files()
    {
        Assert.Throws<ArgumentException>(() => _lib.Save(new OpsisTheme { Name = ".." }));
        Assert.Throws<InvalidOperationException>(() => _lib.Save(new OpsisTheme { Name = "night" }));
        string exe = System.IO.Path.Combine(_root, "evil.exe"); File.WriteAllBytes(exe, new byte[10]);
        Assert.Throws<InvalidOperationException>(() => _lib.Save(new OpsisTheme { Name = "X" }, exe));
        string big = System.IO.Path.Combine(_root, "big.png");
        using (var fs = File.Create(big)) fs.SetLength(ThemeLibrary.MaxWallpaperBytes + 1);
        Assert.Throws<InvalidOperationException>(() => _lib.Save(new OpsisTheme { Name = "Y" }, big));
    }

    [Fact]
    public void Delete_removes_only_our_own_theme_folders()
    {
        _lib.Save(new OpsisTheme { Name = "Temp" });
        Assert.True(_lib.Delete("Temp"));
        Assert.DoesNotContain(_lib.List(), t => t.Name == "Temp");
        Assert.False(_lib.Delete(".."));                                           // would be the root folder: refused
        Assert.True(Directory.Exists(_root));
        Directory.CreateDirectory(System.IO.Path.Combine(_lib.Root, "NotATheme"));
        File.WriteAllText(System.IO.Path.Combine(_lib.Root, "NotATheme", "keep.txt"), "x");
        Assert.False(_lib.Delete("NotATheme"));                                    // no theme.json: untouched
        Assert.True(File.Exists(System.IO.Path.Combine(_lib.Root, "NotATheme", "keep.txt")));
    }

    [Fact]
    public void Export_then_import_into_another_library_roundtrips()
    {
        var t = new OpsisTheme { Name = "Share me", Look = new Look { Dark = true, AccentRgb = 0x112233 }, Effects = new EffectsSettings { Corners = CornerStyle.Round } };
        _lib.Save(t, MakeWallpaper("w.png", 5000));
        string zip = System.IO.Path.Combine(_root, "share.opsistheme");
        _lib.Export(_lib.List().Single(x => x.Name == "Share me"), zip);

        var other = new ThemeLibrary(System.IO.Path.Combine(_root, "other"));
        var imported = other.Import(zip);
        Assert.Equal("Share me", imported.Name);
        Assert.Equal(0x112233, imported.Look.AccentRgb);
        Assert.Equal(CornerStyle.Round, imported.Effects!.Corners);
        Assert.Equal(5000, new FileInfo(other.WallpaperPath(imported)!).Length);
    }

    private string Zip(params (string Name, byte[] Data)[] entries)
    {
        string p = System.IO.Path.Combine(_root, Guid.NewGuid().ToString("N")[..6] + ".opsistheme");
        using var z = ZipFile.Open(p, ZipArchiveMode.Create);
        foreach (var (name, data) in entries) { var e = z.CreateEntry(name); using var s = e.Open(); s.Write(data); }
        return p;
    }
    private static byte[] J(string s) => Encoding.UTF8.GetBytes(s);

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("evil.exe")]
    [InlineData("sub/theme.json")]
    [InlineData("..\\evil.png")]
    public void Import_rejects_any_entry_that_is_not_theme_json_or_a_plain_wallpaper(string extraEntry)
    {
        string zip = Zip(("theme.json", J("{\"Name\":\"X\"}")), (extraEntry, new byte[10]));
        Assert.Throws<InvalidDataException>(() => _lib.Import(zip));
        Assert.False(File.Exists(System.IO.Path.Combine(_root, "evil.txt")));
        Assert.False(File.Exists(System.IO.Path.Combine(_root, "evil.exe")));
    }

    [Fact]
    public void Import_rejects_a_package_without_theme_json_or_with_garbage_json_or_too_many_files()
    {
        Assert.Throws<InvalidDataException>(() => _lib.Import(Zip(("wallpaper.png", new byte[10]))));
        Assert.ThrowsAny<Exception>(() => _lib.Import(Zip(("theme.json", J("{ not json")))));
        Assert.Throws<InvalidDataException>(() => _lib.Import(Zip(("theme.json", J("{}")), ("wallpaper.png", new byte[1]), ("wallpaper.jpg", new byte[1]), ("wallpaper.bmp", new byte[1]), ("wallpaper.jpeg", new byte[1]))));
    }

    [Fact]
    public void Import_sanitises_hostile_values_inside_theme_json()
    {
        string zip = Zip(("theme.json", J("{\"Name\":\"..\\\\..\\\\Windows\\\\evil\",\"WallpaperFile\":\"..\\\\..\\\\x.png\",\"WallpaperStyle\":99,\"Effects\":{\"Corners\":77,\"Backdrop\":-5,\"BorderRgb\":-1}}")));
        var t = _lib.Import(zip);
        Assert.DoesNotContain("..", t.Name);
        Assert.DoesNotContain("\\", t.Name);
        Assert.Null(t.WallpaperFile);
        Assert.Equal(WallpaperStyle.Fill, t.WallpaperStyle);
        Assert.Equal(CornerStyle.Default, t.Effects!.Corners);
        Assert.Equal(BackdropStyle.Default, t.Effects.Backdrop);
        Assert.Equal(0xFFFFFF, t.Effects.BorderRgb);
        Assert.StartsWith(System.IO.Path.GetFullPath(_lib.Root), System.IO.Path.GetFullPath(_lib.FolderOf(t.Name)));
    }

    [Fact]
    public void Import_of_a_name_that_matches_a_builtin_gets_a_suffix_instead_of_shadowing_it()
    {
        var t = _lib.Import(Zip(("theme.json", J("{\"Name\":\"Night\"}"))));
        Assert.Equal("Night (imported)", t.Name);
        Assert.Single(_lib.List(), x => x.Name == "Night");
    }

    [Fact]
    public void Import_counts_real_bytes_not_the_size_claimed_in_the_zip_header()
    {
        // 41 MB of zeros compresses to a few KB: the header size is checked AND the stream is counted while copying.
        var zipPath = System.IO.Path.Combine(_root, "bomb.opsistheme");
        using (var z = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var j = z.CreateEntry("theme.json"); using (var s = j.Open()) s.Write(J("{\"Name\":\"Bomb\"}"));
            var w = z.CreateEntry("wallpaper.png", CompressionLevel.Optimal); using (var s = w.Open()) { var chunk = new byte[1024 * 1024]; for (int i = 0; i < 41; i++) s.Write(chunk); }
        }
        Assert.Throws<InvalidDataException>(() => _lib.Import(zipPath));
    }

    // ---- applying ---------------------------------------------------------------------------------------------

    private static (ThemeApplier Applier, FakeRegistry Reg, FakeWallpaper Wall, List<EffectsSettings> Fx) MakeApplier(ThemeLibrary lib)
    {
        var reg = new FakeRegistry(); var wall = new FakeWallpaper(); var fx = new List<EffectsSettings>();
        return (new ThemeApplier(new AppearanceEngine(reg, () => { }), e => fx.Add(e), wall, lib), reg, wall, fx);
    }

    [Fact]
    public void Apply_sets_the_look_the_effects_and_the_wallpaper_with_its_style()
    {
        var (ap, reg, wall, fx) = MakeApplier(_lib);
        _lib.Save(new OpsisTheme { Name = "Full", WallpaperStyle = WallpaperStyle.Center, Look = new Look { Dark = true, AccentRgb = 0x0078D4 }, Effects = new EffectsSettings { Corners = CornerStyle.Square } }, MakeWallpaper());
        var theme = _lib.List().Single(t => t.Name == "Full");
        var report = ap.Apply(theme);
        Assert.True(report.LookApplied && report.EffectsApplied && report.WallpaperApplied);
        Assert.Empty(report.Errors);
        Assert.Equal(0, reg.Get(AppearanceEngine.Personalize, "AppsUseLightTheme"));
        Assert.Equal(CornerStyle.Square, fx.Single().Corners);
        Assert.Equal(WallpaperStyle.Center, wall.Style);
        Assert.EndsWith("wallpaper.png", wall.Path);
    }

    [Fact]
    public void A_theme_without_effects_does_not_touch_the_current_effects()
    {
        var (ap, _, _, fx) = MakeApplier(_lib);
        ap.Apply(_lib.List().Single(t => t.Name == "Classic taskbar"));
        Assert.Empty(fx);
    }

    [Fact]
    public void One_failing_step_is_reported_and_the_others_still_run()
    {
        var wall = new FakeWallpaper(); var reg = new FakeRegistry(); var fx = new List<EffectsSettings>();
        var ap = new ThemeApplier(new AppearanceEngine(reg, () => throw new InvalidOperationException("broadcast failed")), e => fx.Add(e), wall, _lib);
        _lib.Save(new OpsisTheme { Name = "Partial", Look = new Look { Dark = true }, Effects = new EffectsSettings() }, MakeWallpaper());
        var report = ap.Apply(_lib.List().Single(t => t.Name == "Partial"));
        Assert.False(report.LookApplied);
        Assert.Contains(report.Errors, e => e.StartsWith("Look:"));
        Assert.True(report.EffectsApplied && report.WallpaperApplied);
    }

    [Fact]
    public void A_missing_wallpaper_file_is_reported_not_thrown()
    {
        var (ap, _, wall, _) = MakeApplier(_lib);
        _lib.Save(new OpsisTheme { Name = "Gone" }, MakeWallpaper());
        File.Delete(System.IO.Path.Combine(_lib.FolderOf("Gone"), "wallpaper.png"));
        var report = ap.Apply(_lib.List().Single(t => t.Name == "Gone"));
        Assert.Equal(0, wall.SetCalls);
        Assert.False(report.WallpaperApplied);
    }

    [Fact]
    public void CaptureCurrent_reads_the_live_look_effects_and_wallpaper()
    {
        var (ap, reg, wall, _) = MakeApplier(_lib);
        reg.Set(AppearanceEngine.Personalize, "AppsUseLightTheme", 0, RegistryValueKind.DWord);
        reg.Set(AppearanceEngine.Advanced, "TaskbarAl", 0, RegistryValueKind.DWord);
        wall.Path = MakeWallpaper("current.png"); wall.Style = WallpaperStyle.Stretch;
        var (theme, source) = ap.CaptureCurrent("Snapshot", new EffectsSettings { ActiveBorder = true, Corners = CornerStyle.RoundSmall }, includeWallpaper: true);
        Assert.True(theme.Look.Dark); Assert.False(theme.Look.TaskbarCentered);
        Assert.True(theme.Effects!.ActiveBorder); Assert.Equal(CornerStyle.RoundSmall, theme.Effects.Corners);
        Assert.Equal(wall.Path, source); Assert.Equal(WallpaperStyle.Stretch, theme.WallpaperStyle);
        var (_, none) = ap.CaptureCurrent("NoWall", new EffectsSettings(), includeWallpaper: false);
        Assert.Null(none);
    }

    [Theory]
    [InlineData(WallpaperStyle.Fill, "10", "0")]
    [InlineData(WallpaperStyle.Fit, "6", "0")]
    [InlineData(WallpaperStyle.Stretch, "2", "0")]
    [InlineData(WallpaperStyle.Tile, "0", "1")]
    [InlineData(WallpaperStyle.Center, "0", "0")]
    [InlineData(WallpaperStyle.Span, "22", "0")]
    public void Wallpaper_style_registry_mapping_roundtrips(WallpaperStyle style, string regStyle, string regTile)
    {
        Assert.Equal((regStyle, regTile), RealWallpaper.ToRegistry(style));
        Assert.Equal(style, RealWallpaper.FromRegistry(regStyle, regTile));
    }
}
