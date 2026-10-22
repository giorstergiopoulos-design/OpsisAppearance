using OpsisAppearance;
using Xunit;

namespace OpsisAppearance.Tests;

internal sealed class FakeDwm : IDwmWindows
{
    public readonly Dictionary<IntPtr, (int Corner, int Border, bool? Dark, int Backdrop)> State = new();
    public readonly HashSet<IntPtr> Eligible = new();
    public readonly HashSet<IntPtr> Dead = new();
    public IntPtr Foreground { get; set; }

    public FakeDwm Add(int id, bool eligible = true)
    {
        var h = (IntPtr)id; State[h] = (0, WindowEffectsEngine.ColorDefault, null, 0);
        if (eligible) Eligible.Add(h);
        return this;
    }
    public IReadOnlyList<IntPtr> TopLevelWindows() => State.Keys.ToList();
    public bool IsEligible(IntPtr h) => Eligible.Contains(h) && !Dead.Contains(h);
    public bool IsAlive(IntPtr h) => State.ContainsKey(h) && !Dead.Contains(h);
    public void SetCorner(IntPtr h, int p) => State[h] = State[h] with { Corner = p };
    public void SetBorderColor(IntPtr h, int c) => State[h] = State[h] with { Border = c };
    public void SetDarkTitle(IntPtr h, bool d) => State[h] = State[h] with { Dark = d };
    public void SetBackdrop(IntPtr h, int t) => State[h] = State[h] with { Backdrop = t };
}

internal sealed class FakeTaskbar : ITaskbarControl
{
    public bool IsAutoHide { get; private set; }
    public int Calls;
    public FakeTaskbar(bool start) { IsAutoHide = start; }
    public void SetAutoHide(bool on) { IsAutoHide = on; Calls++; }
}

public class WindowEffectsTests
{
    private static (WindowEffectsEngine Engine, FakeDwm Dwm, FakeTaskbar Bar) Make(bool systemDark = false, bool barAutoHide = false)
    {
        var dwm = new FakeDwm().Add(1).Add(2).Add(3).Add(99, eligible: false);      // 99 = Shell/tool window: never touched
        var bar = new FakeTaskbar(barAutoHide);
        return (new WindowEffectsEngine(dwm, bar, () => systemDark), dwm, bar);
    }

    [Fact]
    public void Corner_style_is_applied_to_eligible_windows_only()
    {
        var (e, dwm, _) = Make();
        e.Apply(new EffectsSettings { Corners = CornerStyle.Square });
        Assert.Equal(1, dwm.State[(IntPtr)1].Corner);
        Assert.Equal(1, dwm.State[(IntPtr)3].Corner);
        Assert.Equal(0, dwm.State[(IntPtr)99].Corner);                     // ineligible: untouched
        Assert.Equal(3, e.Touched.Count);
    }

    [Theory]
    [InlineData(CornerStyle.Round, 2)]
    [InlineData(CornerStyle.RoundSmall, 3)]
    [InlineData(CornerStyle.Square, 1)]
    public void Corner_preference_values_match_the_dwm_enum(CornerStyle style, int expected)
    {
        var (e, dwm, _) = Make();
        e.Apply(new EffectsSettings { Corners = style });
        Assert.Equal(expected, dwm.State[(IntPtr)2].Corner);
    }

    [Fact]
    public void Active_border_follows_the_foreground_window_and_clears_the_previous_one()
    {
        var (e, dwm, _) = Make();
        e.AccentRgb = 0x0078D4;
        dwm.Foreground = (IntPtr)1;
        e.Apply(new EffectsSettings { ActiveBorder = true });
        int accentRef = WindowEffectsEngine.ToColorRef(0x0078D4);
        Assert.Equal(accentRef, dwm.State[(IntPtr)1].Border);
        e.OnForegroundChanged((IntPtr)2);
        Assert.Equal(WindowEffectsEngine.ColorDefault, dwm.State[(IntPtr)1].Border);   // previous window back to default
        Assert.Equal(accentRef, dwm.State[(IntPtr)2].Border);
        e.OnForegroundChanged((IntPtr)99);                                             // ineligible foreground (e.g. the taskbar): no border anywhere
        Assert.Equal(WindowEffectsEngine.ColorDefault, dwm.State[(IntPtr)2].Border);
    }

    [Fact]
    public void Border_colour_uses_colorref_byte_order_and_a_custom_colour_overrides_the_accent()
    {
        Assert.Equal(0x0000FF, WindowEffectsEngine.ToColorRef(0xFF0000));              // RGB red -> COLORREF 0x0000FF (BGR)
        Assert.Equal(0xD47800, WindowEffectsEngine.ToColorRef(0x0078D4));
        var (e, dwm, _) = Make();
        dwm.Foreground = (IntPtr)1;
        e.Apply(new EffectsSettings { ActiveBorder = true, BorderRgb = 0xE74856 });
        Assert.Equal(WindowEffectsEngine.ToColorRef(0xE74856), dwm.State[(IntPtr)1].Border);
    }

    [Fact]
    public void Dark_title_bars_follow_the_theme_or_are_forced()
    {
        var (e, dwm, _) = Make(systemDark: false);
        e.Apply(new EffectsSettings { DarkTitle = DarkTitleBars.On });
        Assert.True(dwm.State[(IntPtr)1].Dark);
        e.Apply(new EffectsSettings { DarkTitle = DarkTitleBars.FollowTheme });
        Assert.False(dwm.State[(IntPtr)1].Dark);                                      // theme is light
        var (e2, dwm2, _) = Make(systemDark: true);
        e2.Apply(new EffectsSettings { DarkTitle = DarkTitleBars.FollowTheme });
        Assert.True(dwm2.State[(IntPtr)1].Dark);
    }

