using Xunit;

namespace OpsisAppearance.Tests;

/// <summary>Καμία τοπική διαδρομή ή όνομα χρήστη μέσα στα DLL/EXE που παραδίδονται (libs, bin\Release, dist): ούτε "C:\Users\", ούτε "C:\w\", ούτε "gstrj" (UTF-8 και UTF-16).</summary>
public class BuildHygieneTests
{
    static string Root() { var d = AppContext.BaseDirectory; while (d != null && !File.Exists(Path.Combine(d, "OpsisAppearance.sln"))) d = Path.GetDirectoryName(d); return d!; }

    static readonly string[] Patterns = { "C:" + (char)92 + "Users" + (char)92, "C:" + (char)92 + "w" + (char)92, "gstrj" };

    static IEnumerable<string> Hits(string file)
    {
        var b = File.ReadAllBytes(file);
        foreach (var p in Patterns)
            if (Contains(b, System.Text.Encoding.UTF8.GetBytes(p)) || Contains(b, System.Text.Encoding.Unicode.GetBytes(p))) yield return p;
    }

    static bool Contains(byte[] hay, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= hay.Length; i++)
        {
            int j = 0; while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return true;
        }
        return false;
    }

    [Fact] public void CommittedLibsContainNoLocalPaths()
    {
        foreach (var f in Directory.GetFiles(Path.Combine(Root(), "libs"), "*.dll")) Assert.Empty(Hits(f).Select(h => f + " contains " + h));
    }

    [Fact] public void ReleaseOutputsContainNoLocalPaths()
    {
        var root = Root(); var checkedAny = false;
        foreach (var dir in new[] { Path.Combine(root, "src", "OpsisAppearance.Plugin", "bin", "Release"), Path.Combine(root, "src", "OpsisAppearance.App", "bin", "Release"), Path.Combine(root, "dist") })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(x).StartsWith("Opsis", StringComparison.OrdinalIgnoreCase) || x.EndsWith("OpsisAppearance.exe", StringComparison.OrdinalIgnoreCase)))
            { checkedAny = true; Assert.Empty(Hits(f).Select(h => f + " contains " + h)); }
        }
    }

    [Fact] public void DeterministicBuildPropsArePresent()
    {
        var props = File.ReadAllText(Path.Combine(Root(), "Directory.Build.props"));
        Assert.Contains("<Deterministic>true", props); Assert.Contains("<DeterministicSourcePaths>true", props); Assert.Contains("<ContinuousIntegrationBuild>true", props); Assert.Contains("PathMap", props);
    }

    [Fact] public void HostedPluginDoesNotOfferWelcomeOrFamilyInTheTray()
    {
        var plugin = new OpsisAppearancePlugin(new FakeRegistry());
        Assert.True(OpsisAppearancePlugin.HostedByOpsis);
        var keys = plugin.GetTrayMenuItems().Select(i => i.Key).ToArray();
        Assert.DoesNotContain("appearance.welcome", keys); Assert.DoesNotContain("appearance.family", keys); Assert.Contains("appearance.toggle", keys); Assert.Contains("appearance.open", keys);
        var old = OpsisAppearancePlugin.HostedByOpsis;
        try { OpsisAppearancePlugin.HostedByOpsis = false; var standalone = plugin.GetTrayMenuItems().Select(i => i.Key).ToArray(); Assert.Contains("appearance.welcome", standalone); Assert.Contains("appearance.family", standalone); }
        finally { OpsisAppearancePlugin.HostedByOpsis = old; }
    }
}
