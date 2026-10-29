using Opsis.UI;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace OpsisAppearance;

/// <summary>
/// The look of Opsis itself for every control of the page and its dialogs: pill buttons, rounded check boxes, round radio buttons,
/// drop-downs that open a flat dark menu, +/- number boxes, rounded text boxes, themed message boxes and dark scroll bars.
/// All controls read <see cref="OpsisSkin.P"/> (the palette the host sends through SetTheme) when they paint, so a theme change is one Invalidate.
/// The classes keep the API of the standard control they replace, so the page code did not have to change.
/// </summary>
internal static class OpsisSkin
{
    public static Palette P { get; private set; } = Palette.Dark;
    public static bool Greek { get; set; }
    public static event Action? Changed;

    public static readonly Font Body = new("Segoe UI", 9f);
    public static readonly Font Semibold = new("Segoe UI Semibold", 9f);

    public static bool IsDark => P.Bg.GetBrightness() < 0.5f;
    public static Color Hover => Mix(P.Surface, P.Text, 0.12f);
    public static Color Pressed => Mix(P.Surface, P.Accent, 0.30f);


    /// <summary>Οι χρωματικές τιμές του Opsis Appearance προς την κοινή βιβλιοθήκη Opsis.UI (ζωντανός προσαρμογέας, όχι αντίγραφο).</summary>
    private sealed class Tokens : Opsis.UI.IOpsisTheme
    {
        public bool IsDark => OpsisSkin.IsDark;
        public Color Background => P.Bg; public Color Surface => P.Surface; public Color Border => P.Border;
        public Color Text => P.Text; public Color TextMuted => P.Muted; public Color Accent => P.Accent;
        public Color SurfaceHover => OpsisSkin.Hover;
        public Color OnAccent => OpsisSkin.ReadableOn(P.Accent);
    }
    static OpsisSkin() => Opsis.UI.OpsisTheme.Use(new Tokens());

    public static void Apply(Palette p) { P = p; Opsis.UI.OpsisTheme.NotifyChanged(); Changed?.Invoke(); }

    public static Color Mix(Color a, Color b, float t) =>
        Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static Color ReadableOn(Color c) => c.GetBrightness() > 0.6f ? Color.FromArgb(20, 24, 32) : Color.White;

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? subApp, string? subIdList);

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Palette background/text for a window and a dark (or light) title bar with the palette's colours (Windows 11; older systems just get the dark flag).</summary>
    public static void StyleWindow(Form f)
    {
        void Paint()
        {
            f.BackColor = P.Bg; f.ForeColor = P.Text;
            if (!f.IsHandleCreated) return;
            try
            {
                int dark = IsDark ? 1 : 0, cap = ColorRef(P.Bg), txt = ColorRef(P.Text), border = ColorRef(P.Border);
                DwmSetWindowAttribute(f.Handle, 20, ref dark, 4);
                DwmSetWindowAttribute(f.Handle, 35, ref cap, 4);
                DwmSetWindowAttribute(f.Handle, 36, ref txt, 4);
                DwmSetWindowAttribute(f.Handle, 34, ref border, 4);
            }
            catch (Exception) { }
        }
        Paint();
        f.HandleCreated += (_, _) => Paint();
        Action handler = Paint;
        Changed += handler;
        f.Disposed += (_, _) => Changed -= handler;
    }

    /// <summary>Dark (or light) native scroll bars for scrolling panels and list boxes.</summary>
    public static void ThemeScrollbars(Control c)
    {
        void Do() { try { if (c.IsHandleCreated) SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null); } catch (Exception) { } }
        Do();
        c.HandleCreated += (_, _) => Do();
        Action handler = Do;
        Changed += handler;
        c.Disposed += (_, _) => Changed -= handler;
    }
}

internal abstract class SkinnedControl : Control
{
    private readonly Action _changed;
    protected SkinnedControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Font = OpsisSkin.Body;
        _changed = () => { if (!IsDisposed) { OnSkinChanged(); Invalidate(); } };
        OpsisSkin.Changed += _changed;
    }
    protected virtual void OnSkinChanged() { }
    protected Color ParentBack => Parent?.BackColor is { A: > 0 } c ? c : OpsisSkin.P.Bg;
    protected override void Dispose(bool disposing) { if (disposing) OpsisSkin.Changed -= _changed; base.Dispose(disposing); }
}

