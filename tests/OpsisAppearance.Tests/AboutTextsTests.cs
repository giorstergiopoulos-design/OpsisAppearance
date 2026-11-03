using OpsisAppearance;
using Xunit;

namespace OpsisAppearance.Tests;

public class AboutTextsTests
{
    [Fact] public void AboutHasHelpInstructionsHistoryLicenseInBothLanguages()
    {
        var plugin = new OpsisAppearancePlugin(new FakeRegistry());
        foreach (var lang in new[] { "el", "en" })
        {
            plugin.SetLanguage(lang);
            var about = plugin.GetAboutInfo();
            Assert.Equal(new[] { "Help", "Instructions", "Version history", "License" }, about.Sections.Select(s => s.TitleEn).ToArray());
            Assert.Equal(new[] { "Βοήθεια", "Οδηγίες χρήσης", "Ιστορικό εκδόσεων", "Άδεια χρήσης" }, about.Sections.Select(s => s.TitleEl).ToArray());
            Assert.All(about.Sections, s => Assert.True(s.Text.Length > 40));
        }
    }

    [Fact] public void HistoryListsTheCurrentVersionInBothLanguagesAndMatchesTheChangelog()
    {
        var v = typeof(OpsisAppearancePlugin).Assembly.GetName().Version!;
        string cur = $"{v.Major}.{v.Minor}.{v.Build}";
        Assert.Equal(cur, AboutTexts.HistoryEntries[0].Version);
        Assert.All(AboutTexts.HistoryEntries, h => { Assert.True(h.El.Length > 5 && h.En.Length > 5); Assert.DoesNotMatch("[\u0370-\u03FF]", h.En); });
        Assert.Contains(cur, AboutTexts.History(true)); Assert.Contains(cur, AboutTexts.History(false));
        string root = System.AppContext.BaseDirectory; while (root != null && !File.Exists(Path.Combine(root, "CHANGELOG.md"))) root = Path.GetDirectoryName(root)!;
        var log = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        foreach (var h in AboutTexts.HistoryEntries) Assert.Contains("## " + h.Version, log);
    }

    [Fact] public void LicenseTextComesFromTheLicenseFileAndListsThirdParties()
    {
        var lic = AboutTexts.License(false);
        Assert.Contains("MIT", lic); Assert.Contains("Opsis.UI", lic); Assert.Contains("AS IS", lic);
        Assert.Contains("Συστατικά τρίτων", AboutTexts.License(true));
    }
}
