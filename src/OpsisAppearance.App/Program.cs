using Microsoft.Win32;
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
    [STAThread]
    private static void Main(string[] args)
    {
        // Test switch: --snapshot <file.png> renders the settings page to an image and exits (used to check the layout without a screen).
        if (Array.IndexOf(args, "--snapshot") is >= 0 and var at && at + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var plugin = new OpsisAppearancePlugin();
            plugin.SetLanguage(args.Contains("--el") ? "el" : "en");
            plugin.SetTheme(true, Color.FromArgb(255, 18, 22, 32).ToArgb(), Color.FromArgb(255, 28, 34, 48).ToArgb(), Color.FromArgb(255, 52, 62, 84).ToArgb(), Color.FromArgb(255, 235, 240, 250).ToArgb(), Color.FromArgb(255, 150, 160, 180).ToArgb(), Color.FromArgb(255, 0, 200, 230).ToArgb(), 1.0);
            var page = plugin.GetNavigationItems()[0].CreatePage();
            using var form = new Form { ClientSize = new Size(820, 1250), StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            form.Controls.Add(page); form.Show(); Application.DoEvents();
            using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            page.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(args[at + 1], System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

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
        _plugin.Start();
        _plugin.NavigationRequested += _ => ShowWindow();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _tray = new NotifyIcon { Icon = _icon, Text = "Opsis Appearance", Visible = true, ContextMenuStrip = BuildMenu() };
        _tray.DoubleClick += (_, _) => ShowWindow();

        if (!startMinimized) ShowWindow();
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
