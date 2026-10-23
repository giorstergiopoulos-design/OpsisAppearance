namespace OpsisAppearance;

/// <summary>Slideshow ρυθμίσεις: άλλαξε το wallpaper των Windows από έναν φάκελο ανά τακτά διαστήματα (όπως ο Wallpaper Manager του Seelen).</summary>
public sealed class SlideshowSettings
{
    public bool Enabled { get; set; }
    public string Folder { get; set; } = "";
    public int IntervalMinutes { get; set; } = 30;
    public bool Shuffle { get; set; } = true;
    public WallpaperStyle Style { get; set; } = WallpaperStyle.Fill;
    public string? LastFile { get; set; }
    public DateTime? LastChangeUtc { get; set; }
    /// <summary>Το wallpaper πριν ξεκινήσει το slideshow (για να επανέρχεται όταν το σβήσεις).</summary>
    public string? OriginalPath { get; set; }
    public WallpaperStyle OriginalStyle { get; set; } = WallpaperStyle.Fill;
}

public static class WallpaperSlideshow
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
    public const long MaxImageBytes = 40L * 1024 * 1024;

    /// <summary>Οι εικόνες του φακέλου (όχι υποφάκελοι), ταξινομημένες. Αγνοεί αρχεία που λείπουν/είναι τεράστια. Ποτέ exception.</summary>
    public static List<string> Images(string? folder)
    {
        var list = new List<string>();
        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return list;
            foreach (var f in Directory.EnumerateFiles(folder))
            {
                if (!Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)) continue;
                try { if (new FileInfo(f).Length is > 0 and <= MaxImageBytes) list.Add(f); } catch (IOException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>Η επόμενη εικόνα: τυχαία (ποτέ η ίδια δύο φορές στη σειρά όταν υπάρχουν ≥2) ή η επόμενη με τη σειρά, κυκλικά.</summary>
    public static string? Pick(IReadOnlyList<string> images, string? last, bool shuffle, Random? rng = null)
    {
        if (images.Count == 0) return null;
        if (images.Count == 1) return images[0];
        int at = last == null ? -1 : ToList(images).FindIndex(p => string.Equals(p, last, StringComparison.OrdinalIgnoreCase));
        if (!shuffle) return images[(at + 1) % images.Count];
        rng ??= Random.Shared;
        int pick;
        do pick = rng.Next(images.Count); while (pick == at);
        return images[pick];
    }

    private static List<string> ToList(IReadOnlyList<string> l) => l as List<string> ?? l.ToList();

    /// <summary>True όταν πέρασε το διάστημα από την τελευταία αλλαγή (ή δεν έχει γίνει ποτέ). Ο ελάχιστος χρόνος είναι 1 λεπτό.</summary>
    public static bool Due(SlideshowSettings s, DateTime utcNow) =>
        s.Enabled && (s.LastChangeUtc == null || utcNow - s.LastChangeUtc.Value >= TimeSpan.FromMinutes(Math.Max(1, s.IntervalMinutes)) || utcNow < s.LastChangeUtc.Value);
}
