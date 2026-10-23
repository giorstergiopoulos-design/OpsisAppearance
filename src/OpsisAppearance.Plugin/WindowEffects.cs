using System.Runtime.InteropServices;
using System.Text;

namespace OpsisAppearance;

public enum CornerStyle { Default, Round, RoundSmall, Square }
public enum DarkTitleBars { Off, On, FollowTheme }
public enum BackdropStyle { Default, None, Mica, Acrylic, Tabbed }

/// <summary>Εφέ παραθύρων (στο πνεύμα του SeelenUI): όλα μέσω των τεκμηριωμένων DWM API των Windows 11, χωρίς injection.</summary>
public sealed class EffectsSettings
{
    public bool ActiveBorder { get; set; }
    public int? BorderRgb { get; set; }                         // null = χρώμα accent των Windows
    public CornerStyle Corners { get; set; } = CornerStyle.Default;
    public DarkTitleBars DarkTitle { get; set; } = DarkTitleBars.Off;
    public BackdropStyle Backdrop { get; set; } = BackdropStyle.Default;
    public bool AutoHideTaskbar { get; set; }
    /// <summary>Χρώμα γραμμής τίτλου / κειμένου τίτλου (Windows 11 22H2+). null = προεπιλογή. Όπως το Mica For Everyone.</summary>
    public int? CaptionRgb { get; set; }
    public int? CaptionTextRgb { get; set; }
    /// <summary>Διεργασίες που ΔΕΝ αγγίζονται (π.χ. "chrome.exe", "game.exe"). Συγκρίνεται χωρίς διάκριση πεζών/κεφαλαίων· το «.exe» είναι προαιρετικό.</summary>
    public List<string> ExcludedProcesses { get; set; } = new();

    public bool AnyWindowEffect => ActiveBorder || Corners != CornerStyle.Default || DarkTitle != DarkTitleBars.Off || Backdrop != BackdropStyle.Default || CaptionRgb != null || CaptionTextRgb != null;

    public static string NormalizeProcess(string name)
    {
        var n = (name ?? "").Trim().Trim('"').ToLowerInvariant();
        return n.Length == 0 ? "" : n.EndsWith(".exe") ? n : n + ".exe";
    }

    public bool IsExcluded(string? processName) =>
        !string.IsNullOrEmpty(processName) && ExcludedProcesses.Any(e => NormalizeProcess(e) == NormalizeProcess(processName));
}

/// <summary>Ό,τι χρειάζεται η μηχανή από το σύστημα — interface ώστε να δοκιμάζεται με ψεύτικα παράθυρα.</summary>
public interface IDwmWindows
{
    IReadOnlyList<IntPtr> TopLevelWindows();
    bool IsEligible(IntPtr hwnd);
    bool IsAlive(IntPtr hwnd);
    void SetCorner(IntPtr hwnd, int preference);               // 0 default, 1 square, 2 round, 3 round-small
    void SetBorderColor(IntPtr hwnd, int colorRef);            // 0x00BBGGRR ή 0xFFFFFFFF = προεπιλογή
    void SetDarkTitle(IntPtr hwnd, bool dark);
    void SetBackdrop(IntPtr hwnd, int type);                   // 0 auto, 1 none, 2 mica, 3 acrylic, 4 tabbed
    IntPtr Foreground { get; }
    /// <summary>Όνομα εκτελέσιμου του παραθύρου (π.χ. "chrome.exe"), ή null αν δεν είναι γνωστό.</summary>
    string? ProcessName(IntPtr hwnd) => null;
    void SetCaptionColor(IntPtr hwnd, int colorRef) { }        // 0x00BBGGRR ή 0xFFFFFFFF = προεπιλογή
    void SetCaptionTextColor(IntPtr hwnd, int colorRef) { }
}

public interface ITaskbarControl
{
    bool IsAutoHide { get; }
    void SetAutoHide(bool on);
}

