using Microsoft.Win32;
using OpsisAppearance;
using Xunit;

namespace OpsisAppearance.Tests;

internal sealed class FakeRegistry : IRegistryStore
{
    public readonly Dictionary<(string, string), (object Value, RegistryValueKind Kind)> Data = new();
    public object? Get(string subKey, string name) => Data.TryGetValue((subKey, name), out var v) ? v.Value : null;
    public void Set(string subKey, string name, object value, RegistryValueKind kind) => Data[(subKey, name)] = (value, kind);
    public void Delete(string subKey, string name) => Data.Remove((subKey, name));
}

public class SolarTests
{
    // Athens, 21 June: sunrise about 06:00 and sunset about 20:47 local (EEST). Allow 12 minutes of tolerance.
    [Fact]
    public void Athens_midsummer_sunrise_and_sunset_are_plausible()
    {
        var athens = TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time");
        var sun = Solar.Compute(new DateTime(2026, 6, 21), 37.9838, 23.7275, athens);
        Assert.NotNull(sun);
        Assert.InRange(sun!.Value.Sunrise.TimeOfDay.TotalMinutes, 5 * 60 + 48, 6 * 60 + 12);
        Assert.InRange(sun.Value.Sunset.TimeOfDay.TotalMinutes, 20 * 60 + 35, 20 * 60 + 59);
    }

    [Fact]
    public void Athens_midwinter_days_are_shorter_than_summer()
    {
        var athens = TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time");
        var summer = Solar.Compute(new DateTime(2026, 6, 21), 37.9838, 23.7275, athens)!.Value;
        var winter = Solar.Compute(new DateTime(2026, 12, 21), 37.9838, 23.7275, athens)!.Value;
        Assert.True((winter.Sunset - winter.Sunrise) < (summer.Sunset - summer.Sunrise) - TimeSpan.FromHours(4));
        // 21 December in Athens: day length = 2 * acos(...) / 15 = 9 h 31 min (hand-checked); sunrise ~07:37, sunset ~17:09
        Assert.InRange(winter.Sunrise.TimeOfDay.TotalMinutes, 7 * 60 + 29, 7 * 60 + 45);
        Assert.InRange(winter.Sunset.TimeOfDay.TotalMinutes, 17 * 60 + 1, 17 * 60 + 17);
        Assert.InRange((winter.Sunset - winter.Sunrise).TotalMinutes, 9 * 60 + 28, 9 * 60 + 34);
    }

    [Fact]
    public void Polar_night_and_day_return_null()
    {
        Assert.Null(Solar.Compute(new DateTime(2026, 12, 21), 78.2, 15.6, TimeZoneInfo.Utc));   // Svalbard, midwinter
        Assert.Null(Solar.Compute(new DateTime(2026, 6, 21), 78.2, 15.6, TimeZoneInfo.Utc));    // midsummer
    }
}

public class ScheduleTests
{
    [Theory]
    [InlineData("12:00", false)]
    [InlineData("06:59", true)]
    [InlineData("07:00", false)]
    [InlineData("18:59", false)]
    [InlineData("19:00", true)]
    [InlineData("23:30", true)]
    [InlineData("00:10", true)]
    public void Fixed_times(string time, bool expectDark)
    {
        var s = new AppearanceSettings { Mode = ThemeMode.AutoFixed, LightAt = "07:00", DarkAt = "19:00" };
        Assert.Equal(expectDark, AppearanceEngine.ShouldBeDark(s, DateTime.Today + TimeSpan.Parse(time)));
    }

    [Fact]
    public void Fixed_times_that_wrap_midnight_work()   // a night owl: light from 20:00 to 04:00
    {
        var s = new AppearanceSettings { Mode = ThemeMode.AutoFixed, LightAt = "20:00", DarkAt = "04:00" };
        Assert.False(AppearanceEngine.ShouldBeDark(s, DateTime.Today.AddHours(22)));
        Assert.True(AppearanceEngine.ShouldBeDark(s, DateTime.Today.AddHours(12)));
    }

