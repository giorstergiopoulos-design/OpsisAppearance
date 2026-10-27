using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Opsis.Suite;

// Καλωσόρισμα + οδηγός χρήσης και «Οι εφαρμογές της οικογένειας Opsis» για εφαρμογές WinForms. Η λογική (εκδόσεις, κατάλογος, ανίχνευση, λήψη με SHA-256)
// είναι στο SuiteCore.cs και δοκιμάζεται χωρίς UI· εδώ μένει μόνο το περίβλημα.

public sealed record SuiteColors(Color Bg, Color Card, Color Border, Color Text, Color Muted, Color Accent)
{
    public static readonly SuiteColors Dark = new(Color.FromArgb(18, 22, 32), Color.FromArgb(28, 34, 48), Color.FromArgb(52, 62, 84), Color.FromArgb(235, 240, 250), Color.FromArgb(150, 160, 180), Color.FromArgb(0, 200, 230));
    public static readonly SuiteColors Light = new(Color.FromArgb(245, 246, 250), Color.FromArgb(255, 255, 255), Color.FromArgb(208, 213, 224), Color.FromArgb(25, 28, 36), Color.FromArgb(105, 112, 128), Color.FromArgb(0, 120, 212));
}

/// <summary>Όλα όσα χρειάζονται τα δύο παράθυρα από την εφαρμογή που τα φιλοξενεί.</summary>
public sealed class SuiteContext
{
    public string Owner = "giorstergiopoulos-design", AppId = "", AppName = "", Repo = "", Version = "0.0.0", EmbeddedCatalogue = "", CacheFile = "";
    public IKeyValueStore Store = new MemoryStore();
    public IHttp Http = new OfflineHttp();
    public SuiteColors Colors = SuiteColors.Dark;
    public Icon? AppIcon;
    public Func<Rectangle>? IconTarget;       // πού «κατοικεί» το εικονίδιο της εφαρμογής (tray ή γωνία παραθύρου)
    public IInstallProbe Probe = new RealInstallProbe();
    public int MinimiseMs = 420;
    public bool ForceTop;                     // μόνο για το demo/screenshots

    public Rectangle Target()
    {
        try { var r = IconTarget?.Invoke(); if (r is { Width: > 0 }) return r.Value; } catch (Exception) { }
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        return new Rectangle(wa.Right - 150, wa.Bottom - 22, 20, 20);          // προς την περιοχή ειδοποιήσεων
    }

    public async Task<SuiteCatalogue> LoadCatalogueAsync() =>
        await CatalogueProvider.LoadAsync(Http, Store, () => DateTime.UtcNow, CacheFile, EmbeddedCatalogue) ?? SuiteCatalogue.Parse(EmbeddedCatalogue, out _)!;
}

public sealed class OfflineHttp : IHttp
{
    public Task<HttpResult> GetAsync(string url, string? etag, TimeSpan timeout) => Task.FromResult(new HttpResult { Status = 0 });
    public Task<bool> DownloadAsync(string url, string destination, long maxBytes, TimeSpan timeout) => Task.FromResult(false);
}

internal static class SuiteUi
{
    public static string L => SuiteLang.Current();
    public static string T(string el, string en) => L == "el" ? el : en;
    public static bool Animations => SystemInformation.UIEffectsEnabled;