/// <summary>Κουμπί-δείγμα χρώματος (accent / χρώμα taskbar): μόνο η ζωγραφιά του δείγματος είναι δική μας, όλα τα άλλα κουμπιά είναι το κοινό Opsis.UI.FlatButton.</summary>
internal sealed class SwatchButton : FlatButton
{
    public Color? Swatch { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Swatch == null) { base.OnPaint(e); return; }
        var p = OpsisSkin.P; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor is { A: > 0 } pb ? pb : p.Bg);
        Color fill = Enabled ? Swatch.Value : OpsisSkin.Mix(p.Surface, p.Bg, 0.4f);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = OpsisSkin.Round(r, string.IsNullOrEmpty(Text) ? 8 : Height / 2))
        {
            using (var br = new SolidBrush(fill)) g.FillPath(br, path);
            using var pen = new Pen(Focused && Enabled ? p.Accent : OpsisSkin.Mix(fill, p.Text, 0.35f), Focused ? 1.5f : 1f); g.DrawPath(pen, path);
        }
        if (!string.IsNullOrEmpty(Text))
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), OpsisSkin.ReadableOn(fill), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

internal sealed class OpsisRadio : RadioButton
{
    private const int Dot = 18;
    private bool _hot;
    private readonly Action _changed;

    public OpsisRadio()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = OpsisSkin.Body; Cursor = Cursors.Hand;
        _changed = () => { if (!IsDisposed) Invalidate(); };
        OpsisSkin.Changed += _changed;
    }
    protected override void Dispose(bool disposing) { if (disposing) OpsisSkin.Changed -= _changed; base.Dispose(disposing); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hot = false; Invalidate(); }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = TextRenderer.MeasureText(string.IsNullOrEmpty(Text) ? " " : Text, Font, new Size(int.MaxValue, 0), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        return new Size(Dot + 8 + t.Width + 2, Math.Max(Dot, t.Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = OpsisSkin.P; var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor is { A: > 0 } pb ? pb : p.Bg);
        var ring = new Rectangle(1, (Height - Dot) / 2 + 1, Dot - 2, Dot - 2);
        using (var fill = new SolidBrush(p.Surface)) g.FillEllipse(fill, ring);
        using (var pen = new Pen(Checked ? p.Accent : _hot ? p.Accent : p.Muted, 1.5f)) g.DrawEllipse(pen, ring);
        if (Checked) { var inner = Rectangle.Inflate(ring, -4, -4); using var br = new SolidBrush(p.Accent); g.FillEllipse(br, inner); }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(Dot + 8, 0, Math.Max(0, Width - Dot - 8), Height), Enabled ? p.Text : p.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

/// <summary>Drop-down list (replaces ComboBox): a rounded box that opens a flat menu in the colours of the palette.</summary>
internal sealed class OpsisText : SkinnedControl
{
    private readonly TextBox _box = new() { BorderStyle = BorderStyle.None };
    public OpsisText()
    {
        Height = 30; Width = 120;
        _box.Font = OpsisSkin.Body;
        _box.Leave += (_, e) => OnLeave(e);
        _box.TextChanged += (_, e) => OnTextChanged(e);
        _box.GotFocus += (_, _) => Invalidate();
        _box.LostFocus += (_, _) => Invalidate();
        Controls.Add(_box);
        OnSkinChanged();
        DoLayout();
    }
    public override string Text { get => _box.Text; set => _box.Text = value ?? ""; }
    protected override void OnSkinChanged() { _box.BackColor = OpsisSkin.P.Surface; _box.ForeColor = Enabled ? OpsisSkin.P.Text : OpsisSkin.P.Muted; }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); _box.Enabled = Enabled; OnSkinChanged(); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); _box.Focus(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); DoLayout(); }
    private void DoLayout()
    {
        int h = _box.PreferredHeight;
        _box.SetBounds(10, Math.Max(2, (Height - h) / 2), Math.Max(10, Width - 20), h);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var p = OpsisSkin.P; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ParentBack);
        using var path = OpsisSkin.Round(new Rectangle(0, 0, Width - 1, Height - 1), 7);
        using (var br = new SolidBrush(Enabled ? p.Surface : OpsisSkin.Mix(p.Surface, p.Bg, 0.4f))) g.FillPath(br, path);
        using var pen = new Pen(_box.Focused ? p.Accent : p.Border); g.DrawPath(pen, path);
    }
}

/// <summary>Number box with − / + (replaces NumericUpDown). The value can also be typed.</summary>
internal sealed class OpsisNumeric : SkinnedControl
{
    private readonly TextBox _box = new() { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center };
    private decimal _value, _min, _max = 100;
    private int _decimals;
    private int _hotZone;     // -1 minus, +1 plus
    private const int Zone = 24;
    public event EventHandler? ValueChanged;