    [Fact]
    public void Sun_mode_uses_the_sun_and_falls_back_to_fixed_times_at_the_poles()
    {
        var athens = TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time");
        var s = new AppearanceSettings { Mode = ThemeMode.AutoSun };
        var summerNoon = new DateTime(2026, 6, 21, 12, 0, 0);
        var summer2130 = new DateTime(2026, 6, 21, 21, 30, 0);
        Assert.False(AppearanceEngine.ShouldBeDark(s, summerNoon, athens));
        Assert.True(AppearanceEngine.ShouldBeDark(s, summer2130, athens));
        // 19:30 is still daylight in an Athens summer but already dark with the fixed fallback
        Assert.False(AppearanceEngine.ShouldBeDark(s, new DateTime(2026, 6, 21, 19, 30, 0), athens));
        var pole = new AppearanceSettings { Mode = ThemeMode.AutoSun, Latitude = 78.2, Longitude = 15.6 };
        Assert.True(AppearanceEngine.ShouldBeDark(pole, new DateTime(2026, 12, 21, 23, 0, 0), TimeZoneInfo.Utc));
    }
}

public class EngineTests
{
    private static (AppearanceEngine Engine, FakeRegistry Reg, List<int> Broadcasts) Make()
    {
        var reg = new FakeRegistry(); var calls = new List<int>();
        return (new AppearanceEngine(reg, () => calls.Add(1)), reg, calls);
    }

    [Fact]
    public void Defaults_when_nothing_is_set_match_windows_defaults()
    {
        var (e, _, _) = Make();
        var cur = e.ReadCurrent();
        Assert.False(cur.Dark);
        Assert.True(cur.Transparency); Assert.True(cur.TaskbarCentered); Assert.True(cur.ShowSearch);
        Assert.False(cur.AccentOnSurfaces);
        Assert.Null(cur.AccentRgb);
    }

    [Fact]
    public void SetDark_writes_both_app_and_system_flags_and_apply_broadcasts_once()
    {
        var (e, reg, calls) = Make();
        e.Apply(new Look { Dark = true, Transparency = false, TaskbarCentered = false });
        Assert.Equal(0, reg.Get(AppearanceEngine.Personalize, "AppsUseLightTheme"));
        Assert.Equal(0, reg.Get(AppearanceEngine.Personalize, "SystemUsesLightTheme"));
        Assert.Equal(0, reg.Get(AppearanceEngine.Personalize, "EnableTransparency"));
        Assert.Equal(0, reg.Get(AppearanceEngine.Advanced, "TaskbarAl"));
        Assert.True(e.IsDark);
        Assert.Single(calls);
    }

    [Fact]
    public void Null_members_are_left_untouched()
    {
        var (e, reg, _) = Make();
        e.Apply(new Look { Name = "empty" });
        Assert.Empty(reg.Data);
    }

    [Fact]
    public void Accent_roundtrips_through_the_dwm_abgr_encoding()
    {
        var (e, reg, _) = Make();
        e.SetAccent(0x0078D4);                      // Windows blue: R=0x00 G=0x78 B=0xD4
        Assert.Equal(unchecked((int)0xFFD47800), reg.Get(AppearanceEngine.Dwm, "AccentColor"));
        Assert.Equal(0x0078D4, e.ReadAccent());
        var palette = (byte[])reg.Get(AppearanceEngine.Accent, "AccentPalette")!;
        Assert.Equal(32, palette.Length);
        Assert.Equal(new byte[] { 0x00, 0x78, 0xD4, 0x00 }, palette[12..16]);   // 4th entry is the accent itself
        Assert.Equal("0", reg.Get(AppearanceEngine.Desktop, "AutoColorization"));
    }

