using Microsoft.Win32;
using OpsisAppearance;
using Xunit;

namespace OpsisAppearance.Tests;

public class ShellTweakTests
{
    private static (AppearanceEngine Engine, FakeRegistry Reg) Make()
    {
        var reg = new FakeRegistry();
        return (new AppearanceEngine(reg, () => { }), reg);
    }

    [Fact]
    public void File_extensions_and_hidden_files_map_to_the_explorer_values()
    {
        var (e, reg) = Make();
        e.Apply(new Look { ShowFileExtensions = true, ShowHiddenFiles = true });
        Assert.Equal(0, reg.Data[(AppearanceEngine.Advanced, "HideFileExt")].Value);
        Assert.Equal(1, reg.Data[(AppearanceEngine.Advanced, "Hidden")].Value);
        e.Apply(new Look { ShowFileExtensions = false, ShowHiddenFiles = false });
        Assert.Equal(1, reg.Data[(AppearanceEngine.Advanced, "HideFileExt")].Value);
        Assert.Equal(2, reg.Data[(AppearanceEngine.Advanced, "Hidden")].Value);
    }

    [Fact]
    public void Clock_seconds_taskbar_combine_and_animations_are_written_and_read_back()
    {
        var (e, reg) = Make();
        e.Apply(new Look { ClockSeconds = true, TaskbarCombine = 2, Animations = false });
        var cur = e.ReadCurrent();
        Assert.True(cur.ClockSeconds);
        Assert.Equal(2, cur.TaskbarCombine);
        Assert.False(cur.Animations);
        Assert.Equal("0", reg.Data[(AppearanceEngine.WindowMetrics, "MinAnimate")].Value);
        Assert.Equal(RegistryValueKind.String, reg.Data[(AppearanceEngine.WindowMetrics, "MinAnimate")].Kind);
    }

    [Fact]
    public void Taskbar_combine_is_clamped()
    {
        var (e, reg) = Make();
        e.Apply(new Look { TaskbarCombine = 9 });
        Assert.Equal(2, reg.Data[(AppearanceEngine.Advanced, "TaskbarGlomLevel")].Value);
        e.Apply(new Look { TaskbarCombine = -3 });
        Assert.Equal(0, reg.Data[(AppearanceEngine.Advanced, "TaskbarGlomLevel")].Value);
    }

    [Fact]
    public void Missing_values_read_as_the_windows_defaults()
    {
        var (e, _) = Make();
        var cur = e.ReadCurrent();
        Assert.False(cur.ShowFileExtensions);   // Windows hides extensions by default
        Assert.False(cur.ShowHiddenFiles);
        Assert.False(cur.ClockSeconds);
        Assert.Equal(0, cur.TaskbarCombine);
        Assert.True(cur.Animations);
    }

    [Fact]
    public void The_new_values_are_part_of_the_backup_and_are_restored()
    {
        var (e, reg) = Make();
        reg.Data[(AppearanceEngine.Advanced, "HideFileExt")] = (1, RegistryValueKind.DWord);
        var backup = e.Snapshot();
        e.Apply(new Look { ShowFileExtensions = true, ClockSeconds = true, TaskbarCombine = 1, Animations = false });
        e.Restore(backup);
        Assert.Equal(1, reg.Data[(AppearanceEngine.Advanced, "HideFileExt")].Value);
        Assert.False(reg.Data.ContainsKey((AppearanceEngine.Advanced, "ShowSecondsInSystemClock")));   // was missing -> deleted again
        Assert.False(reg.Data.ContainsKey((AppearanceEngine.WindowMetrics, "MinAnimate")));
    }
}

public class SlideshowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oa_slide_" + Guid.NewGuid().ToString("N"));
    public SlideshowTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private string Make(string name, int bytes = 10) { var p = Path.Combine(_dir, name); File.WriteAllBytes(p, new byte[bytes]); return p; }

    [Fact]
    public void Images_lists_only_pictures_sorted_and_skips_empty_files()
    {
        Make("b.jpg"); Make("a.png"); Make("notes.txt"); Make("empty.png", 0); Make("c.BMP");
        var names = WallpaperSlideshow.Images(_dir).Select(Path.GetFileName).ToArray();
        Assert.Equal(new[] { "a.png", "b.jpg", "c.BMP" }, names);
    }

    [Fact]
    public void Images_of_a_missing_folder_is_empty_not_an_exception()
    {
        Assert.Empty(WallpaperSlideshow.Images(Path.Combine(_dir, "nope")));
        Assert.Empty(WallpaperSlideshow.Images(null));
        Assert.Empty(WallpaperSlideshow.Images(""));
    }

    [Fact]
    public void Pick_in_order_wraps_around()
    {
        var l = new List<string> { "a", "b", "c" };
        Assert.Equal("a", WallpaperSlideshow.Pick(l, null, false));
        Assert.Equal("b", WallpaperSlideshow.Pick(l, "a", false));
        Assert.Equal("a", WallpaperSlideshow.Pick(l, "c", false));
    }

    [Fact]
    public void Pick_shuffle_never_repeats_the_last_picture()
    {
        var l = new List<string> { "a", "b", "c" };
        var rng = new Random(7);
        string? last = "b";
        for (int i = 0; i < 200; i++)
        {
            var next = WallpaperSlideshow.Pick(l, last, true, rng);
            Assert.NotEqual(last, next);
            last = next;
        }
    }

    [Fact]
    public void Pick_with_one_or_no_image()
    {
        Assert.Null(WallpaperSlideshow.Pick(new List<string>(), null, true));
        Assert.Equal("only", WallpaperSlideshow.Pick(new List<string> { "only" }, "only", true));
    }

    [Fact]
    public void Due_follows_the_interval_and_survives_a_clock_set_back()
    {
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        var s = new SlideshowSettings { Enabled = true, IntervalMinutes = 30 };
        Assert.True(WallpaperSlideshow.Due(s, now));                       // never changed yet
        s.LastChangeUtc = now.AddMinutes(-10); Assert.False(WallpaperSlideshow.Due(s, now));
        s.LastChangeUtc = now.AddMinutes(-31); Assert.True(WallpaperSlideshow.Due(s, now));
        s.LastChangeUtc = now.AddMinutes(+5); Assert.True(WallpaperSlideshow.Due(s, now));   // the clock went back: change now
        s.Enabled = false; Assert.False(WallpaperSlideshow.Due(s, now));
        s.Enabled = true; s.IntervalMinutes = 0; s.LastChangeUtc = now.AddSeconds(-30); Assert.False(WallpaperSlideshow.Due(s, now));   // minimum is 1 minute
    }
}
