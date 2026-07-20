using System.IO.Compression;
using System.Text;
using ReleaseTool.Desktop.Helpers;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class ZipService
{
    private static readonly string[] WebNodeModulesExcludes = ["web/node_modules", "web/node_modules/**"];

    private static readonly string[] DefaultEscrowExtra =
    [
        "react_source", "react_source/**",
        "svelte_source", "svelte_source/**",
        "vue_source", "vue_source/**"
    ];

    private static readonly string[] DefaultExclude =
    [
        "node_modules", "web/node_modules", "web/node_modules/**",
        ".git", ".github", ".vscode", ".idea", ".cache",
        "cache", "tmp", "temp", "logs",
        "*.log", "*.zip",
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml"
    ];

    public static bool IsWebNodeModulesPath(string relPosix)
    {
        var p = relPosix.ToLowerInvariant();
        return p == "web/node_modules" || p.StartsWith("web/node_modules/", StringComparison.Ordinal);
    }

    public static bool IsZipPathExcluded(string relPosix, string fileName, IEnumerable<string> excludeList)
    {
        if (IsWebNodeModulesPath(relPosix))
            return true;

        var list = excludeList.ToList();
        return ExcludeHelper.ShouldExclude(fileName, list) || ExcludeHelper.ShouldExclude(relPosix, list);
    }

    public static bool IsEscrowWebExcludedExceptPublish(string relPosix, string webPublishFolder)
    {
        var p = PathHelper.ToPosix(relPosix);
        var pub = (webPublishFolder ?? "dist")
            .Replace('\\', '/')
            .Trim('/')
            .ToLowerInvariant();
        var lower = p.ToLowerInvariant();

        if (!lower.StartsWith("web/", StringComparison.Ordinal) && lower != "web")
            return false;

        if (lower == "web")
            return false;

        var prefix = $"web/{pub}";
        if (lower == prefix || lower.StartsWith(prefix + "/", StringComparison.Ordinal))
            return false;

        return true;
    }

    public static List<string> ParseReleaseIgnore(string resourcePath)
    {
        try
        {
            var ignoreFile = Path.Combine(resourcePath, "release.ignore");
            if (!File.Exists(ignoreFile))
                return [];

            return File.ReadAllLines(ignoreFile)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<string> StripOpensourceWebSourceConflicts(string resourcePath, List<string> merged)
    {
        var bases = new[] { "src", "source", "app" };
        var probes = new List<string>();

        foreach (var b in bases)
        {
            var abs = Path.Combine(resourcePath, "web", b);
            try
            {
                if (Directory.Exists(abs))
                {
                    probes.AddRange(
                    [
                        $"web/{b}",
                        $"web/{b}/x.ts",
                        $"web/{b}/x.tsx",
                        $"web/{b}/x.vue",
                        $"web/{b}/App.vue",
                        $"web/{b}/main.ts",
                        $"web/{b}/index.ts",
                        b
                    ]);
                }
            }
            catch
            {
                // skip
            }
        }

        if (probes.Count == 0)
            return merged;

        var uniq = probes.Distinct().ToList();
        return merged.Where(entry => !uniq.Any(p => ExcludeHelper.ShouldExclude(p, [entry]))).ToList();
    }

    public static List<string> GetZipExcludeList(string resourcePath, string mode, AppConfig config)
    {
        var configExcludes = config.Exclude.Count > 0 ? config.Exclude : DefaultExclude.ToList();
        var merged = configExcludes
            .Concat(WebNodeModulesExcludes)
            .Concat(ParseReleaseIgnore(resourcePath))
            .Distinct()
            .ToList();

        if (mode == "escrow")
        {
            var extra = config.EscrowZipExtraExcludes.Count > 0
                ? config.EscrowZipExtraExcludes
                : DefaultEscrowExtra.ToList();
            merged.AddRange(extra);
            return merged.Distinct().ToList();
        }

        var escrowOnly = new HashSet<string>(
            config.EscrowZipExtraExcludes.Count > 0
                ? config.EscrowZipExtraExcludes
                : DefaultEscrowExtra);

        var output = merged.Where(entry => !escrowOnly.Contains(entry)).ToList();
        output = output.Concat(WebNodeModulesExcludes).Distinct().ToList();
        return StripOpensourceWebSourceConflicts(resourcePath, output);
    }

    public static List<string> ListRelativeFilePathsForZip(string resourcePath, List<string> excludeList)
    {
        var root = Path.GetFullPath(resourcePath);
        var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Walk(string localPath, string zipPath)
        {
            string[] items;
            try
            {
                items = Directory.GetFileSystemEntries(localPath);
            }
            catch
            {
                return;
            }

            foreach (var fullLocalPath in items)
            {
                var item = Path.GetFileName(fullLocalPath);
                var fullZipPath = string.IsNullOrEmpty(zipPath) ? item : Path.Combine(zipPath, item);
                var posix = PathHelper.ToPosix(fullZipPath);

                if (IsZipPathExcluded(posix, item, excludeList))
                    continue;

                try
                {
                    var attr = File.GetAttributes(fullLocalPath);
                    if (attr.HasFlag(FileAttributes.Directory))
                        Walk(fullLocalPath, fullZipPath);
                    else
                        output.Add(posix);
                }
                catch
                {
                    // skip
                }
            }
        }

        Walk(root, "");
        return output.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public static List<string> CollectOpensourceLuaEscrowIgnorePatterns(string resourcePath, List<string> excludeList)
    {
        var luaPaths = ListRelativeFilePathsForZip(resourcePath, excludeList)
            .Where(p => p.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (luaPaths.Count == 0)
            return [];

        var patterns = new HashSet<string>();
        var byTop = new Dictionary<string, bool>();

        foreach (var f in luaPaths)
        {
            var parts = f.Split('/');
            if (parts.Length == 1)
            {
                patterns.Add("*.lua");
                continue;
            }

            var top = parts[0];
            var nested = parts.Length > 2;
            if (!byTop.ContainsKey(top))
                byTop[top] = false;
            if (nested)
                byTop[top] = true;
        }

        foreach (var (top, deep) in byTop)
            patterns.Add(deep ? $"{top}/**/*.lua" : $"{top}/*.lua");

        return patterns.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public static string PrepareManifestContent(
        string baseContent,
        string mode,
        string resourcePath,
        IEnumerable<string> escrowIgnorePaths,
        AppConfig config)
    {
        if (mode == "escrow")
        {
            var posixIgnores = escrowIgnorePaths
                .Select(p => PathHelper.ToPosix(p).TrimStart('.', '/'))
                .ToList();
            return ManifestService.UpsertEscrowIgnoreBlock(baseContent, posixIgnores);
        }

        var excludeList = GetZipExcludeList(resourcePath, mode, config);
        var luaGlobs = CollectOpensourceLuaEscrowIgnorePatterns(resourcePath, excludeList);
        return ManifestService.UpsertEscrowIgnoreBlock(baseContent, luaGlobs);
    }

    public static (long bytes, int fileCount) CreateZip(
        string resourcePath,
        string zipDestination,
        List<string> excludeList,
        string mode,
        string webPublishFolder,
        string manifestFile,
        string manifestContent)
    {
        var dir = Path.GetDirectoryName(zipDestination);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(zipDestination))
            File.Delete(zipDestination);

        var fileCount = 0;
        var manifestPosix = PathHelper.ToPosix(manifestFile);
        using var stream = new FileStream(zipDestination, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        void AddFolder(string localPath, string zipPath)
        {
            foreach (var fullLocalPath in Directory.GetFileSystemEntries(localPath))
            {
                var item = Path.GetFileName(fullLocalPath);
                var fullZipPath = string.IsNullOrEmpty(zipPath) ? item : Path.Combine(zipPath, item);
                var relPosix = PathHelper.ToPosix(fullZipPath);

                if (string.Equals(relPosix, manifestPosix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (IsZipPathExcluded(relPosix, item, excludeList))
                    continue;

                if (mode == "escrow" && IsEscrowWebExcludedExceptPublish(relPosix, webPublishFolder))
                    continue;

                var attr = File.GetAttributes(fullLocalPath);
                if (attr.HasFlag(FileAttributes.Directory))
                    AddFolder(fullLocalPath, fullZipPath);
                else
                {
                    var entry = archive.CreateEntry(relPosix, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(fullLocalPath);
                    fileStream.CopyTo(entryStream);
                    fileCount++;
                }
            }
        }

        AddFolder(resourcePath, "");

        var manifestEntry = archive.CreateEntry(manifestPosix, CompressionLevel.Optimal);
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(manifestContent)))
        using (var entryStream = manifestEntry.Open())
            ms.CopyTo(entryStream);
        fileCount++;

        if (fileCount == 0)
            throw new InvalidOperationException(
                mode == "escrow" ? "Escrow ZIP için dosya bulunamadı" : "Açık kaynak ZIP için dosya bulunamadı");

        stream.Flush();
        return (stream.Length, fileCount);
    }
}
