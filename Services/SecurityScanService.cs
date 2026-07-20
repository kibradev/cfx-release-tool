using System.Text.RegularExpressions;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class SecurityScanService
{
    private static readonly string[] SensitiveNames =
    [
        ".env", "credentials.json", "secrets.json", "id_rsa", "id_ed25519",
        "password.txt", "apikey.txt", "token.txt"
    ];

    private static readonly Regex SecretPatterns =
        new(
            @"(api[_-]?key|secret|password|token|private[_-]?key)\s*[=:]\s*['""]?[\w\-]{8,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<string> ScanResource(string resourcePath, IEnumerable<string> zipCandidates, long maxEscrowMb)
    {
        var warnings = new List<string>();

        foreach (var rel in zipCandidates)
        {
            var name = Path.GetFileName(rel);
            if (SensitiveNames.Any(s => name.Equals(s, StringComparison.OrdinalIgnoreCase)))
                warnings.Add($"Hassas dosya adı ZIP'e girebilir: {rel}");

            var full = Path.Combine(resourcePath, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full) || new FileInfo(full).Length > 512_000)
                continue;

            try
            {
                var text = File.ReadAllText(full);
                if (SecretPatterns.IsMatch(text))
                    warnings.Add($"Olası gizli anahtar içeriği: {rel}");
            }
            catch
            {
                // binary skip
            }
        }

        var total = zipCandidates.Sum(p =>
        {
            var full = Path.Combine(resourcePath, p.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) ? new FileInfo(full).Length : 0;
        });

        var maxBytes = maxEscrowMb * 1024L * 1024L;
        if (total > maxBytes)
            warnings.Add($"Tahmini boyut {total / (1024 * 1024):N0} MB — limit {maxEscrowMb} MB");

        return warnings;
    }
}