    public static void OpenUrl(string url)
    {
        if (!SafeUrl.IsAllowed(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
    }

    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2; var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    /// <summary>Στιγμιότυπο του παραθύρου μέσω PrintWindow (PW_RENDERFULLCONTENT): το DrawToBitmap δίνει μαύρο σε παράθυρα που έχουν περάσει από Opacity/layered.</summary>
    public static Bitmap Snapshot(Form f)
    {
        var bmp = new Bitmap(f.Width, f.Height);
        using (var g = Graphics.FromImage(bmp)) { var hdc = g.GetHdc(); try { PrintWindow(f.Handle, hdc, 2); } finally { g.ReleaseHdc(hdc); } }
        return bmp;
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
    public static void DragForm(Form f) { ReleaseCapture(); SendMessage(f.Handle, 0xA1, 2, 0); }

    public static Button MakeButton(string text, SuiteColors c, bool primary = false, int width = 100)
    {
        var b = new Button { Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.5f), UseVisualStyleBackColor = false, Margin = new Padding(0, 0, 8, 0) };
        b.FlatAppearance.BorderSize = 0; b.BackColor = primary ? c.Accent : c.Border; b.ForeColor = primary ? (Brightness(c.Accent) > 150 ? Color.Black : Color.White) : c.Text;
        b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(c.Accent, 0.2f) : ControlPaint.Light(c.Border, 0.25f);
        return b;
    }
    public static float Brightness(Color c) => c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;

    public static Label MakeLabel(string text, float size, Color color, bool bold = false, int maxWidth = 0)
    {
        var l = new Label { Text = text, AutoSize = true, ForeColor = color, BackColor = Color.Transparent, Font = new Font("Segoe UI" + (bold ? " Semibold" : ""), size), Margin = new Padding(0) };
        if (maxWidth > 0) l.MaximumSize = new Size(maxWidth, 0);
        return l;
    }
}

/// <summary>Το εικονίδιο της εφαρμογής με ζωντανό glow και δαχτυλίδι που γυρίζει (στατικό όταν τα animations των Windows είναι κλειστά).</summary>
public sealed class AnimatedLogo : Control
{
    private readonly Icon? _icon; private readonly string _letter; private readonly Color _tint; private readonly System.Windows.Forms.Timer? _timer;
    private float _angle, _phase;

    public AnimatedLogo(Icon? icon, string letter, Color tint, int size)
    {
        _icon = icon; _letter = letter; _tint = tint; Size = new Size(size, size);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        if (SuiteUi.Animations) { _timer = new System.Windows.Forms.Timer { Interval = 40 }; _timer.Tick += (_, _) => { _angle = (_angle + 3f) % 360; _phase += 0.07f; Invalidate(); }; _timer.Start(); }
    }

    protected override void Dispose(bool disposing) { if (disposing) _timer?.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        float s = Math.Min(Width, Height), cx = Width / 2f, cy = Height / 2f, pulse = 0.5f + 0.5f * (float)Math.Sin(_phase);
        // glow
        using (var path = new GraphicsPath()) { path.AddEllipse(cx - s / 2, cy - s / 2, s, s); using var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb((int)(60 + 70 * pulse), _tint), SurroundColors = new[] { Color.FromArgb(0, _tint) } }; g.FillPath(pg, path); }
        // rotating dashed ring
        float rr = s * 0.40f;
        using (var pen = new Pen(Color.FromArgb(230, _tint), Math.Max(2f, s / 28f)) { DashStyle = DashStyle.Dash }) { g.ResetTransform(); g.TranslateTransform(cx, cy); g.RotateTransform(_angle); g.DrawEllipse(pen, -rr, -rr, rr * 2, rr * 2); g.ResetTransform(); }
        // εικονίδιο (πραγματικό αν υπάρχει) ή αρχικό γράμμα
        float inner = s * (0.52f + 0.03f * pulse); var rect = new RectangleF(cx - inner / 2, cy - inner / 2, inner, inner);
        if (_icon != null) g.DrawIcon(_icon, Rectangle.Round(rect));
        else
        {
            using var br = new SolidBrush(Color.FromArgb(255, 20, 28, 48)); g.FillEllipse(br, rect);
            using var f = new Font("Segoe UI", inner * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel); using var tb = new SolidBrush(_tint);
            var sz = g.MeasureString(_letter, f); g.DrawString(_letter, f, tb, cx - sz.Width / 2, cy - sz.Height / 2);
        }
    }
}

/// <summary>Κοινή βάση: χωρίς πλαίσιο, στρογγυλεμένο, με σύρσιμο και animation «μικραίνει προς το εικονίδιο».</summary>
public abstract class SuiteFormBase : Form
{
    protected readonly SuiteContext Ctx; private bool _closing;