/// <summary>
/// Εφαρμόζει τα εφέ σε όλα τα κανονικά παράθυρα εφαρμογών και θυμάται τι άγγιξε ώστε το <see cref="RestoreAll"/> να τα επαναφέρει ΠΛΗΡΩΣ.
/// Δεν γράφει τίποτα στο registry: τα εφέ ζουν όσο τρέχει το Opsis (όπως στο SeelenUI).
/// </summary>
public sealed class WindowEffectsEngine
{
    public const int ColorDefault = unchecked((int)0xFFFFFFFF);

    private readonly IDwmWindows _dwm;
    private readonly ITaskbarControl _taskbar;
    private readonly Func<bool> _systemIsDark;
    private readonly HashSet<IntPtr> _touched = new();
    private IntPtr _activeBordered = IntPtr.Zero;
    private bool? _taskbarOriginal;

    public EffectsSettings Settings { get; private set; } = new();
    public int AccentRgb { get; set; } = 0x0078D4;

    public WindowEffectsEngine(IDwmWindows dwm, ITaskbarControl taskbar, Func<bool> systemIsDark)
    { _dwm = dwm; _taskbar = taskbar; _systemIsDark = systemIsDark; }

    public IReadOnlyCollection<IntPtr> Touched => _touched;

    public static int ToColorRef(int rgb) => ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);

    private int BorderColorRef => ToColorRef(Settings.BorderRgb ?? AccentRgb);

    /// <summary>Νέες ρυθμίσεις: ό,τι σβήστηκε επαναφέρεται, ό,τι ενεργοποιήθηκε εφαρμόζεται σε όλα τα ανοιχτά παράθυρα.</summary>
    public void Apply(EffectsSettings s)
    {
        var old = Settings;
        Settings = s;

        // Παράθυρα: πρώτα καθάρισε ό,τι δεν ισχύει πια, μετά εφάρμοσε.
        foreach (var h in _touched.ToList())
        {
            if (!_dwm.IsAlive(h)) { _touched.Remove(h); continue; }
            if (old.Corners != CornerStyle.Default && s.Corners == CornerStyle.Default) _dwm.SetCorner(h, 0);
            if (old.DarkTitle != DarkTitleBars.Off && s.DarkTitle == DarkTitleBars.Off) _dwm.SetDarkTitle(h, _systemIsDark());
            if (old.Backdrop != BackdropStyle.Default && s.Backdrop == BackdropStyle.Default) _dwm.SetBackdrop(h, 0);
            if (old.ActiveBorder && !s.ActiveBorder) _dwm.SetBorderColor(h, ColorDefault);
            if (old.CaptionRgb != null && s.CaptionRgb == null) _dwm.SetCaptionColor(h, ColorDefault);
            if (old.CaptionTextRgb != null && s.CaptionTextRgb == null) _dwm.SetCaptionTextColor(h, ColorDefault);
            if (s.ExcludedProcesses.Count > 0 && s.IsExcluded(_dwm.ProcessName(h))) { RestoreWindow(h, s); _touched.Remove(h); }     // μπήκε στη λίστα εξαιρέσεων: γυρνά στο αρχικό
        }
        if (!s.ActiveBorder) _activeBordered = IntPtr.Zero;

        if (s.AnyWindowEffect)
            foreach (var h in _dwm.TopLevelWindows()) ApplyToWindow(h);
        if (s.ActiveBorder) OnForegroundChanged(_dwm.Foreground);

        ApplyTaskbar(s.AutoHideTaskbar);
    }

    /// <summary>Νέο ή εμφανισμένο παράθυρο: εφαρμόζονται γωνίες/τίτλος/backdrop.</summary>
    public void ApplyToWindow(IntPtr h)
    {
        if (h == IntPtr.Zero || !_dwm.IsEligible(h)) return;
        var s = Settings;
        if (s.ExcludedProcesses.Count > 0 && s.IsExcluded(_dwm.ProcessName(h)))
        {
            if (_touched.Remove(h)) RestoreWindow(h, s);
            return;
        }
        bool touched = false;
        if (s.CaptionRgb is int cap) { _dwm.SetCaptionColor(h, ToColorRef(cap)); touched = true; }
        if (s.CaptionTextRgb is int capText) { _dwm.SetCaptionTextColor(h, ToColorRef(capText)); touched = true; }
        if (s.Corners != CornerStyle.Default)
        {
            _dwm.SetCorner(h, s.Corners switch { CornerStyle.Round => 2, CornerStyle.RoundSmall => 3, CornerStyle.Square => 1, _ => 0 });
            touched = true;
        }
        if (s.DarkTitle != DarkTitleBars.Off)
        {
            _dwm.SetDarkTitle(h, s.DarkTitle == DarkTitleBars.On || _systemIsDark());
            touched = true;
        }
        if (s.Backdrop != BackdropStyle.Default)
        {
            _dwm.SetBackdrop(h, s.Backdrop switch { BackdropStyle.None => 1, BackdropStyle.Mica => 2, BackdropStyle.Acrylic => 3, BackdropStyle.Tabbed => 4, _ => 0 });
            touched = true;
        }
        if (touched) _touched.Add(h);
    }

    /// <summary>Άλλαξε το ενεργό παράθυρο: το προηγούμενο χάνει το χρωματιστό border, το νέο το παίρνει.</summary>
    public void OnForegroundChanged(IntPtr h)
    {
        if (!Settings.ActiveBorder) return;
        if (_activeBordered != IntPtr.Zero && _activeBordered != h && _dwm.IsAlive(_activeBordered)) _dwm.SetBorderColor(_activeBordered, ColorDefault);
        if (h != IntPtr.Zero && _dwm.IsEligible(h) && !Settings.IsExcluded(_dwm.ProcessName(h)))
        {
            _dwm.SetBorderColor(h, BorderColorRef);
            _touched.Add(h);
            _activeBordered = h;
        }
        else _activeBordered = IntPtr.Zero;
    }

    private void RestoreWindow(IntPtr h, EffectsSettings s)
    {
        if (!_dwm.IsAlive(h)) return;
        _dwm.SetCorner(h, 0);
        _dwm.SetBorderColor(h, ColorDefault);
        _dwm.SetBackdrop(h, 0);
        _dwm.SetCaptionColor(h, ColorDefault);
        _dwm.SetCaptionTextColor(h, ColorDefault);
        if (s.DarkTitle != DarkTitleBars.Off) _dwm.SetDarkTitle(h, _systemIsDark());
    }

    private void ApplyTaskbar(bool autoHide)
    {
        if (autoHide)
        {
            _taskbarOriginal ??= _taskbar.IsAutoHide;
            if (!_taskbar.IsAutoHide) _taskbar.SetAutoHide(true);
        }
        else if (_taskbarOriginal != null)
        {
            _taskbar.SetAutoHide(_taskbarOriginal.Value);       // επαναφορά στην αρχική κατάσταση του χρήστη (όχι απλώς "ανενεργό")
            _taskbarOriginal = null;
        }
    }

    /// <summary>Επαναφέρει ΟΛΑ τα παράθυρα (και το taskbar) στην προεπιλογή. Καλείται όταν σταματά το plugin.</summary>
    public void RestoreAll()
    {
        foreach (var h in _touched.ToList()) RestoreWindow(h, Settings);
        _touched.Clear();
        _activeBordered = IntPtr.Zero;
        ApplyTaskbar(false);
        Settings = new EffectsSettings();
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Πραγματικές υλοποιήσεις (Win32)
// ---------------------------------------------------------------------------------------------------------------------

public sealed class RealDwmWindows : IDwmWindows
{
    private delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

    private const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    private const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_EX_TOOLWINDOW = 0x80;
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
        { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland", "XamlExplorerHostIslandWindow", "Windows.UI.Core.CoreWindow" };

    public IntPtr Foreground => GetForegroundWindow();
    public bool IsAlive(IntPtr h) => IsWindow(h);

    public IReadOnlyList<IntPtr> TopLevelWindows()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) => { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    /// <summary>Μόνο κανονικά παράθυρα εφαρμογών (με τίτλο/πλαίσιο, ορατά, όχι tool windows, όχι κέλυφος των Windows).</summary>
    public bool IsEligible(IntPtr h)
    {
        try
        {
            if (!IsWindow(h) || !IsWindowVisible(h)) return false;
            long style = GetWindowLongPtr(h, GWL_STYLE).ToInt64(), ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
            if ((ex & WS_EX_TOOLWINDOW) != 0 || (style & WS_CAPTION) != WS_CAPTION && (style & WS_THICKFRAME) == 0) return false;
            if (GetWindow(h, 4 /*GW_OWNER*/) != IntPtr.Zero) return false;
            var sb = new StringBuilder(128); GetClassName(h, sb, 128);
            return !Ignored.Contains(sb.ToString());
        }
        catch (Exception) { return false; }
    }

    private static void Set(IntPtr h, int attr, int value) { try { DwmSetWindowAttribute(h, attr, ref value, sizeof(int)); } catch (Exception) { } }
    public void SetCorner(IntPtr h, int pref) => Set(h, 33, pref);
    public void SetBorderColor(IntPtr h, int colorRef) => Set(h, 34, colorRef);
    public void SetDarkTitle(IntPtr h, bool dark) => Set(h, 20, dark ? 1 : 0);
    public void SetBackdrop(IntPtr h, int type) => Set(h, 38, type);
    public void SetCaptionColor(IntPtr h, int colorRef) => Set(h, 35, colorRef);          // DWMWA_CAPTION_COLOR
    public void SetCaptionTextColor(IntPtr h, int colorRef) => Set(h, 36, colorRef);      // DWMWA_TEXT_COLOR

    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    private readonly Dictionary<uint, string?> _names = new();
    public string? ProcessName(IntPtr h)
    {
        try
        {
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == 0) return null;
            if (_names.TryGetValue(pid, out var cached)) return cached;
            string? name = null;
            try { using var p = System.Diagnostics.Process.GetProcessById((int)pid); name = p.ProcessName + ".exe"; } catch (Exception) { }
            if (_names.Count > 512) _names.Clear();
            return _names[pid] = name;
        }
        catch (Exception) { return null; }
    }
}

public sealed class RealTaskbar : ITaskbarControl
{
    [StructLayout(LayoutKind.Sequential)] private struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public int l, t, r, b; public IntPtr lParam; }
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    private const uint ABM_GETSTATE = 4, ABM_SETSTATE = 10;
    private const int ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2;

    public bool IsAutoHide { get { var d = New(); return ((int)SHAppBarMessage(ABM_GETSTATE, ref d) & ABS_AUTOHIDE) != 0; } }

    public void SetAutoHide(bool on)
    {
        var d = New();
        d.lParam = (IntPtr)(on ? ABS_AUTOHIDE | ABS_ALWAYSONTOP : ABS_ALWAYSONTOP);
        SHAppBarMessage(ABM_SETSTATE, ref d);
    }

    private static APPBARDATA New() => new() { cbSize = Marshal.SizeOf<APPBARDATA>() };
}

