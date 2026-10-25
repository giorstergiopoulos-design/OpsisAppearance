using OpsisAppearance;
using Xunit;

namespace OpsisAppearance.Tests;

public class ThemeSafetyTests
{
    private static string Write(string body)
    {
        string f = Path.Combine(Path.GetTempPath(), "opsis_ts_" + Guid.NewGuid().ToString("N") + ".theme");
        File.WriteAllText(f, body);
        return f;
    }

    [Fact]
    public void Reads_the_visual_style_path_and_expands_variables()
    {
        string f = Write("[Theme]\nDisplayName=X\n[VisualStyles]\nPath=%SystemRoot%\\Resources\\Themes\\aero\\aero.msstyles\nColorStyle=NormalColor\n");
        try { Assert.Equal(Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, @"Resources\Themes\aero\aero.msstyles"), ThemeSafety.ReadVisualStylePath(f)); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void A_theme_without_a_visual_style_has_nothing_to_warn_about()
    {
        string f = Write("[Theme]\nDisplayName=X\n[Control Panel\\Desktop]\nWallpaper=a.jpg\n");
        try
        {
            var r = ThemeSafety.Check(f, patcherInstalled: false);
            Assert.Equal(ThemeSafety.VisualStyleState.None, r.State);
            Assert.False(r.NeedsWarning);
        }
        finally { File.Delete(f); }
    }

    [Theory]
    [InlineData(ThemeSafety.VisualStyleState.Unsigned, false, true)]
    [InlineData(ThemeSafety.VisualStyleState.SignedByOthers, false, true)]
    [InlineData(ThemeSafety.VisualStyleState.SignedByMicrosoft, false, false)]
    [InlineData(ThemeSafety.VisualStyleState.Unsigned, true, false)]       // a patcher is installed: Windows will accept it
    [InlineData(ThemeSafety.VisualStyleState.Missing, false, false)]
    public void Warns_only_when_windows_would_refuse_the_style(ThemeSafety.VisualStyleState state, bool patched, bool warn)
    {
        string f = Write("[VisualStyles]\nPath=C:\\x\\y.msstyles\n");
        try { Assert.Equal(warn, ThemeSafety.Check(f, _ => state, patched).NeedsWarning); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void A_missing_file_is_reported_as_missing()
    {
        Assert.Equal(ThemeSafety.VisualStyleState.Missing, ThemeSafety.Signature(@"C:\does\not\exist.msstyles"));
    }
}
