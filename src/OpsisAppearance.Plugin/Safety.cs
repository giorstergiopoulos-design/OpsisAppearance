using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace OpsisAppearance;

// ---------------------------------------------------------------------------------------------------------------------
// Δίχτυ ασφαλείας για παρεμβατικές αλλαγές: (1) BackupVault = αντίγραφο ΤΟΥ ΑΡΧΙΚΟΥ αρχείου των Windows πριν από οποιαδήποτε επέμβαση σε αρχείο
// και ελεγμένη επαναφορά, (2) σημείο επαναφοράς συστήματος πριν από την πρώτη παρεμβατική ρύθμιση.
// Το plugin ΔΕΝ τροποποιεί σήμερα κανένα αρχείο των Windows (ούτε DLL ούτε explorer.exe)· το BackupVault υπάρχει ώστε ΚΑΘΕ μελλοντική επέμβαση
// σε αρχείο να περνά υποχρεωτικά από εδώ.
// ---------------------------------------------------------------------------------------------------------------------

public sealed class VaultEntry
{
    public string OriginalPath { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Length { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed record VaultResult(bool Ok, string Message, VaultEntry? Entry = null);

/// <summary>
/// Φύλαξη αντιγράφων των ΑΡΧΙΚΩΝ αρχείων. Κανόνες: (α) ΠΟΤΕ δεν αντικαθίσταται υπάρχον αντίγραφο (το πρώτο = το γνήσιο αρχικό), (β) το αντίγραφο ελέγχεται
/// με SHA-256 αμέσως μετά τη δημιουργία και πριν από κάθε επαναφορά, (γ) η επαναφορά γίνεται σε προσωρινό αρχείο δίπλα στον στόχο και μετά αντικατάσταση
/// με έλεγχο, ώστε μια διακοπή στη μέση να μην αφήνει μισό αρχείο, (δ) κανένα exception δεν διαρρέει: το αποτέλεσμα λέει τι έγινε.
/// </summary>
public sealed class BackupVault
{
    private readonly string _root;
    private string ManifestPath => Path.Combine(_root, "manifest.json");
    public BackupVault(string root) { _root = root; }
    public string Root => _root;

    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opsis", "appearance", "backups");

    public static string Sha256Of(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(fs));
    }

    private List<VaultEntry> Load()
    {
        try { return File.Exists(ManifestPath) ? JsonSerializer.Deserialize<List<VaultEntry>>(File.ReadAllText(ManifestPath)) ?? new() : new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    private void Save(List<VaultEntry> list)
    {
        Directory.CreateDirectory(_root);
        var tmp = ManifestPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, ManifestPath, overwrite: true);
    }

    private static string Key(string path) => Path.GetFullPath(path).ToLowerInvariant();

    public IReadOnlyList<VaultEntry> All() => Load();

    public VaultEntry? Find(string originalPath) => Load().FirstOrDefault(e => Key(e.OriginalPath) == Key(originalPath));

    /// <summary>Αντιγράφει το αρχείο ΠΡΙΝ το αγγίξεις. Αν υπάρχει ήδη αντίγραφο, το επιστρέφει αναλλοίωτο (δεν ξαναγράφεται ποτέ).</summary>
    public VaultResult Backup(string originalPath)
    {
        try
        {
            var existing = Find(originalPath);
            if (existing != null)
            {
                if (File.Exists(existing.BackupPath) && Sha256Of(existing.BackupPath) == existing.Sha256)
                    return new VaultResult(true, "A verified backup of the original already exists (kept).", existing);
                return new VaultResult(false, "The recorded backup is missing or damaged - refusing to replace it silently.", existing);
            }
            if (!File.Exists(originalPath)) return new VaultResult(false, "The file does not exist: " + originalPath);

            Directory.CreateDirectory(_root);
            string hash = Sha256Of(originalPath);
            string name = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Key(originalPath))))[..12] + "_" + Path.GetFileName(originalPath);
            string dest = Path.Combine(_root, name);
            if (File.Exists(dest)) return new VaultResult(false, "A backup file with this name already exists but is not in the manifest - refusing to overwrite it.");
            File.Copy(originalPath, dest, overwrite: false);
            if (Sha256Of(dest) != hash)
            {
                try { File.Delete(dest); } catch (IOException) { }
                return new VaultResult(false, "The copy did not match the original (hash differs) - nothing was recorded.");
            }
            var entry = new VaultEntry { OriginalPath = Path.GetFullPath(originalPath), BackupPath = dest, Sha256 = hash, Length = new FileInfo(dest).Length, CreatedUtc = DateTime.UtcNow };
            var all = Load(); all.Add(entry); Save(all);
            return new VaultResult(true, "Backup created and verified.", entry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new VaultResult(false, ex.Message); }
    }

    /// <summary>Γυρνά το αρχείο στην αρχική του μορφή. Αν το αντίγραφο λείπει ή είναι αλλοιωμένο δεν αγγίζει τίποτα.</summary>
    public VaultResult Restore(string originalPath)
    {
        try
        {
            var e = Find(originalPath);
            if (e == null) return new VaultResult(false, "There is no backup for this file.");
            if (!File.Exists(e.BackupPath)) return new VaultResult(false, "The backup file is missing.", e);
            if (Sha256Of(e.BackupPath) != e.Sha256) return new VaultResult(false, "The backup is damaged (hash mismatch) - the file was NOT touched.", e);

            string target = e.OriginalPath;
            if (File.Exists(target) && Sha256Of(target) == e.Sha256) return new VaultResult(true, "The file already is the original.", e);
            string tmp = target + ".opsis-restore.tmp";
            try
            {
                File.Copy(e.BackupPath, tmp, overwrite: true);
                if (Sha256Of(tmp) != e.Sha256) { File.Delete(tmp); return new VaultResult(false, "The temporary copy did not verify - the file was NOT touched.", e); }
                File.Move(tmp, target, overwrite: true);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
                throw;
            }
            return Sha256Of(target) == e.Sha256
                ? new VaultResult(true, "The original file was restored and verified.", e)
                : new VaultResult(false, "The restored file does not match the original hash.", e);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new VaultResult(false, ex.Message); }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Σημείο επαναφοράς συστήματος
// ---------------------------------------------------------------------------------------------------------------------

public enum RestorePointStatus { Created, NeedsAdministrator, Failed, Skipped }
public sealed record RestorePointResult(RestorePointStatus Status, string Message);

public interface IRestorePointService
{
    bool IsAdministrator { get; }
    RestorePointResult Create(string description);
}

/// <summary>SRSetRestorePointW (srclient.dll). Θέλει δικαιώματα διαχειριστή και ενεργή «Προστασία συστήματος»· τα Windows μπορεί επίσης να παραλείψουν
/// το σημείο αν δημιουργήθηκε άλλο πρόσφατα (προεπιλογή: 1 ανά 24 ώρες). Ποτέ δεν ζητά UAC μόνο του και ποτέ δεν μπλοκάρει την αλλαγή.</summary>
public sealed class RealRestorePointService : IRestorePointService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
    private struct RESTOREPOINTINFOW { public uint dwEventType, dwRestorePtType; public long llSequenceNumber; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szDescription; }
    [StructLayout(LayoutKind.Sequential, Pack = 8)] private struct STATEMGRSTATUS { public uint nStatus; public long llSequenceNumber; }
    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SRSetRestorePointW(ref RESTOREPOINTINFOW info, out STATEMGRSTATUS status);

    private const uint BEGIN_SYSTEM_CHANGE = 100, END_SYSTEM_CHANGE = 101, MODIFY_SETTINGS = 12;

    public bool IsAdministrator
    {
        get { try { using var id = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); } catch (Exception) { return false; } }
    }

    public RestorePointResult Create(string description)
    {
        if (!IsAdministrator) return new RestorePointResult(RestorePointStatus.NeedsAdministrator, "Creating a restore point needs administrator rights (run Opsis as administrator once, or create the point from System Properties > System Protection).");
        try
        {
            var info = new RESTOREPOINTINFOW { dwEventType = BEGIN_SYSTEM_CHANGE, dwRestorePtType = MODIFY_SETTINGS, szDescription = description.Length > 255 ? description[..255] : description };
            if (!SRSetRestorePointW(ref info, out var st) || st.nStatus != 0)
                return new RestorePointResult(RestorePointStatus.Failed, $"Windows refused the restore point (status {st.nStatus}). System Protection may be turned off for this drive.");
            var end = new RESTOREPOINTINFOW { dwEventType = END_SYSTEM_CHANGE, dwRestorePtType = MODIFY_SETTINGS, llSequenceNumber = st.llSequenceNumber, szDescription = "" };
            SRSetRestorePointW(ref end, out _);
            return new RestorePointResult(RestorePointStatus.Created, "Restore point requested. (Windows may skip it if another one was created in the last 24 hours.)");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or Win32Exception or InvalidOperationException)
        {
            return new RestorePointResult(RestorePointStatus.Failed, ex.Message);
        }
    }
}

public sealed class SafetySettings
{
    /// <summary>Δημιουργία σημείου επαναφοράς πριν από την ΠΡΩΤΗ παρεμβατική ρύθμιση (αν έχει δικαιώματα διαχειριστή).</summary>
    public bool RestorePointBeforeInvasive { get; set; } = true;
    public DateTime? LastAttemptUtc { get; set; }
    public string? LastStatus { get; set; }
}

/// <summary>Πολιτική: ζητά σημείο επαναφοράς το πολύ μία φορά ανά ημέρα και ΠΟΤΕ δεν εμποδίζει τη ρύθμιση να εφαρμοστεί.</summary>
public static class InvasiveGuard
{
    public static RestorePointResult? BeforeInvasiveChange(SafetySettings s, IRestorePointService svc, string description, DateTime utcNow, bool force = false)
    {
        if (!force && !s.RestorePointBeforeInvasive) return null;
        if (!force && s.LastAttemptUtc is DateTime last && utcNow - last < TimeSpan.FromHours(24) && utcNow >= last) return null;
        var r = svc.Create(description);
        s.LastAttemptUtc = utcNow;
        s.LastStatus = r.Status + ": " + r.Message;
        return r;
    }
}

/// <summary>Εκτελέσιμα των Windows μόνο με πλήρη διαδρομή από τον φάκελο των Windows — ποτέ μέσω PATH ή του τρέχοντος φακέλου (αποφυγή «binary planting»).</summary>
public static class TrustedExe
{
    public static string Windows(string exe) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), exe);
    public static string System32(string exe) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
    /// <summary>Μόνο υπαρκτό τοπικό .exe με πλήρη διαδρομή (όχι UNC/URL/συντόμευση): η διαδρομή προέρχεται από registry του χρήστη, άρα δεν εμπιστεύεται τυφλά.</summary>
    public static bool IsLaunchable(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\")) return false;
            return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
        }
        catch (Exception) { return false; }
    }
    public static bool IsBareName(string exe) => exe.IndexOfAny(new[] { (char)92, (char)47, (char)58 }) < 0;
}