    [Fact]
    public void Palette_has_three_lighter_and_three_darker_shades()
    {
        var p = AppearanceEngine.Palette(0x0078D4);
        int Luma(int i) => p[i * 4] + p[i * 4 + 1] + p[i * 4 + 2];
        for (int i = 0; i < 6; i++) Assert.True(Luma(i) > Luma(i + 1), $"shade {i} should be lighter than {i + 1}");
    }

    [Fact]
    public void Snapshot_then_restore_returns_every_value_including_missing_ones()
    {
        var (e, reg, calls) = Make();
        reg.Set(AppearanceEngine.Personalize, "AppsUseLightTheme", 1, RegistryValueKind.DWord);
        reg.Set(AppearanceEngine.Accent, "AccentPalette", new byte[] { 1, 2, 3, 4 }, RegistryValueKind.Binary);
        // AccentColor deliberately missing
        var snap = e.Snapshot();

        e.Apply(new Look { Dark = true, AccentRgb = 0xE74856, TaskbarCentered = false, Transparency = false });
        Assert.NotNull(reg.Get(AppearanceEngine.Dwm, "AccentColor"));

        e.Restore(snap);
        Assert.Equal(1, reg.Get(AppearanceEngine.Personalize, "AppsUseLightTheme"));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])reg.Get(AppearanceEngine.Accent, "AccentPalette")!);
        Assert.Null(reg.Get(AppearanceEngine.Dwm, "AccentColor"));          // was missing: deleted again
        Assert.Null(reg.Get(AppearanceEngine.Advanced, "TaskbarAl"));
        Assert.Equal(2, calls.Count);                                          // one broadcast for Apply, one for Restore
    }

    [Fact]
    public void Search_box_hidden_uses_mode_zero()
    {
        var (e, reg, _) = Make();
        e.Apply(new Look { ShowSearch = false });
        Assert.Equal(0, reg.Get(AppearanceEngine.Search, "SearchboxTaskbarMode"));
        Assert.False(e.ReadCurrent().ShowSearch);
    }

    [Fact]
    public void Dominant_accent_prefers_the_colourful_region_over_grey()
    {
        using var bmp = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(120, 120, 120));                                           // grey background
            using var teal = new SolidBrush(Color.FromArgb(0, 160, 170));
            g.FillRectangle(teal, 0, 0, 40, 100);                                              // a teal band
        }
        int? c = AppearanceEngine.DominantAccent(bmp);
        Assert.NotNull(c);
        int r = (c!.Value >> 16) & 0xFF, gr = (c.Value >> 8) & 0xFF, b = c.Value & 0xFF;
        Assert.True(gr > r + 60 && b > r + 60, $"expected a teal accent, got {r},{gr},{b}");
    }

    [Fact]
    public void Dominant_accent_of_a_pure_grey_image_is_null()
    {
        using var bmp = new Bitmap(50, 50);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Gray);
        Assert.Null(AppearanceEngine.DominantAccent(bmp));
    }
}

public class PluginContractTests
{
    [Fact]
    public void Plugin_exposes_navigation_tray_and_about_without_touching_the_real_registry()
    {
        var plugin = new OpsisAppearancePlugin(new FakeRegistry());
        plugin.SetLanguage("el");
        plugin.SetTheme(true, 0x121620, 0x1c2230, 0x343e54, 0xebf0fa, 0x96a0b4, 0x00c8e6, 1.0);    // no alpha byte: must not throw
        Assert.Equal("giorstergiopoulos.opsisappearance", plugin.Id);
        Assert.Single(plugin.GetNavigationItems());
        var keys = plugin.GetTrayMenuItems().Select(i => i.Key).ToArray();
        Assert.Equal(new[] { "appearance.toggle", "appearance.open", "appearance.welcome", "appearance.update", "appearance.family" }, keys);      // οδηγός + οικογένεια Opsis: μόνο εγγραφές μενού, τίποτα δεν ξεκινά μόνο του
        Assert.NotEmpty(plugin.GetAboutInfo().Sections);
    }
}
