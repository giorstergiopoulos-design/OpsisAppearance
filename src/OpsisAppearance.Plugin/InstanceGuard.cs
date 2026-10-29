namespace OpsisAppearance;

/// <summary>
/// Ένα Opsis Appearance «κινητήρας» ανά συνεδρία χρήστη: το αυτόνομο πρόγραμμα και το plugin μέσα στο Opsis χρησιμοποιούν το ίδιο ονομασμένο mutex, ώστε να μην τρέχουν δύο
/// εφαρμογές εφέ/taskbar/slideshow ταυτόχρονα (δύο εγγραφείς των ίδιων ρυθμίσεων). Όποιος ξεκινήσει πρώτος το κρατά· ο άλλος δεν εφαρμόζει τίποτα και το λέει.
/// </summary>
public sealed class InstanceGuard : IDisposable
{
    /// <summary>Το όνομα που περιμένει και ο installer (AppMutex).</summary>
    public const string DefaultName = "OpsisAppearance.Engine";
    private Mutex? _mutex;

    public bool TryAcquire(string name = DefaultName)
    {
        if (_mutex != null) return true;
        var m = new Mutex(true, name, out bool createdNew);
        if (!createdNew) { m.Dispose(); return false; }
        _mutex = m; return true;
    }

    public bool IsOwner => _mutex != null;

    public void Release()
    {
        if (_mutex == null) return;
        try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }      // άλλο thread: το λειτουργικό το ελευθερώνει στο τέλος της διεργασίας
        _mutex.Dispose(); _mutex = null;
    }

    public void Dispose() => Release();
}