    [Theory]
    [InlineData(BackdropStyle.None, 1)]
    [InlineData(BackdropStyle.Mica, 2)]
    [InlineData(BackdropStyle.Acrylic, 3)]
    [InlineData(BackdropStyle.Tabbed, 4)]
    public void Backdrop_values_match_the_dwm_enum(BackdropStyle style, int expected)
    {
        var (e, dwm, _) = Make();
        e.Apply(new EffectsSettings { Backdrop = style });
        Assert.Equal(expected, dwm.State[(IntPtr)3].Backdrop);
    }

    [Fact]
    public void New_windows_that_appear_later_get_the_effects()
    {
        var (e, dwm, _) = Make();
        e.Apply(new EffectsSettings { Corners = CornerStyle.Round });
        dwm.Add(50);
        Assert.Equal(0, dwm.State[(IntPtr)50].Corner);
        e.ApplyToWindow((IntPtr)50);                                                   // what the EVENT_OBJECT_SHOW hook does
        Assert.Equal(2, dwm.State[(IntPtr)50].Corner);
    }

    [Fact]
    public void Turning_an_effect_off_restores_only_that_effect()
    {
        var (e, dwm, _) = Make();
        e.Apply(new EffectsSettings { Corners = CornerStyle.Square, Backdrop = BackdropStyle.Mica });
        e.Apply(new EffectsSettings { Corners = CornerStyle.Default, Backdrop = BackdropStyle.Mica });
        Assert.Equal(0, dwm.State[(IntPtr)1].Corner);                                 // restored
        Assert.Equal(2, dwm.State[(IntPtr)1].Backdrop);                               // still on
    }

    [Fact]
    public void RestoreAll_puts_every_touched_window_back_and_forgets_them()
    {
        var (e, dwm, _) = Make();
        dwm.Foreground = (IntPtr)2;
        e.Apply(new EffectsSettings { Corners = CornerStyle.Square, ActiveBorder = true, Backdrop = BackdropStyle.Acrylic });
        e.RestoreAll();
        foreach (var h in new[] { (IntPtr)1, (IntPtr)2, (IntPtr)3 })
        {
            Assert.Equal(0, dwm.State[h].Corner);
            Assert.Equal(WindowEffectsEngine.ColorDefault, dwm.State[h].Border);
            Assert.Equal(0, dwm.State[h].Backdrop);
        }
        Assert.Empty(e.Touched);
    }

    [Fact]
    public void Closed_windows_are_skipped_without_errors()
    {
        var (e, dwm, _) = Make();
        e.Apply(new EffectsSettings { Corners = CornerStyle.Square });
        dwm.Dead.Add((IntPtr)2);
        e.Apply(new EffectsSettings { Corners = CornerStyle.Default });
        e.RestoreAll();
        Assert.DoesNotContain((IntPtr)2, e.Touched);
    }

    [Fact]
    public void Taskbar_autohide_is_restored_to_the_users_original_state_not_simply_turned_off()
    {
        var (e, _, bar) = Make(barAutoHide: true);                                    // the user already had auto-hide ON
        e.Apply(new EffectsSettings { AutoHideTaskbar = true });
        Assert.True(bar.IsAutoHide);
        Assert.Equal(0, bar.Calls);                                                    // nothing to change
        e.Apply(new EffectsSettings { AutoHideTaskbar = false });
        Assert.True(bar.IsAutoHide);                                                   // still on: it was on before we came
    }

    [Fact]
    public void Taskbar_autohide_turns_on_and_back_off_when_it_was_off_originally()
    {
        var (e, _, bar) = Make(barAutoHide: false);
        e.Apply(new EffectsSettings { AutoHideTaskbar = true });
        Assert.True(bar.IsAutoHide);
        e.RestoreAll();
        Assert.False(bar.IsAutoHide);
    }

    [Fact]
    public void Default_settings_touch_nothing()
    {
        var (e, dwm, bar) = Make();
        e.Apply(new EffectsSettings());
        Assert.Empty(e.Touched);
        Assert.All(dwm.State.Values, v => Assert.Equal((0, WindowEffectsEngine.ColorDefault, (bool?)null, 0), v));
        Assert.Equal(0, bar.Calls);
        Assert.False(new EffectsSettings().AnyWindowEffect);
    }

    [Fact]
    public void Effects_settings_roundtrip_through_json()
    {
        var s = new AppearanceSettings { Effects = new EffectsSettings { ActiveBorder = true, BorderRgb = 0x123456, Corners = CornerStyle.RoundSmall, DarkTitle = DarkTitleBars.FollowTheme, Backdrop = BackdropStyle.Tabbed, AutoHideTaskbar = true } };
        var back = System.Text.Json.JsonSerializer.Deserialize<AppearanceSettings>(System.Text.Json.JsonSerializer.Serialize(s))!;
        Assert.Equal(CornerStyle.RoundSmall, back.Effects.Corners);
        Assert.Equal(0x123456, back.Effects.BorderRgb);
        Assert.True(back.Effects.AutoHideTaskbar);
        Assert.Equal(BackdropStyle.Tabbed, back.Effects.Backdrop);
    }
}