    protected SuiteFormBase(SuiteContext ctx, Size size)
    {
        Ctx = ctx; TopMost = ctx.ForceTop; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = false; Size = size; KeyPreview = true;
        BackColor = ctx.Colors.Card; ForeColor = ctx.Colors.Text; DoubleBuffered = true; Font = new Font("Segoe UI", 9.5f);
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) SuiteUi.DragForm(this); };
        Resize += (_, _) => UpdateRegion();
        Shown += (_, _) => { if (SuiteUi.Animations) { Opacity = 0; var t = new System.Windows.Forms.Timer { Interval = 15 }; t.Tick += (_, _) => { Opacity = Math.Min(1, Opacity + 0.12); if (Opacity >= 1) t.Dispose(); }; t.Start(); } };
    }

    private void UpdateRegion() { if (Width < 10 || Height < 10) return; using var p = SuiteUi.Rounded(new Rectangle(0, 0, Width, Height), Math.Min(16, Math.Min(Width, Height) / 2)); Region?.Dispose(); Region = new Region(p); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Ctx.Colors.Border, 1.5f); using var path = SuiteUi.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 16); e.Graphics.DrawPath(pen, path);
    }

    /// <summary>Κλείνει με animation (κλίμακα + μετακίνηση + fade ~400 ms) προς το εικονίδιο της εφαρμογής· χωρίς animations των Windows: fade 150 ms.</summary>
    public async Task MinimiseAsync()
    {
        if (_closing || IsDisposed) return; _closing = true;
        var done = new TaskCompletionSource<bool>();
        if (!Visible) { Close(); return; }
        int ms = SuiteUi.Animations && Ctx.MinimiseMs > 0 ? Ctx.MinimiseMs : 150; bool move = SuiteUi.Animations && Ctx.MinimiseMs > 0;
        var snap = SuiteUi.Snapshot(this); foreach (Control c in Controls) c.Visible = false; BackgroundImage = snap; BackgroundImageLayout = ImageLayout.Stretch;
        var from = Bounds; var to = Ctx.Target(); var sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            double p = Math.Min(1, sw.Elapsed.TotalMilliseconds / ms), e = p * p * p;
            if (move) Bounds = new Rectangle((int)(from.X + (to.X - from.X) * e), (int)(from.Y + (to.Y - from.Y) * e), Math.Max(8, (int)(from.Width + (to.Width - from.Width) * e)), Math.Max(8, (int)(from.Height + (to.Height - from.Height) * e)));
            Opacity = 1 - p;
            if (p >= 1) { timer.Dispose(); done.TrySetResult(true); }
        };
        timer.Start(); await done.Task; Close();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { _ = MinimiseAsync(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

// =====================================================================================================================================
//  Καλωσόρισμα + οδηγός
// =====================================================================================================================================
public sealed class WelcomeForm : SuiteFormBase
{
    public sealed record Page(string Title, string Body);
    private readonly IReadOnlyList<Page> _pages; private int _i;
    private readonly Label _title, _body, _count; private readonly Button _back, _next, _skip; private readonly CheckBox _never; private readonly Panel _dots;
    public bool Finished { get; private set; }
    public int PageIndex => _i; public int PageCount => _pages.Count;

    public WelcomeForm(SuiteContext ctx, IReadOnlyList<Page> pages) : base(ctx, new Size(660, 500))
    {
        _pages = pages; var c = ctx.Colors;
        var logo = new AnimatedLogo(ctx.AppIcon, ctx.AppName.Replace("Opsis ", "")[..1], c.Accent, 104) { Location = new Point(24, 18) }; Controls.Add(logo);
        var name = SuiteUi.MakeLabel(ctx.AppName, 24, c.Text, true); name.Location = new Point(138, 34); Controls.Add(name);
        var ver = SuiteUi.MakeLabel(SuiteUi.T("Έκδοση ", "Version ") + ctx.Version, 11, c.Muted); ver.Location = new Point(140, 76); Controls.Add(ver);
        _count = SuiteUi.MakeLabel("", 10, c.Muted); _count.Location = new Point(140, 98); Controls.Add(_count);
        _title = SuiteUi.MakeLabel("", 17, c.Accent, true, 600); _title.Location = new Point(30, 140); Controls.Add(_title);
        _body = SuiteUi.MakeLabel("", 11.5f, c.Text, false, 600); _body.Location = new Point(30, 182); Controls.Add(_body);
        _dots = new Panel { Location = new Point(30, 410), Size = new Size(240, 14), BackColor = Color.Transparent }; _dots.Paint += PaintDots; Controls.Add(_dots);
        _never = new CheckBox { Text = SuiteUi.T("Να μην εμφανιστεί ξανά", "Don't show again"), AutoSize = true, ForeColor = c.Muted, BackColor = Color.Transparent, Location = new Point(30, 440) }; Controls.Add(_never);
        _skip = SuiteUi.MakeButton(SuiteUi.T("Παράλειψη", "Skip"), c, false, 92); _back = SuiteUi.MakeButton(SuiteUi.T("Πίσω", "Back"), c, false, 80); _next = SuiteUi.MakeButton("", c, true, 112);
        int y = 436; _next.Location = new Point(660 - 30 - 112, y); _back.Location = new Point(_next.Left - 8 - 80, y); _skip.Location = new Point(_back.Left - 8 - 92, y);
        Controls.AddRange(new Control[] { _skip, _back, _next });
        AcceptButton = _next;
        _skip.Click += async (_, _) => await CloseAsync(false);
        _back.Click += (_, _) => Go(_i - 1);
        _next.Click += async (_, _) => { if (_i >= _pages.Count - 1) await CloseAsync(true); else Go(_i + 1); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Right && _i < _pages.Count - 1) Go(_i + 1); else if (e.KeyCode == Keys.Left) Go(_i - 1); };
        Go(0);
    }

    public void Go(int i)
    {
        _i = Math.Max(0, Math.Min(_pages.Count - 1, i));
        _title.Text = _pages[_i].Title; _body.Text = _pages[_i].Body; _count.Text = (_i + 1) + " / " + _pages.Count;
        _back.Enabled = _i > 0; _next.Text = _i >= _pages.Count - 1 ? SuiteUi.T("Τέλος", "Finish") : SuiteUi.T("Επόμενο", "Next");
        _skip.Visible = _i < _pages.Count - 1; _dots.Invalidate();
    }

    private void PaintDots(object? s, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; int x = 0;
        for (int k = 0; k < _pages.Count; k++)
        {
            int w = k == _i ? 24 : 9; using var b = new SolidBrush(k == _i ? Ctx.Colors.Accent : Ctx.Colors.Border);
            using var p = SuiteUi.Rounded(new Rectangle(x, 2, w, 9), 4); e.Graphics.FillPath(b, p); x += w + 6;
        }
    }

    public async Task CloseAsync(bool finished)
    {
        Finished = finished;
        Ctx.Store.Set("welcome.shown", MajorMinor(Ctx.Version));
        if (_never.Checked) Ctx.Store.Set("welcome.never", "1");
        await MinimiseAsync();
    }

    public static string MajorMinor(string v) => SemVer.TryParse(v, out var n, out _) ? n[0] + "." + n[1] : v;
}

// =====================================================================================================================================
//  «Οι εφαρμογές της οικογένειας Opsis»
// =====================================================================================================================================
public sealed class SuiteForm : SuiteFormBase
{
    private readonly SuiteCatalogue _cat; private readonly FlowLayoutPanel _flow;
    private readonly Dictionary<string, (Label State, Button Open, Button Install)> _cards = new();
    private readonly Dictionary<string, (InstallState St, ReleaseDto? Latest)> _info = new();

    public SuiteForm(SuiteContext ctx, SuiteCatalogue catalogue) : base(ctx, new Size(960, 700))
    {
        _cat = catalogue; var c = ctx.Colors;
        var title = SuiteUi.MakeLabel(SuiteUi.T("Οι εφαρμογές της οικογένειας Opsis", "The Opsis family"), 20, c.Text, true); title.Location = new Point(28, 20); Controls.Add(title);
        var sub = SuiteUi.MakeLabel(SuiteUi.T("Τι κάνει η καθεμία, ποιες έχεις και πώς τις παίρνεις. Οι λήψεις ελέγχονται με SHA-256.", "What each one does, which ones you have and how to get them. Downloads are checked with SHA-256."), 10.5f, c.Muted); sub.Location = new Point(30, 58); Controls.Add(sub);
        var close = SuiteUi.MakeButton(SuiteUi.T("Κλείσιμο", "Close"), c, false, 100); close.Location = new Point(960 - 28 - 100, 24); close.Click += async (_, _) => await MinimiseAsync(); Controls.Add(close);
        _flow = new FlowLayoutPanel { Location = new Point(18, 92), Size = new Size(924, 556), AutoScroll = true, WrapContents = true, BackColor = Color.Transparent }; Controls.Add(_flow);
        var foot = SuiteUi.MakeLabel(SuiteUi.T("Ανιχνεύεται μόνο ανάγνωση (registry/φάκελοι)· τίποτα δεν εγκαθίσταται χωρίς τη δική σου επιβεβαίωση.", "Detection is read-only (registry/folders); nothing is installed without your confirmation."), 9, c.Muted); foot.Location = new Point(30, 664); Controls.Add(foot);
        foreach (var app in _cat.Apps) _flow.Controls.Add(BuildCard(app));
        Shown += async (_, _) => await RefreshStatesAsync();
    }

    private Control BuildCard(SuiteApp app)
    {
        var c = Ctx.Colors; bool me = app.Id == Ctx.AppId; string lang = SuiteUi.L;
        var card = new Panel { Width = 440, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14), Margin = new Padding(6), BackColor = c.Bg };
        card.Paint += (_, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var pen = new Pen(c.Border); using var p = SuiteUi.Rounded(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10); e.Graphics.DrawPath(pen, p); };
        var hue = Math.Abs(app.Id.GetHashCode()) % 360;
        var logo = new AnimatedLogo(me ? Ctx.AppIcon : null, app.Name.Replace("Opsis ", "")[..1].ToUpperInvariant(), me ? c.Accent : FromHsv(hue, 0.6, 0.95), 70) { Location = new Point(14, 14) };
        card.Controls.Add(logo);
        var col = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Location = new Point(94, 12), Width = 330, BackColor = Color.Transparent };
        var name = SuiteUi.MakeLabel(app.Name + (me ? "   " + SuiteUi.T("Είσαι εδώ", "You are here") : ""), 13, c.Text, true, 320); col.Controls.Add(name);
        col.Controls.Add(SuiteUi.MakeLabel(app.TaglineFor(lang), 9.5f, c.Muted, false, 320));
        foreach (var b in app.CapabilitiesFor(lang).Take(6)) { var l = SuiteUi.MakeLabel("•  " + b, 9.5f, c.Text, false, 320); l.Margin = new Padding(0, 3, 0, 0); col.Controls.Add(l); }
        var state = SuiteUi.MakeLabel(SuiteUi.T("Έλεγχος…", "Checking…"), 9.5f, c.Accent, true, 320); state.Margin = new Padding(0, 8, 0, 4); col.Controls.Add(state);
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 4) };
        var open = SuiteUi.MakeButton(SuiteUi.T("Άνοιγμα", "Open"), c, false, 84); open.Visible = false;
        var install = SuiteUi.MakeButton(SuiteUi.T("Λήψη", "Download"), c, true, 96); install.Visible = !me;
        var details = SuiteUi.MakeButton(SuiteUi.T("Λεπτομέρειες", "Details"), c, false, 104);
        open.Click += (_, _) => OpenApp(app); install.Click += async (_, _) => await InstallAsync(app, install); details.Click += (_, _) => SuiteUi.OpenUrl(app.RepoUrl(_cat.Owner));
        row.Controls.AddRange(new Control[] { open, install, details }); col.Controls.Add(row);
        card.Controls.Add(col); _cards[app.Id] = (state, open, install);
        return card;
    }

    private static Color FromHsv(double h, double s, double v)
    {
        double ch = v * s, x = ch * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - ch; double r, g, b;
        if (h < 60) { r = ch; g = x; b = 0; } else if (h < 120) { r = x; g = ch; b = 0; } else if (h < 180) { r = 0; g = ch; b = x; } else if (h < 240) { r = 0; g = x; b = ch; } else if (h < 300) { r = x; g = 0; b = ch; } else { r = ch; g = 0; b = x; }
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    public async Task RefreshStatesAsync()
    {
        foreach (var app in _cat.Apps)
        {
            if (IsDisposed) return; var c = _cards[app.Id];
            var st = await Task.Run(() => app.Id == Ctx.AppId ? new InstallState { Installed = true, Version = Ctx.Version } : InstallDetector.Detect(app, Ctx.Probe));
            ReleaseDto? latest = null; if (st.Installed && app.Id != Ctx.AppId) latest = await LatestAsync(app);
            if (IsDisposed) return; _info[app.Id] = (st, latest);
            bool upd = latest != null && InstallDetector.UpdateAvailable(st, latest.Tag);
            c.State.Text = app.Id == Ctx.AppId ? SuiteUi.T("Εγκατεστημένο · v", "Installed · v") + Ctx.Version
                : !st.Installed ? SuiteUi.T("Δεν είναι εγκατεστημένο", "Not installed")
                : upd ? SuiteUi.T("Διαθέσιμη ενημέρωση: ", "Update available: ") + latest!.Tag + (st.Version.Length > 0 ? " (" + SuiteUi.T("έχεις ", "you have ") + st.Version + ")" : "")
                : SuiteUi.T("Εγκατεστημένο", "Installed") + (st.Version.Length > 0 ? " · v" + st.Version : "");
            c.Open.Visible = st.Installed && st.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && app.Id != Ctx.AppId;
            if (app.Id != Ctx.AppId) c.Install.Text = !st.Installed ? SuiteUi.T("Λήψη", "Download") : upd ? SuiteUi.T("Λήψη ενημέρωσης", "Download update") : SuiteUi.T("Λήψη ξανά", "Download again");
        }
    }

    private async Task<ReleaseDto?> LatestAsync(SuiteApp app)
    {
        string key = "rel." + app.Repo;
        var r = await Ctx.Http.GetAsync($"https://api.github.com/repos/{_cat.Owner}/{app.Repo}/releases/latest", Ctx.Store.Get(key + ".etag"), TimeSpan.FromSeconds(8));
        string? body = r.Status == 304 ? Ctx.Store.Get(key + ".body") : r.Ok ? r.Body : null;
        if (r.Ok) { Ctx.Store.Set(key + ".etag", r.ETag); Ctx.Store.Set(key + ".body", r.Body.Length < 200_000 ? r.Body : null); }
        var rel = ReleaseDto.Parse(body ?? ""); return rel == null || rel.Draft || rel.Prerelease ? null : rel;
    }

    private void OpenApp(SuiteApp app)
    {
        if (!_info.TryGetValue(app.Id, out var i) || string.IsNullOrEmpty(i.St.Path) || !File.Exists(i.St.Path)) return;
        try { Process.Start(new ProcessStartInfo(i.St.Path) { UseShellExecute = true }); } catch (Exception) { }
    }

    private async Task InstallAsync(SuiteApp app, Button btn)
    {
        btn.Enabled = false;
        try
        {
            var rel = (_info.TryGetValue(app.Id, out var i) ? i.Latest : null) ?? await LatestAsync(app);
            if (rel == null) { SuiteUi.OpenUrl(app.RepoUrl(_cat.Owner) + "/releases/latest"); return; }
            var asset = SafeInstaller.PickAsset(rel, app.InstallerAsset);
            if (asset == null) { SuiteUi.OpenUrl(rel.Url); return; }
            var expected = await SafeInstaller.ExpectedSha256Async(Ctx.Http, rel, app, asset);
            if (expected == null) { MessageBox.Show(this, SuiteUi.T("Δεν υπάρχει checksum για να επαληθευτεί το αρχείο, οπότε δεν θα κατέβει αυτόματα. Ανοίγω τη σελίδα της έκδοσης.", "There is no checksum to verify the file against, so it will not be downloaded automatically. Opening the release page."), app.Name, MessageBoxButtons.OK, MessageBoxIcon.Information); SuiteUi.OpenUrl(rel.Url); return; }
            string size = asset.Size > 0 ? (asset.Size / 1048576.0).ToString("0.0") + " MB" : "?", dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var ask = MessageBox.Show(this, SuiteUi.T("Να κατέβει ο installer;\n\nΑρχείο: ", "Download the installer?\n\nFile: ") + asset.Name + "\n" + SuiteUi.T("Μέγεθος: ", "Size: ") + size + "\n" + SuiteUi.T("Πηγή: ", "Source: ") + new Uri(asset.Url).Host + "\nSHA-256: " + expected[..16] + "…\n" + SuiteUi.T("Προορισμός: ", "Destination: ") + dir + "\n\n" + SuiteUi.T("Δεν θα εκτελεστεί αυτόματα.", "It will not be run automatically."), app.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (ask != DialogResult.Yes) return;
            btn.Text = SuiteUi.T("Λήψη…", "Downloading…");
            var res = await SafeInstaller.DownloadVerifiedAsync(Ctx.Http, asset, expected, dir);
            if (res.Status == DownloadStatus.Ok)
            {
                if (MessageBox.Show(this, SuiteUi.T("Κατέβηκε και επαληθεύτηκε (SHA-256).\n\n", "Downloaded and verified (SHA-256).\n\n") + res.Path + "\n\n" + SuiteUi.T("Να ανοίξει ο φάκελος;", "Open the folder?"), app.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    try { Process.Start("explorer.exe", "/select,\"" + res.Path + "\""); } catch (Exception) { }
            }
            else if (res.Status == DownloadStatus.Mismatch) MessageBox.Show(this, SuiteUi.T("ΑΠΟΡΡΙΦΘΗΚΕ: το SHA-256 του αρχείου δεν ταιριάζει με το αναμενόμενο. Το αρχείο διαγράφηκε.", "REFUSED: the file's SHA-256 does not match the expected value. The file was deleted."), app.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else MessageBox.Show(this, SuiteUi.T("Η λήψη απέτυχε. Δοκίμασε από τη σελίδα της έκδοσης.", "The download failed. Try the release page."), app.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if (!IsDisposed) { btn.Enabled = true; await RefreshStatesAsync(); } }
    }
}

/// <summary>Φορτώνει τον ενσωματωμένο κατάλογο / το εικονίδιο από τους πόρους του assembly που περιέχει αυτόν τον κώδικα.</summary>
public static class SuiteResources
{
    public static string Catalogue()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("suite.apps.json");
        if (s == null) return "";
        using var r = new StreamReader(s); return r.ReadToEnd();
    }

    public static Icon? AppIcon(int size = 128)
    {
        try { using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("suite.appicon.ico"); return s == null ? null : new Icon(s, new Size(size, size)); }
        catch (Exception) { return null; }
    }
}