/// <summary>Ειδοποιήσεις από τα Windows (αλλαγή ενεργού παραθύρου, νέο παράθυρο) χωρίς polling: SetWinEventHook στο UI thread.</summary>
public sealed class WinEventWatcher : IDisposable
{
    private delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_OBJECT_SHOW = 0x8002, WINEVENT_OUTOFCONTEXT = 0;
    private readonly WinEventProc _proc;        // κρατιέται ζωντανό (αλλιώς ο GC θα το μάζευε και θα κρασάριζε το hook)
    private IntPtr _fg, _show;

    public WinEventWatcher(Action<IntPtr> onForeground, Action<IntPtr> onShown)
    {
        _proc = (_, evt, hwnd, idObject, _, _, _) =>
        {
            try
            {
                if (evt == EVENT_SYSTEM_FOREGROUND) onForeground(hwnd);
                else if (evt == EVENT_OBJECT_SHOW && idObject == 0 /*OBJID_WINDOW*/) onShown(hwnd);
            }
            catch (Exception) { }
        };
        _fg = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
        _show = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void Dispose()
    {
        if (_fg != IntPtr.Zero) UnhookWinEvent(_fg);
        if (_show != IntPtr.Zero) UnhookWinEvent(_show);
        _fg = _show = IntPtr.Zero;
    }
}