    public OpsisNumeric()
    {
        Height = 30; Width = 80; MinimumSize = new Size(66, 30); Cursor = Cursors.IBeam;
        _box.Font = OpsisSkin.Body;
        _box.Leave += (_, _) => Commit();
        _box.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Commit(); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Up) { Step(1); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Down) { Step(-1); e.SuppressKeyPress = true; } };
        _box.MouseWheel += (_, e) => Step(e.Delta > 0 ? 1 : -1);
        _box.GotFocus += (_, _) => Invalidate();
        _box.LostFocus += (_, _) => Invalidate();
        Controls.Add(_box);
        OnSkinChanged(); Display();
    }

    public decimal Minimum { get => _min; set { _min = value; Value = _value; } }
    public decimal Maximum { get => _max; set { _max = value; Value = _value; } }
    public int DecimalPlaces { get => _decimals; set { _decimals = value; Display(); } }
    public decimal Value
    {
        get => _value;
        set
        {
            decimal v = Math.Round(Math.Clamp(value, _min, _max), _decimals);
            bool changed = v != _value; _value = v; Display();
            if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Display() => _box.Text = _value.ToString("F" + _decimals);
    private void Commit()
    {
        if (decimal.TryParse(_box.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var v)
            || decimal.TryParse(_box.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out v)) Value = v;
        Display();
    }
    private void Step(int dir) { Commit(); Value = _value + dir; }

    protected override void OnSkinChanged() { _box.BackColor = OpsisSkin.P.Surface; _box.ForeColor = Enabled ? OpsisSkin.P.Text : OpsisSkin.P.Muted; }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); _box.Enabled = Enabled; OnSkinChanged(); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); _box.Focus(); }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int h = _box.PreferredHeight;
        _box.SetBounds(Zone, Math.Max(2, (Height - h) / 2), Math.Max(10, Width - Zone * 2), h);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int z = e.X < Zone ? -1 : e.X > Width - Zone ? 1 : 0;
        if (z != _hotZone) { _hotZone = z; Cursor = z == 0 ? Cursors.IBeam : Cursors.Hand; Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hotZone = 0; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (!Enabled) return; if (e.X < Zone) Step(-1); else if (e.X > Width - Zone) Step(1); else _box.Focus(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = OpsisSkin.P; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ParentBack);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = OpsisSkin.Round(r, 7))
        {
            using (var br = new SolidBrush(Enabled ? p.Surface : OpsisSkin.Mix(p.Surface, p.Bg, 0.4f))) g.FillPath(br, path);
            if (_hotZone != 0 && Enabled)
            {
                var zr = _hotZone < 0 ? new Rectangle(0, 0, Zone, Height - 1) : new Rectangle(Width - 1 - Zone, 0, Zone, Height - 1);
                var state = g.Save(); g.SetClip(path, CombineMode.Intersect);
                using (var hb = new SolidBrush(OpsisSkin.Hover)) g.FillRectangle(hb, zr);
                g.Restore(state);
            }
            using var pen = new Pen(_box.Focused ? p.Accent : p.Border); g.DrawPath(pen, path);
        }
        using var glyph = new Pen(Enabled ? p.Text : p.Muted, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float cy = Height / 2f - 0.5f;
        g.DrawLine(glyph, Zone / 2f - 4, cy, Zone / 2f + 4, cy);
        float px = Width - 1 - Zone / 2f;
        g.DrawLine(glyph, px - 4, cy, px + 4, cy); g.DrawLine(glyph, px, cy - 4, px, cy + 4);
    }
}

/// <summary>Message box in the colours of Opsis (replaces MessageBox) and a one-line prompt.</summary>
internal static class OpsisBox
{
    public static DialogResult Show(string text, string caption = "Opsis Appearance", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None) =>
        Show(null, text, caption, buttons, icon);

