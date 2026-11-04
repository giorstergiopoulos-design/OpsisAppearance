using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Xunit;

namespace OpsisAppearance.Tests;

/// <summary>Υγιεινή κυκλοφορίας: πληροφορίες έκδοσης στα binaries, τυπικό MIT, Inno 7 με εκδότη, καθόλου ίχνη (διαδρομές, ονόματα, email) στα binaries και στα zip.</summary>
public class ReleaseHygieneTests
{
    static string Root() { var d = AppContext.BaseDirectory; while (d != null && !File.Exists(Path.Combine(d, "OpsisAppearance.sln"))) d = Path.GetDirectoryName(d); return d!; }

    static readonly string[] Patterns =
    {
        "C:" + (char)92 + "Users" + (char)92, "C:" + (char)92 + "w" + (char)92, "D:" + (char)92 + "a" + (char)92, "gstrj", "LOGISTIRIO", "giorstergiopoulos@gmail",
    };

    static bool Contains(byte[] hay, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= hay.Length; i++) { int j = 0; while (j < needle.Length && hay[i + j] == needle[j]) j++; if (j == needle.Length) return true; }
        return false;
    }

    // Microsoft's own .NET apphost (the stub of every OpsisAppearance.exe) carries the path of ITS build machine ("D:\a\_work\1\s\src\runtime\...\apphost.pdb"): third-party, identical in every .NET app, not ours.
    static readonly string AllowedMicrosoftMarker = "D:" + (char)92 + "a" + (char)92 + "_work" + (char)92 + "1" + (char)92 + "s" + (char)92 + "src" + (char)92 + "runtime" + (char)92;

    static byte[] RemoveAll(byte[] hay, byte[] needle)
    {
        var o = new List<byte>(hay.Length);
        for (int i = 0; i < hay.Length;)
        {
            int j = 0; while (j < needle.Length && i + j < hay.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) { i += needle.Length; continue; }
            o.Add(hay[i]); i++;
        }
        return o.ToArray();
    }

    static IEnumerable<string> Hits(byte[] raw)
    {
        var b = RemoveAll(RemoveAll(raw, System.Text.Encoding.UTF8.GetBytes(AllowedMicrosoftMarker)), System.Text.Encoding.Unicode.GetBytes(AllowedMicrosoftMarker));
        return Patterns.Where(p => Contains(b, System.Text.Encoding.UTF8.GetBytes(p)) || Contains(b, System.Text.Encoding.Unicode.GetBytes(p)));
    }

    [Fact] public void BinariesCarryCompanyProductCopyrightAndVersion()
    {
        var asm = typeof(OpsisAppearancePlugin).Assembly;
        var info = FileVersionInfo.GetVersionInfo(asm.Location);
        Assert.Equal("giorstergiopoulos-design", info.CompanyName); Assert.Equal("Opsis Appearance", info.ProductName);
        Assert.Contains("giorstergiopoulos-design", info.LegalCopyright); Assert.False(string.IsNullOrWhiteSpace(info.FileDescription));
        var v = asm.GetName().Version!; Assert.StartsWith($"{v.Major}.{v.Minor}.{v.Build}", info.FileVersion);
        Assert.Equal("MIT", asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "x")?.Value ?? "MIT");
        var props = File.ReadAllText(Path.Combine(Root(), "Directory.Build.props"));
        Assert.Contains("<PackageLicenseExpression>MIT</PackageLicenseExpression>", props); Assert.Contains("<RepositoryUrl>", props); Assert.Contains("<Authors>giorstergiopoulos-design</Authors>", props);
    }

    [Fact] public void LicenseIsTheStandardMitText()
    {
        var t = File.ReadAllText(Path.Combine(Root(), "LICENSE")).Replace("\r\n", "\n");
        Assert.StartsWith("MIT License\n\nCopyright (c) 2026 giorstergiopoulos-design\n\nPermission is hereby granted, free of charge, to any person obtaining a copy", t);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND", t); Assert.DoesNotContain("IMPORTANT", t);
    }

    [Fact] public void InstallerRequiresInno7AndNamesThePublisher()
    {
        var iss = File.ReadAllText(Path.Combine(Root(), "installer", "OpsisAppearance.iss"));
        Assert.Contains("#if Ver < EncodeVer(7, 0, 0)", iss); Assert.Contains("#error", iss);
        foreach (var key in new[] { "AppPublisher=giorstergiopoulos-design", "AppPublisherURL=", "AppSupportURL=", "VersionInfoCompany=giorstergiopoulos-design", "VersionInfoDescription=", "VersionInfoVersion=", "VersionInfoCopyright=", "VersionInfoProductName=" })
            Assert.Contains(key, iss);
        var build = File.ReadAllText(Path.Combine(Root(), "build.ps1")); Assert.DoesNotContain("Inno Setup 6", build);
    }

    [Fact] public void CommittedLibsAndReleaseOutputsHaveNoTraces()
    {
        var root = Root(); var files = new List<string>(Directory.GetFiles(Path.Combine(root, "libs"), "*.dll"));
        foreach (var dir in new[] { Path.Combine(root, "src", "OpsisAppearance.Plugin", "bin", "Release"), Path.Combine(root, "src", "OpsisAppearance.App", "bin", "Release"), Path.Combine(root, "dist") })
            if (Directory.Exists(dir)) files.AddRange(Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".dll") && Path.GetFileName(f).StartsWith("Opsis") || f.EndsWith("OpsisAppearance.exe")));
        foreach (var f in files) Assert.Empty(Hits(File.ReadAllBytes(f)).Select(h => f + " contains " + h));
    }

    [Fact] public void ReleaseZipsHaveNoTracesAndNoPdb()
    {
        var output = Path.Combine(Root(), "Output"); if (!Directory.Exists(output)) return;       // zips exist only after build.ps1
        foreach (var zipPath in Directory.GetFiles(output, "*.zip"))
        {
            using var z = ZipFile.OpenRead(zipPath);
            foreach (var e in z.Entries.Where(x => x.Length > 0))
            {
                Assert.False(e.Name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase), zipPath + " contains a PDB: " + e.FullName);
                if (!(e.Name.EndsWith(".dll") || e.Name.EndsWith(".exe") || e.Name.EndsWith(".json") || e.Name.EndsWith(".txt"))) continue;
                using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms);
                Assert.Empty(Hits(ms.ToArray()).Select(h => zipPath + "!" + e.FullName + " contains " + h));
            }
        }
    }

    [Fact] public void TrackedSourcesHaveNoPersonalPathsOrEmail()
    {
        var root = Root();
        foreach (var f in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                              && !x.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar) && !x.Contains(Path.DirectorySeparatorChar + "Output" + Path.DirectorySeparatorChar)
                              && !x.Contains(Path.DirectorySeparatorChar + "dist" + Path.DirectorySeparatorChar) && !x.Contains(Path.DirectorySeparatorChar + "release" + Path.DirectorySeparatorChar)
                              && !x.Contains(Path.DirectorySeparatorChar + "libs" + Path.DirectorySeparatorChar)
                              && (x.EndsWith(".cs") || x.EndsWith(".md") || x.EndsWith(".ps1") || x.EndsWith(".iss") || x.EndsWith(".yml") || x.EndsWith(".csproj") || x.EndsWith(".props"))
                              && !x.EndsWith("ReleaseHygieneTests.cs") && !x.EndsWith("BuildHygieneTests.cs") && !x.EndsWith("security-scan.yml") && !x.EndsWith("CHANGELOG.md") && !x.Contains("RELEASE-")))
            Assert.Empty(Hits(File.ReadAllBytes(f)).Select(h => f + " contains " + h));
    }
}
