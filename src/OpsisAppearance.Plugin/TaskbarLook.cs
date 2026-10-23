using System.Runtime.InteropServices;
using System.Text;

namespace OpsisAppearance;

// ---------------------------------------------------------------------------------------------------------------------
// Εμφάνιση του taskbar (στο πνεύμα του TranslucentTB και του RoundedTB), χωρίς injection σε άλλη διεργασία και χωρίς αλλαγή αρχείων:
//  * TaskbarLookEngine: χρώμα/διαφάνεια του taskbar με SetWindowCompositionAttribute (διαφανές, blur, acrylic, συμπαγές) και διαφορετική κατάσταση όταν
//    υπάρχει μεγιστοποιημένο παράθυρο ή είναι ανοιχτό το Start/η Αναζήτηση.
//  * TaskbarShapeEngine: στρογγυλεμένο / «επιπλέον» taskbar με SetWindowRgn (περιοχή κοπής) — όπως ο RoundedTB.
// Και τα δύο ζουν μόνο όσο τρέχει το plugin και επαναφέρονται ΠΛΗΡΩΣ (RestoreAll). Δεν γράφουν τίποτα στο registry.
// ---------------------------------------------------------------------------------------------------------------------

public enum TaskbarState { Default, Clear, Blur, Acrylic, Opaque }

public sealed class TaskbarLookSettings
{
    public bool Enabled { get; set; }
    public TaskbarState Normal { get; set; } = TaskbarState.Clear;
    public TaskbarState Maximized { get; set; } = TaskbarState.Opaque;
    public TaskbarState StartOpen { get; set; } = TaskbarState.Default;
    public int TintRgb { get; set; } = 0x101418;
    public int OpacityPercent { get; set; } = 60;            // για Blur / Acrylic / Opaque

    public TaskbarLookSettings Clone() => (TaskbarLookSettings)MemberwiseClone();
}

public sealed class TaskbarShapeSettings
{
    public bool Enabled { get; set; }
    public int Radius { get; set; } = 12;                    // px
    public int MarginX { get; set; } = 8;                    // «επιπλέον» taskbar: κενό αριστερά/δεξιά
    public int MarginY { get; set; } = 4;                    // κενό πάνω/κάτω
}

public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>Ό,τι χρειάζονται οι μηχανές από τα Windows — interface ώστε να δοκιμάζονται με ψεύτικο taskbar.</summary>
public interface ITaskbarSurface
{
    IReadOnlyList<IntPtr> Taskbars();                         // Shell_TrayWnd + Shell_SecondaryTrayWnd
    bool IsAlive(IntPtr taskbar);
    void SetAccent(IntPtr taskbar, int accentState, int gradientColorAbgr);
    bool AnyMaximizedWindow();
    bool StartOrSearchActive();
    ScreenRect GetRect(IntPtr taskbar);
    void SetRegion(IntPtr taskbar, ScreenRect? regionInWindowCoords, int radius);   // null = καμία περιοχή (προεπιλογή)
}

public sealed class TaskbarLookEngine
{
    private readonly ITaskbarSurface _s;
    private readonly HashSet<IntPtr> _touched = new();
    private IReadOnlyList<IntPtr> _lastHandles = Array.Empty<IntPtr>();
    private TaskbarState? _lastState;
    private int _ticksSinceApply;

    public TaskbarLookSettings Settings { get; private set; } = new();
    public TaskbarState? CurrentState => _lastState;
    public IReadOnlyCollection<IntPtr> Touched => _touched;
    public TaskbarLookEngine(ITaskbarSurface surface) { _s = surface; }

    /// <summary>Χρειάζεται το plugin timer; Μόνο όταν οι καταστάσεις διαφέρουν (αλλιώς το taskbar δεν χρειάζεται παρακολούθηση).</summary>
    public bool NeedsTimer => Settings.Enabled && (Settings.Maximized != Settings.Normal || Settings.StartOpen != Settings.Normal);

    public static TaskbarState Decide(TaskbarLookSettings s, bool startActive, bool maximized) =>
        startActive ? s.StartOpen : maximized ? s.Maximized : s.Normal;

