using System.Text.RegularExpressions;
using ReleaseTool.Desktop.Helpers;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class ManifestAnalyzerService
{
    private static readonly Regex ScriptBlockRegex = new(
        @"(?:client_scripts?|server_scripts?|shared_scripts?|files)\s*\{([\s\S]*?)\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScriptLineRegex = new(
        @"['""](@?[^'""]+?)['""]",
        RegexOptions.Compiled);

    public static ManifestAnalysisResult Analyze(string resourcePath, string manifestContent, AppConfig config)
    {
        var referenced = ParseReferencedPaths(manifestContent);
        var missing = new List<string>();

        foreach (var rel in referenced)
        {
            if (rel.Contains('*') || rel.Contains('?'))
                continue;

            var normalized = PathHelper.ToPosix(rel).TrimStart('@', '/');
            if (normalized.StartsWith("resource/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized["resource/".Length..];

            var full = Path.Combine(resourcePath, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full) && !Directory.Exists(full))
                missing.Add(normalized);
        }

        var webWarning = CheckWebBuild(resourcePath, config);
        return new ManifestAnalysisResult
        {
            ReferencedFiles = referenced,
            MissingFiles = missing.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            WebBuildWarning = webWarning
        };
    }

    private static List<string> ParseReferencedPaths(string content)
    {
        var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match block in ScriptBlockRegex.Matches(content))
        {
            foreach (Match m in ScriptLineRegex.Matches(block.Groups[1].Value))
            {
                var p = m.Groups[1].Value.Trim();
                if (p.Length > 0 && !p.StartsWith("--", StringComparison.Ordinal))
                    output.Add(p);
            }
        }

        foreach (Match m in Regex.Matches(content, @"(?:client_script|server_script|shared_script)\s+['""]([^'""]+)['""]", RegexOptions.IgnoreCase))
            output.Add(m.Groups[1].Value.Trim());

        return output.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static string? CheckWebBuild(string resourcePath, AppConfig config)
    {
        var webDir = Path.Combine(resourcePath, "web");
        if (!Directory.Exists(webDir))
            return null;

        var publish = string.IsNullOrWhiteSpace(config.EscrowWebPublishFolder) ? "dist" : config.EscrowWebPublishFolder;
        var publishDir = Path.Combine(webDir, publish.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(publishDir) && Directory.EnumerateFileSystemEntries(publishDir).Any())
            return null;

        return $"web/{publish} klasörü boş veya yok. Escrow ZIP'te web build dahil olmayabilir.";
    }
}
