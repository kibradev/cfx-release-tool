using System.Text.RegularExpressions;
using ReleaseTool.Desktop.Helpers;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class ManifestService
{
    private static readonly Regex VersionRegex = new(
        @"version\s+['""](?:v)?(\d+)\.(\d+)(?:\.(\d+))?['""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EscrowIgnoreRegex = new(
        @"escrow_ignore\s*\{([\s\S]*?)\}",
        RegexOptions.Compiled);

    private static readonly Regex EscrowPathRegex = new(
        @"['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    private static readonly Regex FxVersionRegex = new(
        @"^(fx_version\s+[^\r\n]+)(\r?\n)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static string? FindManifestPath(string resourcePath)
    {
        foreach (var name in new[] { "fxmanifest.lua", "__resource.lua" })
        {
            if (File.Exists(Path.Combine(resourcePath, name)))
                return name;
        }

        return null;
    }

    public static string ReadManifest(string resourcePath, string manifestFile)
    {
        return File.ReadAllText(Path.Combine(resourcePath, manifestFile));
    }

    public static void WriteManifest(string resourcePath, string manifestFile, string content)
    {
        File.WriteAllText(Path.Combine(resourcePath, manifestFile), content);
    }

    public static VersionParts? GetCurrentVersion(string content)
    {
        var match = VersionRegex.Match(content);
        if (!match.Success)
            return null;

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        return new VersionParts
        {
            Full = $"{major}.{minor}.{patch}",
            Major = major,
            Minor = minor,
            Patch = patch
        };
    }

    public static string SetVersion(string content, string newVersion)
    {
        if (VersionRegex.IsMatch(content))
            return VersionRegex.Replace(content, $"version '{newVersion}'");

        return InsertVersionLine(content, newVersion);
    }

    /// <summary>
    /// version alanı yoksa varsayılan sürümü fx_version satırından sonra ekler.
    /// </summary>
    public static (string Content, VersionParts Version, bool Added) EnsureVersion(
        string content,
        string defaultVersion = "1.0.0")
    {
        var existing = GetCurrentVersion(content);
        if (existing != null)
            return (content, existing, false);

        var updated = InsertVersionLine(content, defaultVersion);
        var version = GetCurrentVersion(updated)
            ?? throw new InvalidOperationException($"version eklenemedi: {defaultVersion}");

        return (updated, version, true);
    }

    public static (string Content, VersionParts Version) ReadManifestEnsuringVersion(
        string resourcePath,
        string manifestFile,
        string defaultVersion = "1.0.0")
    {
        var content = ReadManifest(resourcePath, manifestFile);
        var (updated, version, added) = EnsureVersion(content, defaultVersion);
        if (added)
            WriteManifest(resourcePath, manifestFile, updated);
        return (updated, version);
    }

    private static string InsertVersionLine(string content, string version)
    {
        var line = $"version '{version}'\n";
        var fxMatch = FxVersionRegex.Match(content);
        if (fxMatch.Success)
        {
            var idx = content.IndexOf(fxMatch.Value, StringComparison.Ordinal) + fxMatch.Value.Length;
            return content[..idx] + line + content[idx..];
        }

        return line + content.TrimStart('\r', '\n', ' ', '\t');
    }

    public static string BumpPatchVersionString(VersionParts version)
    {
        return $"{version.Major}.{version.Minor}.{version.Patch + 1}";
    }

    public static string BumpVersionString(VersionParts version, VersionBumpKind kind)
    {
        return kind switch
        {
            VersionBumpKind.Major => $"{version.Major + 1}.0.0",
            VersionBumpKind.Minor => $"{version.Major}.{version.Minor + 1}.0",
            VersionBumpKind.Patch => BumpPatchVersionString(version),
            _ => version.Full
        };
    }

    public static string ExtractEscrowIgnoreBlock(string content, IEnumerable<string> paths)
    {
        var pathList = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (pathList.Count == 0)
            return "(escrow_ignore boş — hiçbir dosya açık kalmayacak)";

        var cleaned = Regex.Replace(RemoveEscrowIgnoreBlock(content), @"\n{3,}", "\n\n");
        var block = UpsertEscrowIgnoreBlock(cleaned, pathList);
        var match = EscrowIgnoreRegex.Match(block);
        return match.Success ? match.Value : block;
    }

    public static List<string> ParseEscrowIgnorePaths(string content)
    {
        var match = EscrowIgnoreRegex.Match(content);
        if (!match.Success)
            return [];

        var outList = new List<string>();
        foreach (Match m in EscrowPathRegex.Matches(match.Groups[1].Value))
            outList.Add(m.Groups[1].Value);

        return outList;
    }

    public static string RemoveEscrowIgnoreBlock(string content)
    {
        return Regex.Replace(content, @"\r?\n?escrow_ignore\s*\{[\s\S]*?\}\s*", "\n");
    }

    public static string UpsertEscrowIgnoreBlock(string content, IEnumerable<string> relativePaths)
    {
        var cleaned = Regex.Replace(RemoveEscrowIgnoreBlock(content), @"\n{3,}", "\n\n");
        var paths = relativePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count == 0)
            return cleaned.TrimEnd() + "\n";

        var lines = paths
            .Select(p => PathHelper.ToPosix(p).TrimStart('.', '/'))
            .Select(p => $"    '{p.Replace("'", "\\'")}'");

        var block = "escrow_ignore {\n" + string.Join(",\n", lines) + "\n}\n";
        var fxMatch = FxVersionRegex.Match(cleaned);
        if (fxMatch.Success)
        {
            var idx = cleaned.IndexOf(fxMatch.Value, StringComparison.Ordinal) + fxMatch.Value.Length;
            return cleaned[..idx] + block + cleaned[idx..];
        }

        return block + "\n" + cleaned;
    }
}