    /// <summary>ABGR: το alpha μπαίνει στο ψηλό byte. Clear = alpha 0 (πλήρως διαφανές).</summary>
    public static int GradientColor(TaskbarLookSettings s, TaskbarState state)
    {
        int rgb = s.TintRgb & 0xFFFFFF;
        int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        int alpha = state switch { TaskbarState.Clear => 0, TaskbarState.Opaque => 255, _ => (Math.Clamp(s.OpacityPercent, 0, 100) * 255 + 50) / 100 };
        return (alpha << 24) | (b << 16) | (g << 8) | r;
    }

    /// <summary>Αριθμοί ACCENT_STATE των Windows: 0 disabled, 1 gradient (συμπαγές), 2 transparent gradient, 3 blur behind, 4 acrylic.</summary>
    public static int AccentState(TaskbarState st) => st switch { TaskbarState.Clear => 2, TaskbarState.Blur => 3, TaskbarState.Acrylic => 4, TaskbarState.Opaque => 1, _ => 0 };

    public void Apply(TaskbarLookSettings s)
    {
        var old = Settings;
        Settings = s.Clone();
        if (!s.Enabled) { RestoreAll(); Settings = s.Clone(); return; }
        Refresh(force: true);
    }

    /// <summary>Καλείται από τον timer (ή όταν αλλάζει το ενεργό παράθυρο). Ξαναεφαρμόζει όταν αλλάζει η κατάσταση, όταν εμφανιστεί νέο taskbar
    /// (π.χ. επανεκκίνηση του Explorer) και περιοδικά, γιατί ο Explorer μπορεί να ξαναβάψει το taskbar.</summary>
    public void Tick() { if (Settings.Enabled) Refresh(force: false); }

    private void Refresh(bool force)
    {
        var handles = _s.Taskbars();
        var state = Decide(Settings, _s.StartOrSearchActive(), Settings.Maximized != Settings.Normal && _s.AnyMaximizedWindow());
        bool sameHandles = handles.SequenceEqual(_lastHandles);
        _ticksSinceApply++;
        if (!force && sameHandles && _lastState == state && _ticksSinceApply < 20) return;
        foreach (var h in handles)
        {
            if (!_s.IsAlive(h)) continue;
            _s.SetAccent(h, AccentState(state), state == TaskbarState.Default ? 0 : GradientColor(Settings, state));
            _touched.Add(h);
        }
        _lastHandles = handles.ToList(); _lastState = state; _ticksSinceApply = 0;
    }

    /// <summary>Επαναφέρει ΟΛΑ τα taskbar που αγγίχτηκαν στην προεπιλεγμένη εμφάνιση των Windows.</summary>
    public void RestoreAll()
    {
        foreach (var h in _touched.Concat(_s.Taskbars()).Distinct().ToList())
            if (_s.IsAlive(h)) _s.SetAccent(h, 0, 0);
        _touched.Clear(); _lastHandles = Array.Empty<IntPtr>(); _lastState = null; _ticksSinceApply = 0;
        Settings = new TaskbarLookSettings();
    }
}

public sealed class TaskbarShapeEngine
{
    private readonly ITaskbarSurface _s;
    private readonly Dictionary<IntPtr, ScreenRect> _applied = new();
    public TaskbarShapeSettings Settings { get; private set; } = new();
    public IReadOnlyCollection<IntPtr> Touched => _applied.Keys;
    public TaskbarShapeEngine(ITaskbarSurface surface) { _s = surface; }

    /// <summary>Η περιοχή κοπής σε συντεταγμένες του παραθύρου: το taskbar μικραίνει κατά τα περιθώρια, ώστε να «επιπλέει» με στρογγυλές γωνίες.</summary>
    public static ScreenRect? RegionFor(TaskbarShapeSettings s, ScreenRect window)
    {
        int mx = Math.Clamp(s.MarginX, 0, 200), my = Math.Clamp(s.MarginY, 0, 60);
        int w = window.Width, h = window.Height;
        if (w < 80 || h < 16 || w - 2 * mx < 80 || h - 2 * my < 12) return null;        // ποτέ αφύσικα μικρή περιοχή (π.χ. κάθετο ή κρυμμένο taskbar)
        return new ScreenRect(mx, my, w - mx, h - my);
    }

    public void Apply(TaskbarShapeSettings s)
    {
        Settings = s;
        if (!s.Enabled) { RestoreAll(); Settings = s; return; }
        Refresh(force: true);
    }

