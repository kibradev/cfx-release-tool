using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class ResourceScanner
{
    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".github", ".vscode", ".idea", ".cache", "cache", "tmp", "temp", "logs"
    };

    public static List<ResourceInfo> ScanResources(string resourcesFolder, int maxDepth = 6)
    {
        if (!Directory.Exists(resourcesFolder))
            return [];

        if (ManifestService.FindManifestPath(resourcesFolder) != null)
            return [CreateResourceInfo(resourcesFolder)];

        var results = new List<ResourceInfo>();
        ScanChildren(resourcesFolder, 0, maxDepth, results);
        return results.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ScanChildren(string current, int depth, int maxDepth, List<ResourceInfo> results)
    {
        if (depth > maxDepth)
            return;

        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories(current);
        }
        catch
        {
            return;
        }

        foreach (var dir in dirs)
        {
            var name = Path.GetFileName(dir);
            if (SkipDirNames.Contains(name))
                continue;

            if (ManifestService.FindManifestPath(dir) != null)
            {
                results.Add(CreateResourceInfo(dir));
                continue;
            }

            ScanChildren(dir, depth + 1, maxDepth, results);
        }
    }

    private static ResourceInfo CreateResourceInfo(string path)
    {
        string? version = null;
        var manifest = ManifestService.FindManifestPath(path);
        if (manifest != null)
        {
            try
            {
                var content = ManifestService.ReadManifest(path, manifest);
                version = ManifestService.GetCurrentVersion(content)?.Full;
            }
            catch
            {
                // skip
            }
        }

        return new ResourceInfo
        {
            Name = Path.GetFileName(path),
            Path = path,
            Version = version,
            HasManifest = manifest != null
        };
    }
}
