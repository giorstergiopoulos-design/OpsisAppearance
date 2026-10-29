using System.Diagnostics;
using Opsis.Suite;
using Opsis.UI;

namespace OpsisAppearance;

/// <summary>
/// Το ενιαίο popup ενημέρωσης όλων των εφαρμογών Opsis: Εγκατάσταση / Αργότερα / Παράλειψη έκδοσης / Τι νέο υπάρχει. Η εγκατάσταση κατεβάζει τον installer ΜΟΝΟ από τα releases
/// του repo, επαληθεύει SHA-256 (άρνηση + διαγραφή σε ασυμφωνία), και τον τρέχει σιωπηλά μετά το κλείσιμο της εφαρμογής (το script ξαναελέγχει το hash).
/// </summary>
public sealed class UpdatePrompt : Form
{
    private readonly UpdateInfo _info; private readonly UpdateChecker _updater; private readonly SuiteContext _ctx; private readonly bool _greek, _hosted;
    private readonly Label _status = new() { AutoSize = false, Height = 40, Dock = DockStyle.Bottom };
    private readonly FlatButton _install = new() { Primary = true }, _later = new(), _skip = new(), _notes = new();
    public bool InstallStarted { get; private set; }

    public static string UpdateDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpsisAppearance", "update");

    public UpdatePrompt(SuiteContext ctx, UpdateChecker updater, UpdateInfo info, bool hostedByOpsis = false)
    {
        _hosted = hostedByOpsis; _ctx = ctx; _updater = updater; _info = info; _greek = SuiteLang.Current() == "el";
        string T(string el, string en) => _greek ? el : en;
        var c = ctx.Colors;
        Text = "Opsis Appearance"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen; TopMost = true; ShowInTaskbar = true;
        ClientSize = new Size(520, 190); BackColor = c.Bg; ForeColor = c.Text; Font = new Font("Segoe UI", 9.5f); AutoScaleMode = AutoScaleMode.Dpi;
        var title = new Label { Text = "Opsis Appearance " + info.Tag + T(" είναι διαθέσιμη", " is available"), Font = new Font("Segoe UI Semibold", 12f), AutoSize = false, Height = 30, Dock = DockStyle.Top, ForeColor = c.Text, Padding = new Padding(0, 4, 0, 0) };
        var sub = new Label { Text = T("Η εγκατάσταση γίνεται σιωπηλά με έλεγχο SHA-256 και η εφαρμογή ανοίγει ξανά.", "The update installs silently after a SHA-256 check and the app reopens."), AutoSize = false, Height = 40, Dock = DockStyle.Top, ForeColor = c.Muted };
        _status.ForeColor = c.Muted;
        _install.Text = T("Εγκατάσταση", "Install"); _later.Text = T("Αργότερα", "Later"); _skip.Text = T("Παράλειψη έκδοσης", "Skip this version"); _notes.Text = T("Τι νέο υπάρχει", "What's new");
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Color.Transparent };
        foreach (var b in new[] { _install, _later, _skip, _notes }) { b.AutoSize = true; b.Margin = new Padding(0, 4, 8, 0); row.Controls.Add(b); }
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 10) };
        pad.Controls.Add(_status); pad.Controls.Add(row); pad.Controls.Add(sub); pad.Controls.Add(title);
        Controls.Add(pad);
        AcceptButton = _install;
        _notes.Click += (_, _) => { if (SafeUrl.IsAllowed(_info.Url)) { try { Process.Start(new ProcessStartInfo(_info.Url) { UseShellExecute = true }); } catch (Exception) { } } };
        _later.Click += (_, _) => Close();
        _skip.Click += (_, _) => { _updater.SkipVersion(_info.Tag); Close(); };
        _install.Click += async (_, _) => await InstallAsync();
    }

    private async Task InstallAsync()
    {
        string T(string el, string en) => _greek ? el : en;
        _install.Enabled = _later.Enabled = _skip.Enabled = false;
        _status.Text = T("Λήψη και έλεγχος SHA-256…", "Downloading and checking SHA-256…");
        DownloadResult r;
        try { r = await SelfUpdate.PrepareAsync(_ctx.Http, _info, "OpsisAppearance-Setup.exe", UpdateDir); }
        catch (Exception ex) { r = new DownloadResult { Status = DownloadStatus.DownloadFailed, Message = ex.Message }; }
        if (r.Status == DownloadStatus.Ok)
        {
            try
            {
                string script = SelfUpdate.BuildScript(r.Path, r.Actual, Environment.ProcessId, Environment.ProcessPath ?? "", _hosted ? "plugin" : "standalone", relaunch: !_hosted, waitForever: _hosted);
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"))
                { ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-Command", script }, UseShellExecute = false, CreateNoWindow = true });
                InstallStarted = true;
                if (_hosted) { MessageBox.Show(this, T("Η ενημέρωση θα εγκατασταθεί όταν κλείσεις το Opsis (το plugin φορτώνεται από το Opsis). Άνοιξέ το ξανά μετά.", "The update installs when you close Opsis (the plugin is loaded by Opsis). Open it again afterwards."), "Opsis Appearance", MessageBoxButtons.OK, MessageBoxIcon.Information); InstallStarted = false; }
                Close(); return;
            }
            catch (Exception ex) { _status.Text = T("Αποτυχία εκκίνησης: ", "Could not start the installer: ") + ex.Message; }
        }
        else _status.Text = r.Status == DownloadStatus.Mismatch ? T("Η λήψη απορρίφθηκε: το SHA-256 δεν ταιριάζει (το αρχείο διαγράφηκε).", "Download refused: the SHA-256 does not match (the file was deleted).")
                          : r.Status == DownloadStatus.NoChecksum ? T("Δεν υπάρχει SHA-256 για επαλήθευση — άνοιξε τη σελίδα της έκδοσης.", "No SHA-256 to verify against — open the release page.")
                          : T("Η λήψη απέτυχε — δοκίμασε «Τι νέο υπάρχει» για χειροκίνητη λήψη.", "The download failed — use \"What's new\" for a manual download.");
        _install.Enabled = _later.Enabled = _skip.Enabled = true;
    }
}
