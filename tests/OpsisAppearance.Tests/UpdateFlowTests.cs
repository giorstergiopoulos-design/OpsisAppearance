using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Opsis.Suite;
using Opsis.SuiteTests;
using Xunit;

namespace OpsisAppearance.Tests
{
    public class UpdateFlowTests
    {
        static string Hex(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

        static (FakeHttp http, UpdateInfo info, byte[] setup) Make(string tag, byte[] checksumFor = null, bool withSum = true)
        {
            var setup = Encoding.ASCII.GetBytes("MZ fake installer " + tag + new string('x', 5000));
            var http = new FakeHttp();
            var rel = new ReleaseDto { Tag = tag, Name = tag, Url = "https://github.com/giorstergiopoulos-design/OpsisAppearance/releases/tag/" + tag };
            rel.Assets.Add(new ReleaseAsset { Name = "OpsisAppearance-Setup.exe", Url = "https://github.com/giorstergiopoulos-design/OpsisAppearance/releases/download/" + tag + "/OpsisAppearance-Setup.exe" });
            http.Files[rel.Assets[0].Url] = setup;
            if (withSum)
            {
                var sumUrl = rel.Assets[0].Url + ".sha256"; rel.Assets.Add(new ReleaseAsset { Name = "OpsisAppearance-Setup.exe.sha256", Url = sumUrl });
                http.Responses[sumUrl] = new HttpResult { Status = 200, Body = Hex(checksumFor ?? setup) + " *OpsisAppearance-Setup.exe\n" };
            }
            return (http, new UpdateInfo { Tag = tag, Release = rel, Url = rel.Url }, setup);
        }

        static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "opsis_upd_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

        [Fact] public async Task VerifiedDownloadSucceeds()
        {
            var (http, info, setup) = Make("v9.0.0"); var dir = Tmp();
            try { var r = await SelfUpdate.PrepareAsync(http, info, "OpsisAppearance-Setup.exe", dir); Assert.Equal(DownloadStatus.Ok, r.Status); Assert.Equal(Hex(setup), r.Actual); Assert.True(File.Exists(r.Path)); }
            finally { Directory.Delete(dir, true); }
        }

        [Fact] public async Task HashMismatchIsRefusedAndDeleted()
        {
            var (http, info, _) = Make("v9.0.0", checksumFor: Encoding.ASCII.GetBytes("something else")); var dir = Tmp();
            try { var r = await SelfUpdate.PrepareAsync(http, info, "OpsisAppearance-Setup.exe", dir); Assert.Equal(DownloadStatus.Mismatch, r.Status); Assert.Empty(Directory.GetFiles(dir)); }
            finally { Directory.Delete(dir, true); }
        }

        [Fact] public async Task NoChecksumMeansNoDownload()
        {
            var (http, info, _) = Make("v9.0.0", withSum: false); var dir = Tmp();
            try { var r = await SelfUpdate.PrepareAsync(http, info, "OpsisAppearance-Setup.exe", dir); Assert.Equal(DownloadStatus.NoChecksum, r.Status); Assert.DoesNotContain(http.Requests, q => q.StartsWith("DOWNLOAD")); }
            finally { Directory.Delete(dir, true); }
        }

        [Fact] public async Task MissingAssetOrRelease()
        {
            var (http, info, _) = Make("v9.0.0"); var dir = Tmp();
            try
            {
                Assert.Equal(DownloadStatus.NoAsset, (await SelfUpdate.PrepareAsync(http, info, "Other-Setup.exe", dir)).Status);
                Assert.Equal(DownloadStatus.NoAsset, (await SelfUpdate.PrepareAsync(http, new UpdateInfo { Tag = "v1" }, "OpsisAppearance-Setup.exe", dir)).Status);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact] public void SkipVersionIsRemembered_NewerStillAnnounced()
        {
            var st = new MemoryStore(); var u = new UpdateChecker(new FakeHttp(), st, () => DateTime.UtcNow, "o", "r", "1.0.0");
            Assert.Equal("", u.SkippedTag ?? ""); u.SkipVersion("v1.1.0"); Assert.Equal("v1.1.0", u.SkippedTag); u.SkipVersion(null); Assert.Equal("", u.SkippedTag ?? "");
        }

        [Fact] public async Task SkippedVersionNotOfferedUntilNewerOne()
        {
            var http = new FakeHttp(); var st = new MemoryStore(); var now = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
            string Json(string tag) => "{\"tag_name\":\"" + tag + "\",\"name\":\"" + tag + "\",\"html_url\":\"https://github.com/o/r/releases/tag/" + tag + "\",\"body\":\"notes\",\"draft\":false,\"prerelease\":false,\"assets\":[]}";
            http.Responses["https://api.github.com/repos/o/r/releases/latest"] = new HttpResult { Status = 200, Body = Json("v1.1.0"), ETag = "e1" };
            var u = new UpdateChecker(http, st, () => now, "o", "r", "1.0.0");
            var first = await u.CheckAsync(); Assert.NotNull(first); Assert.Equal("notes", first.Notes);
            u.SkipVersion("v1.1.0"); now = now.AddDays(2);
            Assert.Null(await u.CheckAsync());
            http.Responses["https://api.github.com/repos/o/r/releases/latest"] = new HttpResult { Status = 200, Body = Json("v1.2.0"), ETag = "e2" };
            now = now.AddDays(2); var newer = await u.CheckAsync(); Assert.NotNull(newer); Assert.Equal("v1.2.0", newer.Tag);
        }

        [Fact] public void InstallerStartRefusesAChangedFileAndLogsIt()
        {
            string dir = Path.Combine(Path.GetTempPath(), "opsis_inst_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                string setup = Path.Combine(dir, "setup.exe"); File.WriteAllBytes(setup, Encoding.ASCII.GetBytes("tampered after download"));
                string log = Path.Combine(dir, "last-update.log");
                Assert.Throws<InvalidDataException>(() => SelfUpdate.StartInstaller(setup, new string('a', 64), "standalone", logPath: log));
                Assert.Contains("FAILED: SHA-256 changed", File.ReadAllText(log));
                Assert.Throws<ArgumentException>(() => SelfUpdate.StartInstaller(setup, "short"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact] public void InstallerArgumentsUseRestartManagerAndNoPowerShell()
        {
            var plugin = SelfUpdate.BuildArguments("plugin", true, null);
            Assert.Contains("/CLOSEAPPLICATIONS", plugin); Assert.Contains("/RESTARTAPPLICATIONS", plugin); Assert.Contains("/MODE=plugin", plugin); Assert.DoesNotContain("RELAUNCH", plugin);
            var alone = SelfUpdate.BuildArguments("standalone", false, "/RELAUNCH=1");
            Assert.Contains("/MODE=standalone", alone); Assert.Contains("/RELAUNCH=1", alone); Assert.DoesNotContain("/RESTARTAPPLICATIONS", alone);
        }

        [Fact] public void NoHiddenPowerShellOrExecutionPolicyBypassInTheSources()
        {
            string root = AppContext.BaseDirectory; while (root != null && !File.Exists(Path.Combine(root, "OpsisAppearance.sln"))) root = Path.GetDirectoryName(root)!;
            foreach (var f in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories).Where(x => !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
            {
                var t = File.ReadAllText(f);
                Assert.DoesNotContain("ExecutionPolicy", t); Assert.DoesNotContain("-WindowStyle", t);
            }
        }
    }
}

namespace OpsisAppearance.Tests
{
    public class UpdateHostedFlagTests
    {
        [Fact] public void PluginStartsUpdateChecksOnlyWhenHosted()
        {
            Assert.True(OpsisAppearance.OpsisAppearancePlugin.HostedByOpsis);        // προεπιλογή: μέσα στο Opsis ελέγχει το plugin
        }
    }
}
