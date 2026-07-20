using System.IO.Compression;

namespace ReleaseTool.Desktop.Services;

public static class ReleaseDiffService
{
    public static string CompareWithPrevious(string? previousZipPath, string newZipPath)
    {
        if (string.IsNullOrEmpty(previousZipPath) || !File.Exists(previousZipPath))
            return "Önceki release ZIP bulunamadı — karşılaştırma yapılamadı.";

        if (!File.Exists(newZipPath))
            return "Yeni ZIP bulunamadı.";

        var oldEntries = ReadEntryNames(previousZipPath);
        var newEntries = ReadEntryNames(newZipPath);

        var added = newEntries.Except(oldEntries, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var removed = oldEntries.Except(newEntries, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var common = newEntries.Intersect(oldEntries, StringComparer.OrdinalIgnoreCase).Count();

        var lines = new List<string>
        {
            $"Önceki: {Path.GetFileName(previousZipPath)}",
            $"Yeni: {Path.GetFileName(newZipPath)}",
            $"Ortak: {common}",
            $"Eklenen: {added.Count}",
            $"Silinen: {removed.Count}",
            ""
        };

        if (added.Count > 0)
        {
            lines.Add("--- Eklenen ---");
            lines.AddRange(added.Take(40));
            if (added.Count > 40) lines.Add("…");
        }

        if (removed.Count > 0)
        {
            lines.Add("");
            lines.Add("--- Silinen ---");
            lines.AddRange(removed.Take(40));
            if (removed.Count > 40) lines.Add("…");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static HashSet<string> ReadEntryNames(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => e.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
