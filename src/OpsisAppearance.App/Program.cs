using Microsoft.Win32;
using Opsis.Suite;
using OpsisNewsCrawler.Plugin.Abstractions;

namespace OpsisAppearance.App;

/// <summary>
/// Stand-alone Opsis Appearance: a tray icon plus a window that hosts exactly the same page and engine as the Opsis plugin.
/// It plays the role of the host: it tells the plugin the language and the colours, calls Start/Stop on the UI thread and shows
/// the page when the plugin asks for it. Stopping the app restores every window effect (borders, corners, taskbar state).
/// Switches: --minimized (start in the tray, used by "start with Windows"), --show (open the window of an already running copy).
/// </summary>
internal static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

    [STAThread]
    private static void Main(string[] args)
    {
        // Test switch: --snapshot <file.png> renders the settings page to an image and exits (used to check the layout without a screen).
        if (Array.IndexOf(args, "--snapshot") is >= 0 and var at && at + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var plugin = new OpsisAppearancePlugin();
            plugin.SetLanguage(args.Contains("--el") ? "el" : "en");
            if (args.Contains("--light")) plugin.SetTheme(false, Color.FromArgb(255, 246, 248, 251).ToArgb(), Color.FromArgb(255, 255, 255, 255).ToArgb(), Color.FromArgb(255, 213, 219, 228).ToArgb(), Color.FromArgb(255, 24, 28, 36).ToArgb(), Color.FromArgb(255, 96, 105, 120).ToArgb(), Color.FromArgb(255, 0, 120, 212).ToArgb(), 1.0);
            else plugin.SetTheme(true, Color.FromArgb(255, 18, 22, 32).ToArgb(), Color.FromArgb(255, 28, 34, 48).ToArgb(), Color.FromArgb(255, 52, 62, 84).ToArgb(), Color.FromArgb(255, 235, 240, 250).ToArgb(), Color.FromArgb(255, 150, 160, 180).ToArgb(), Color.FromArgb(255, 0, 200, 230).ToArgb(), 1.0);
            var page = plugin.GetNavigationItems()[0].CreatePage();
            // Ένα top-level παράθυρο δεν μπορεί να ξεπεράσει το ύψος της οθόνης: η σελίδα μπαίνει σε ένα ψηλό panel-παιδί ώστε να αποδοθεί ολόκληρη.
            using var form = new Form { ClientSize = new Size(820, 900), StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            var host = new Panel { Location = Point.Empty, Size = new Size(820, 3200) };
            page.Dock = DockStyle.Fill; host.Controls.Add(page); form.Controls.Add(host); form.Show(); Application.DoEvents();
            using var bmp = new Bitmap(host.Width, host.Height);
            host.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(args[at + 1], System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        // Test switch: --family-snapshot <file.png> [--light]: renders the "Opsis family" window (offline catalogue) to an image and exits.
        if (Array.IndexOf(args, "--family-snapshot") is >= 0 and var fa && fa + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var ctx = AppearanceSuite.CreateContext(args.Contains("--light") ? SuiteColors.Light : SuiteColors.Dark, null, offline: true); ctx.ForceTop = true;
            var form = AppearanceSuite.ShowFamilyAsync(ctx).GetAwaiter().GetResult(); form.TopMost = true;
            var until = DateTime.UtcNow.AddSeconds(3); while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(30); }
            using var bmp = new Bitmap(form.Width, form.Height);
            using (var g = Graphics.FromImage(bmp)) { var hdc = g.GetHdc(); PrintWindow(form.Handle, hdc, 2); g.ReleaseHdc(hdc); }
            bmp.Save(args[fa + 1], System.Drawing.Imaging.ImageFormat.Png); return;
        }

        // Test switch: --suite-demo shows the welcome guide, the family window and a balloon with slowed-down animations (for screenshots). Uses no settings and no network.
        if (args.Contains("--suite-demo")) { ApplicationConfiguration.Initialize(); Application.Run(new SuiteDemo()); return; }

        using var mutex = new Mutex(true, @"Local\OpsisAppearance.SingleInstance", out bool first);
        if (!first) return;                              // already running: it lives in the tray

        ApplicationConfiguration.Initialize();
        try { Application.Run(new TrayContext(args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))); }
        catch (Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opsis");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "appearance-crash.log"), $"[{DateTime.Now:O}] {ex}\n");
            }
            catch (Exception) { }
            MessageBox.Show(ex.Message, "Opsis Appearance", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly OpsisAppearancePlugin _plugin = new();
    private readonly NotifyIcon _tray;
    private readonly Form _window;
    private readonly Icon _icon;
    private bool _exiting;
    private bool _greek;
    private readonly SuiteContext _suite;
    private readonly UpdateChecker _updater;
    private readonly System.Windows.Forms.Timer _updTimer = new() { Interval = 30_000 };   // πρώτος έλεγχος 30 s μετά την εκκίνηση, μετά κάθε 24 ώρες
    private string? _pendingUrl;

    public TrayContext(bool startMinimized)
    {
        _greek = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el";
        _icon = LoadIcon();

        _window = new Form
        {
            Text = "Opsis Appearance",
            Icon = _icon,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(820, 780),
            MinimumSize = new Size(640, 520),
        };
        _window.FormClosing += (_, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;                              // closing the window keeps the app (and its effects) in the tray
            _window.Hide();
        };

        PushLanguageAndTheme();
        _plugin.StyleWindow(_window);
        // Ο οδηγός, η «οικογένεια Opsis» και η ειδοποίηση νέας έκδοσης ανήκουν στην ΑΥΤΟΝΟΜΗ εφαρμογή· μέσα στο Opsis (plugin) δεν ξεκινά τίποτα μόνο του.
        _suite = AppearanceSuite.CreateContext(CurrentColors(), () => _window.Visible ? new Rectangle(_window.Left + 10, _window.Top + 8, 20, 20) : Rectangle.Empty);
        _updater = new UpdateChecker(_suite.Http, _suite.Store, () => DateTime.UtcNow, AppearanceSuite.Owner, AppearanceSuite.Repo, AppearanceSuite.Version);
        _plugin.Start();
        _plugin.NavigationRequested += _ => ShowWindow();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _tray = new NotifyIcon { Icon = _icon, Text = "Opsis Appearance", Visible = true, ContextMenuStrip = BuildMenu() };
        if (!_plugin.EngineActive)
            _tray.ShowBalloonTip(8000, "Opsis Appearance", SuiteLang.Current() == "el" ? "Το Opsis Appearance τρέχει ήδη (μέσα στο Opsis). Εδώ δεν εφαρμόζεται τίποτα ώστε να μην υπάρχουν δύο εφαρμογές εφέ." : "Opsis Appearance is already running (inside Opsis). Nothing is applied from here so two engines never fight.", ToolTipIcon.Info);
        _tray.DoubleClick += (_, _) => ShowWindow();
        _tray.BalloonTipClicked += (_, _) => OpenReleasePage();

        if (!startMinimized) ShowWindow();

        var welcome = new System.Windows.Forms.Timer { Interval = 1500 };
        welcome.Tick += (_, _) => { welcome.Dispose(); MaybeWelcome(); };
        welcome.Start();
        _updTimer.Tick += async (_, _) => { _updTimer.Interval = 24 * 60 * 60 * 1000; await CheckUpdateAsync(false); };
        _updTimer.Start();
    }

    private SuiteColors CurrentColors() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is not int v || v != 0 ? SuiteColors.Light : SuiteColors.Dark;

    private void MaybeWelcome()
    {
        if (!AppearanceSuite.ShouldAutoWelcome(_suite.Store, AppearanceSuite.Version)) return;
        _suite.Colors = CurrentColors();
        AppearanceSuite.ShowWelcome(_suite, w =>
        {
            if (w.Finished && _suite.Store.Get("suite.offered") != "1") { _suite.Store.Set("suite.offered", "1"); _ = AppearanceSuite.ShowFamilyAsync(_suite); }     // προσφέρεται ΜΙΑ φορά μετά τον οδηγό
        });
    }

    /// <summary>Έλεγχος νέας έκδοσης (ένα GET στο GitHub): balloon στο tray, μία φορά ανά έκδοση· το κλικ ανοίγει τη σελίδα της έκδοσης.</summary>
    private async Task CheckUpdateAsync(bool force)
    {
        UpdateInfo? info;
        try { info = await _updater.CheckAsync(force); } catch (Exception) { info = null; }
        if (info == null)
        {
            if (force) _tray.ShowBalloonTip(5000, "Opsis Appearance", _greek ? "Δεν υπάρχει νεότερη έκδοση (ή δεν ήταν δυνατός ο έλεγχος)." : "No newer version (or the check was not possible).", ToolTipIcon.Info);
            return;
        }
        _pendingUrl = info.Url;
        _tray.ShowBalloonTip(12000, "Opsis Appearance " + info.Tag, _greek ? "Διαθέσιμη νέα έκδοση — κάνε κλικ για να δεις τι νέο υπάρχει." : "A new version is available — click to see what's new.", ToolTipIcon.Info);
        _updater.MarkNotified(info.Tag);
    }

    private void OpenReleasePage()
    {
        var url = _pendingUrl; if (url == null || !SafeUrl.IsAllowed(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(_greek ? "Άνοιγμα" : "Open", null, (_, _) => ShowWindow());
        menu.Items.Add(new ToolStripSeparator());
        foreach (var item in _plugin.GetTrayMenuItems())
        {
            if (item.Key == "appearance.open") continue;     // "Open" above already does that
            var captured = item;
            menu.Items.Add(_greek ? captured.TitleEl : captured.TitleEn, null, (_, _) => captured.Invoke());
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_greek ? "Έλεγχος για νέα έκδοση τώρα" : "Check for a new version now", null, async (_, _) => await CheckUpdateAsync(true));
        var notify = new ToolStripMenuItem(_greek ? "Ειδοποίηση για νέες εκδόσεις" : "Notify about new versions") { CheckOnClick = true, Checked = _updater.NotifyEnabled };
        notify.CheckedChanged += (_, _) => _updater.NotifyEnabled = notify.Checked;
        menu.Items.Add(notify);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_greek ? "Έξοδος (επαναφορά εφέ παραθύρων)" : "Exit (restores window effects)", null, (_, _) => Exit());
        return menu;
    }

    private static Icon LoadIcon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
        catch (Exception) { return SystemIcons.Application; }
    }

    private void ShowWindow()
    {
        if (_window.Controls.Count == 0)
        {
            var page = _plugin.GetNavigationItems()[0].CreatePage();
            page.Dock = DockStyle.Fill;
            _window.Controls.Add(page);
        }
        if (!_window.Visible) _window.Show();
        if (_window.WindowState == FormWindowState.Minimized) _window.WindowState = FormWindowState.Normal;
        _window.Activate();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
            _window.BeginInvoke(new Action(PushLanguageAndTheme));
    }

    /// <summary>The window follows Windows: light or dark colours according to the current "app mode" (which this app itself may change).</summary>
    private void PushLanguageAndTheme()
    {
        bool light = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is not int v || v != 0;
        _plugin.SetLanguage(_greek ? "el" : "en");
        int Argb(int r, int g, int b) => Color.FromArgb(255, r, g, b).ToArgb();
        if (light)
            _plugin.SetTheme(false, Argb(245, 246, 250), Argb(255, 255, 255), Argb(208, 213, 224), Argb(25, 28, 36), Argb(105, 112, 128), Argb(0, 120, 212), 1.0);
        else
            _plugin.SetTheme(true, Argb(18, 22, 32), Argb(28, 34, 48), Argb(52, 62, 84), Argb(235, 240, 250), Argb(150, 160, 180), Argb(0, 200, 230), 1.0);
        _window.BackColor = light ? Color.FromArgb(245, 246, 250) : Color.FromArgb(18, 22, 32);
    }

    private void Exit()
    {
        _exiting = true;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _tray.Visible = false;
        try { _plugin.Stop(); } catch (Exception) { }         // restores borders, corners, backdrops and the taskbar state
        _window.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Dispose(); _window.Dispose(); _icon.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>--suite-demo: οδηγός (σελίδα 1 → τελευταία → κλείσιμο με αργό animation), παράθυρο οικογένειας, balloon. Χωρίς δίκτυο και χωρίς αποθήκευση ρυθμίσεων.</summary>
internal sealed class SuiteDemo : ApplicationContext
{
    public SuiteDemo() { SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext()); _ = Run(); }     // ώστε μετά από κάθε await να ξαναμπαίνουμε στο UI thread

    private async Task Run()
    {
        var tray = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Opsis Appearance" };
        var ctx = AppearanceSuite.CreateContext(SuiteColors.Dark, null, offline: true); ctx.MinimiseMs = 3000; ctx.ForceTop = true;
        await Task.Delay(1500);
        var w = AppearanceSuite.ShowWelcome(ctx); w.TopMost = true;
        await Task.Delay(4000); w.Go(w.PageCount - 1);
        await Task.Delay(4000); _ = w.CloseAsync(true);
        await Task.Delay(5500);
        var f = await AppearanceSuite.ShowFamilyAsync(ctx); f.TopMost = true;
        await Task.Delay(5000); _ = f.MinimiseAsync();
        await Task.Delay(4500);
        tray.ShowBalloonTip(15000, "Opsis Appearance v9.9.9", "A new version is available — click to see what's new.", ToolTipIcon.Info);
        await Task.Delay(6000);
        tray.Visible = false; tray.Dispose(); ExitThread();
    }
}