    public static DialogResult Show(IWin32Window? owner, string text, string caption = "Opsis Appearance", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    {
        var p = OpsisSkin.P; bool gr = OpsisSkin.Greek;
        using var f = NewDialog(caption);
        string glyph = icon switch { MessageBoxIcon.Warning => "!", MessageBoxIcon.Error => "×", MessageBoxIcon.Question => "?", MessageBoxIcon.Information => "i", _ => "" };
        Color glyphColor = icon is MessageBoxIcon.Warning ? Color.FromArgb(240, 170, 40) : icon is MessageBoxIcon.Error ? Color.FromArgb(230, 80, 80) : p.Accent;
        int left = 20;
        if (glyph.Length > 0)
        {
            var badge = new BadgeLabel(glyph, glyphColor) { Left = 20, Top = 20, Width = 32, Height = 32 };
            f.Controls.Add(badge); left = 66;
        }
        var label = new Label { Text = text, Left = left, Top = 22, AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = p.Text, BackColor = p.Bg, Font = OpsisSkin.Body };
        f.Controls.Add(label);
        int bottom = Math.Max(label.Bottom, 52) + 24;

        var defs = buttons switch
        {
            MessageBoxButtons.OK => new[] { (gr ? "OK" : "OK", DialogResult.OK) },
            MessageBoxButtons.OKCancel => new[] { ("OK", DialogResult.OK), (gr ? "Άκυρο" : "Cancel", DialogResult.Cancel) },
            MessageBoxButtons.YesNo => new[] { (gr ? "Ναι" : "Yes", DialogResult.Yes), (gr ? "Όχι" : "No", DialogResult.No) },
            MessageBoxButtons.YesNoCancel => new[] { (gr ? "Ναι" : "Yes", DialogResult.Yes), (gr ? "Όχι" : "No", DialogResult.No), (gr ? "Άκυρο" : "Cancel", DialogResult.Cancel) },
            _ => new[] { ("OK", DialogResult.OK) },
        };
        int width = Math.Max(left + label.Width + 24, 300);
        int x = width - 16;
        FlatButton? first = null, cancel = null;
        for (int i = defs.Length - 1; i >= 0; i--)
        {
            var (t, r) = defs[i];
            var b = new FlatButton { Text = t, DialogResult = r, AutoSize = false, Width = 92, Height = 30, Top = bottom, Primary = i == 0 };
            x -= b.Width; b.Left = x; x -= 8;
            f.Controls.Add(b);
            if (i == 0) first = b;
            if (r is DialogResult.Cancel or DialogResult.No) cancel ??= b;
        }
        f.ClientSize = new Size(width, bottom + 30 + 18);
        f.AcceptButton = first; f.CancelButton = cancel ?? first;
        if (buttons is MessageBoxButtons.YesNo) f.ControlBox = false;
        return owner == null ? f.ShowDialog() : f.ShowDialog(owner);
    }

    public static string? Prompt(IWin32Window? owner, string text, string initial)
    {
        var p = OpsisSkin.P; bool gr = OpsisSkin.Greek;
        using var f = NewDialog("Opsis Appearance");
        var l = new Label { Text = text, Left = 16, Top = 16, AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = p.Text, BackColor = p.Bg, Font = OpsisSkin.Body };
        var t = new OpsisText { Left = 16, Top = l.Bottom + 8, Width = 368, Text = initial };
        var ok = new FlatButton { Text = "OK", DialogResult = DialogResult.OK, AutoSize = false, Width = 92, Height = 30, Primary = true, Top = t.Bottom + 16 };
        var cancel = new FlatButton { Text = gr ? "Άκυρο" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = false, Width = 92, Height = 30, Top = ok.Top };
        cancel.Left = 384 - cancel.Width; ok.Left = cancel.Left - 8 - ok.Width;
        f.Controls.AddRange(new Control[] { l, t, ok, cancel });
        f.ClientSize = new Size(400, ok.Bottom + 16);
        f.AcceptButton = ok; f.CancelButton = cancel;
        return (owner == null ? f.ShowDialog() : f.ShowDialog(owner)) == DialogResult.OK ? t.Text : null;
    }

    private static Form NewDialog(string caption)
    {
        var f = new Form
        {
            Text = caption, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, Font = OpsisSkin.Body, AutoScaleMode = AutoScaleMode.None,
        };
        OpsisSkin.StyleWindow(f);
        return f;
    }

    private sealed class BadgeLabel : Control
    {
        private readonly string _glyph; private readonly Color _color;
        public BadgeLabel(string glyph, Color color) { _glyph = glyph; _color = color; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent?.BackColor ?? OpsisSkin.P.Bg);
            using (var br = new SolidBrush(Color.FromArgb(40, _color))) g.FillEllipse(br, 0, 0, Width - 1, Height - 1);
            using (var pen = new Pen(_color, 1.5f)) g.DrawEllipse(pen, 1, 1, Width - 3, Height - 3);
            using var f = new Font("Segoe UI Semibold", 13f);
            TextRenderer.DrawText(e.Graphics, _glyph, f, new Rectangle(0, 0, Width, Height), _color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
