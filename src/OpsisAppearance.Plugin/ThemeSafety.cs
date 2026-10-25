using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;

namespace OpsisAppearance;

/// <summary>
/// Pre-flight check for Windows .theme files. A theme whose visual style (.msstyles) is not signed by Microsoft is refused by Windows unless
/// the system has been patched (for example with SecureUxTheme); on some systems applying one has even led to a sign-in loop. Opsis never patches
/// system files, so it only READS the theme, tells the user what will happen and lets them decide.
/// </summary>
public static class ThemeSafety
{
    public enum VisualStyleState { None, SignedByMicrosoft, Unsigned, SignedByOthers, Missing }

    public sealed record Report(string? VisualStylePath, VisualStyleState State, bool PatcherInstalled)
    {
        /// <summary>True when applying the theme would probably be refused (or worse) on this system.</summary>
        public bool NeedsWarning => !PatcherInstalled && State is VisualStyleState.Unsigned or VisualStyleState.SignedByOthers;
    }

    /// <summary>The value of [VisualStyles] Path= (environment variables expanded), or null when the theme has no custom visual style.</summary>
    public static string? ReadVisualStylePath(string themeFile)
    {
        bool inSection = false;
        foreach (var raw in File.ReadLines(themeFile))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { inSection = line.Equals("[VisualStyles]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inSection) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0 || !line[..eq].Trim().Equals("Path", StringComparison.OrdinalIgnoreCase)) continue;
            var value = line[(eq + 1)..].Trim().Trim('"');
            if (value.Length == 0) return null;
            value = Environment.ExpandEnvironmentVariables(value);
            return Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(themeFile) ?? "", value));
        }
        return null;
    }

    public static Report Check(string themeFile, Func<string, VisualStyleState>? signature = null, bool? patcherInstalled = null)
    {
        string? style;
        try { style = ReadVisualStylePath(themeFile); } catch (Exception) { return new Report(null, VisualStyleState.None, patcherInstalled ?? IsPatcherInstalled()); }
        bool patched = patcherInstalled ?? IsPatcherInstalled();
        if (style == null) return new Report(null, VisualStyleState.None, patched);
        var state = (signature ?? Signature)(style);
        return new Report(style, state, patched);
    }

    /// <summary>Authenticode state of a .msstyles file: Microsoft-signed, unsigned, signed by somebody else, or missing. (Presence and issuer only; Windows does the full verification.)</summary>
    public static VisualStyleState Signature(string msstyles)
    {
        if (!File.Exists(msstyles)) return VisualStyleState.Missing;
        try
        {
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(msstyles));
            return cert.Subject.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ? VisualStyleState.SignedByMicrosoft : VisualStyleState.SignedByOthers;
        }
        catch (Exception) { return VisualStyleState.Unsigned; }
    }

    /// <summary>True when a signature-verification patcher such as SecureUxTheme is installed (its provider DLL is in System32 or registered for winlogon).</summary>
    public static bool IsPatcherInstalled()
    {
        try
        {
            if (File.Exists(Path.Combine(Environment.SystemDirectory, "SecureUxTheme.dll"))) return true;
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\winlogon.exe");
            return key?.GetValue("VerifierDlls") is string s && s.Contains("SecureUxTheme", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    public static string Explain(Report r, bool greek)
    {
        string name = r.VisualStylePath == null ? "" : Path.GetFileName(r.VisualStylePath);
        return greek
            ? $"Αυτό το θέμα χρησιμοποιεί οπτικό στυλ ({name}) που δεν είναι υπογεγραμμένο από τη Microsoft. Τα Windows θα το απορρίψουν, εκτός αν έχει εγκατασταθεί εργαλείο που αφαιρεί τον έλεγχο υπογραφής (π.χ. SecureUxTheme)· σε ορισμένα συστήματα ένα τέτοιο θέμα έχει προκαλέσει βρόχο στην είσοδο (login loop). Το Opsis δεν αλλάζει αρχεία συστήματος.\n\nΝα εφαρμοστεί παρ' όλα αυτά;"
            : $"This theme uses a visual style ({name}) that is not signed by Microsoft. Windows will refuse it unless a signature-check patcher (for example SecureUxTheme) is installed; on some systems such a theme has caused a sign-in loop. Opsis does not modify system files.\n\nApply it anyway?";
    }
}