    public void Tick() { if (Settings.Enabled) Refresh(force: false); }

    private void Refresh(bool force)
    {
        var handles = _s.Taskbars();
        foreach (var gone in _applied.Keys.Where(k => !handles.Contains(k)).ToList()) _applied.Remove(gone);
        foreach (var h in handles)
        {
            if (!_s.IsAlive(h)) continue;
            var rect = _s.GetRect(h);
            if (!force && _applied.TryGetValue(h, out var prev) && prev == rect) continue;      // ίδιο μέγεθος: η περιοχή ισχύει ακόμη
            var region = RegionFor(Settings, rect);
            _s.SetRegion(h, region, Math.Clamp(Settings.Radius, 0, 40));
            if (region != null) _applied[h] = rect; else _applied.Remove(h);
        }
    }

    public void RestoreAll()
    {
        foreach (var h in _applied.Keys.Concat(_s.Taskbars()).Distinct().ToList())
            if (_s.IsAlive(h)) _s.SetRegion(h, null, 0);
        _applied.Clear();
        Settings = new TaskbarShapeSettings();
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Πραγματική υλοποίηση (Win32)
// ---------------------------------------------------------------------------------------------------------------------

public sealed class RealTaskbarSurface : ITaskbarSurface
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowW(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string cls, string? title);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr h, ref WindowCompositionAttribData data);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
    private delegate bool EnumProc(IntPtr h, IntPtr l);

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int AccentState, AccentFlags, GradientColor, AnimationId; }
    [StructLayout(LayoutKind.Sequential)] private struct WindowCompositionAttribData { public int Attribute; public IntPtr Data; public int SizeOfData; }

    private const int WCA_ACCENT_POLICY = 19, GWL_EXSTYLE = -20, DWMWA_CLOAKED = 14;
    private const long WS_EX_TOOLWINDOW = 0x80;

    public IReadOnlyList<IntPtr> Taskbars()
    {
        var list = new List<IntPtr>();
        var main = FindWindowW("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) list.Add(main);
        IntPtr sec = IntPtr.Zero;
        while ((sec = FindWindowExW(IntPtr.Zero, sec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero) list.Add(sec);
        return list;
    }

    public bool IsAlive(IntPtr h) => h != IntPtr.Zero && IsWindow(h);

    public void SetAccent(IntPtr h, int accentState, int gradientColorAbgr)
    {
        var policy = new AccentPolicy { AccentState = accentState, AccentFlags = accentState == 0 ? 0 : 2 /* ζωγραφίζει το gradient color σε όλο το παράθυρο */, GradientColor = gradientColorAbgr };
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttribData { Attribute = WCA_ACCENT_POLICY, Data = ptr, SizeOfData = size };
            SetWindowCompositionAttribute(h, ref data);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    public bool AnyMaximizedWindow()
    {
        bool found = false;
        EnumWindows((h, _) =>
        {
            try
            {
                if (!IsWindowVisible(h) || IsIconic(h) || !IsZoomed(h)) return true;
                if ((GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0) return true;
                if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;     // παράθυρα άλλου virtual desktop
                var sb = new StringBuilder(64); GetClassName(h, sb, 64);
                if (sb.ToString() is "Progman" or "WorkerW") return true;
                found = true; return false;
            }
            catch (Exception) { return true; }
        }, IntPtr.Zero);
        return found;
    }

    public bool StartOrSearchActive()
    {
        try
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            GetWindowThreadProcessId(fg, out uint pid);
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("SearchHost", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    public ScreenRect GetRect(IntPtr h) => GetWindowRect(h, out var r) ? new ScreenRect(r.Left, r.Top, r.Right, r.Bottom) : default;

    public void SetRegion(IntPtr h, ScreenRect? region, int radius)
    {
        if (region is not ScreenRect r) { SetWindowRgn(h, IntPtr.Zero, true); return; }
        IntPtr rgn = CreateRoundRectRgn(r.Left, r.Top, r.Right + 1, r.Bottom + 1, radius * 2, radius * 2);
        if (SetWindowRgn(h, rgn, true) == 0) DeleteObject(rgn);       // αποτυχία: το σύστημα δεν πήρε την ιδιοκτησία, το ελευθερώνουμε εμείς
    }
}
