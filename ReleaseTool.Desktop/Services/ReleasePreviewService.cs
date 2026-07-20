using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class ReleasePreviewService
{
    public static EscrowPreviewSummary Build(
        string resourcePath,
        IEnumerable<string> openPaths,
        AppConfig config)
    {
        var open = openPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allFiles = Directory.EnumerateFiles(resourcePath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(resourcePath, f).Replace('\\', '/'))
            .Where(f => !ShouldSkip(f, config))
            .ToList();

        long openBytes = 0, escrowBytes = 0;
        foreach (var rel in allFiles)
        {
            var full = Path.Combine(resourcePath, rel.Replace('/', Path.DirectorySeparatorChar));
            long len;
            try { len = new FileInfo(full).Length; }
            catch { continue; }

            if (open.Contains(rel))
                openBytes += len;
            else
                escrowBytes += len;
        }

        var warnings = SecurityScanService.ScanResource(resourcePath, allFiles, config.PortalMaxZipMb);

        return new EscrowPreviewSummary
        {
            TotalFiles = allFiles.Count,
            OpenFiles = open.Count,
            EscrowedFiles = allFiles.Count - open.Count,
            EstimatedEscrowBytes = escrowBytes,
            EstimatedOsBytes = openBytes + escrowBytes,
            OpenPaths = open.OrderBy(x => x).ToList(),
            Warnings = warnings
        };
    }

    public static string FormatSummary(EscrowPreviewSummary s)
    {
        var lines = new List<string>
        {
            $"Toplam dosya: {s.TotalFiles}",
            $"Açık (escrow_ignore): {s.OpenFiles}",
            $"Escrow'da kalacak: {s.EscrowedFiles}",
            $"Tahmini escrow boyutu: {s.EstimatedEscrowBytes / (1024.0 * 1024.0):F2} MB",
            $"Tahmini OS boyutu: {s.EstimatedOsBytes / (1024.0 * 1024.0):F2} MB",
            ""
        };

        if (s.Warnings.Count > 0)
        {
            lines.Add("Uyarılar:");
            lines.AddRange(s.Warnings.Select(w => "- " + w));
            lines.Add("");
        }

        lines.Add("Açık dosyalar:");
        lines.AddRange(s.OpenPaths.Take(30).Select(p => "  " + p));
        if (s.OpenPaths.Count > 30)
            lines.Add($"  … +{s.OpenPaths.Count - 30} daha");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool ShouldSkip(string rel, AppConfig config)
    {
        var name = Path.GetFileName(rel);
        return ZipService.IsZipPathExcluded(rel, name, config.Exclude)
            || ZipService.IsZipPathExcluded(rel, name, config.EscrowZipExtraExcludes);
    }
}
