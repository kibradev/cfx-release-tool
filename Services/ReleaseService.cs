using ReleaseTool.Desktop.Helpers;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public sealed class ReleaseService
{
    private readonly ConfigService _configService;

    public ReleaseService(ConfigService configService)
    {
        _configService = configService;
    }

    public static string? ValidateEscrowPaths(string resourcePath, IEnumerable<string> paths)
    {
        var root = Path.GetFullPath(resourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var p in paths)
        {
            var posix = PathHelper.ToPosix(p).TrimStart('.', '/');
            var joined = Path.GetFullPath(Path.Combine(root, posix.Replace('/', Path.DirectorySeparatorChar)));
            if (!joined.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(joined, root, StringComparison.OrdinalIgnoreCase))
            {
                return $"Geçersiz yol: {p}";
            }
        }

        return null;
    }

    public async Task<(string Version, List<ReleaseZipResult> Releases)> CreateDualReleaseAsync(
        string resourcePath,
        string resourceName,
        IEnumerable<string> escrowIgnorePaths,
        VersionBumpKind bumpKind,
        string? manualVersion = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ignoreList = escrowIgnorePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            var pathErr = ValidateEscrowPaths(resourcePath, ignoreList);
            if (pathErr != null)
                throw new InvalidOperationException(pathErr);

            var config = _configService.Load();
            var manifestFile = ManifestService.FindManifestPath(resourcePath)
                ?? throw new InvalidOperationException("fxmanifest.lua veya __resource.lua bulunamadı");

            var (content, vBefore) = ManifestService.ReadManifestEnsuringVersion(resourcePath, manifestFile);

            var versionForZip = vBefore.Full;
            if (!string.IsNullOrWhiteSpace(manualVersion))
            {
                versionForZip = manualVersion.Trim();
                content = ManifestService.SetVersion(content, versionForZip);
                ManifestService.WriteManifest(resourcePath, manifestFile, content);
                ChangelogService.UpdateChangelog(resourcePath, versionForZip);
            }
            else if (bumpKind != VersionBumpKind.None)
            {
                versionForZip = ManifestService.BumpVersionString(vBefore, bumpKind);
                content = ManifestService.SetVersion(content, versionForZip);
                ManifestService.WriteManifest(resourcePath, manifestFile, content);
                ChangelogService.UpdateChangelog(resourcePath, versionForZip);
            }

            progress?.Report("Escrow ZIP oluşturuluyor…");
            cancellationToken.ThrowIfCancellationRequested();

            var webPublish = string.IsNullOrWhiteSpace(config.EscrowWebPublishFolder)
                ? "dist"
                : config.EscrowWebPublishFolder;

            var escrowManifest = ZipService.PrepareManifestContent(
                content, "escrow", resourcePath, ignoreList, config);
            var opensourceManifest = ZipService.PrepareManifestContent(
                content, "opensource", resourcePath, ignoreList, config);

            var manifestPath = Path.Combine(resourcePath, manifestFile);
            File.WriteAllText(manifestPath, escrowManifest);

            var outDir = _configService.GetOutputFolder(config);
            var releases = new List<ReleaseZipResult>();

            var escrowZipName = $"{resourceName}-v.{versionForZip}-esc.zip";
            var escrowZipPath = Path.Combine(outDir, escrowZipName);
            var escrowExclude = ZipService.GetZipExcludeList(resourcePath, "escrow", config);
            var (escrowBytes, _) = ZipService.CreateZip(
                resourcePath, escrowZipPath, escrowExclude, "escrow", webPublish, manifestFile, escrowManifest);

            releases.Add(new ReleaseZipResult
            {
                Mode = "escrow",
                ZipName = escrowZipName,
                ZipPath = escrowZipPath,
                Bytes = escrowBytes
            });

            progress?.Report("OS ZIP oluşturuluyor…");
            cancellationToken.ThrowIfCancellationRequested();

            File.WriteAllText(manifestPath, opensourceManifest);

            var osZipName = $"{resourceName}-v.{versionForZip}-os.zip";
            var osZipPath = Path.Combine(outDir, osZipName);
            var osExclude = ZipService.GetZipExcludeList(resourcePath, "opensource", config);
            var (osBytes, _) = ZipService.CreateZip(
                resourcePath, osZipPath, osExclude, "opensource", webPublish, manifestFile, opensourceManifest);

            releases.Add(new ReleaseZipResult
            {
                Mode = "opensource",
                ZipName = osZipName,
                ZipPath = osZipPath,
                Bytes = osBytes
            });

            File.WriteAllText(manifestPath, escrowManifest);

            progress?.Report("Tamamlandı");
            return (versionForZip, releases);
        }, cancellationToken);
    }

    public string BumpVersionOnly(string resourcePath, VersionBumpKind kind)
    {
        var manifestFile = ManifestService.FindManifestPath(resourcePath)
            ?? throw new InvalidOperationException("fxmanifest.lua veya __resource.lua bulunamadı");

        var (content, vBefore) = ManifestService.ReadManifestEnsuringVersion(resourcePath, manifestFile);

        var next = ManifestService.BumpVersionString(vBefore, kind);
        content = ManifestService.SetVersion(content, next);
        ManifestService.WriteManifest(resourcePath, manifestFile, content);
        ChangelogService.UpdateChangelog(resourcePath, next);
        return next;
    }
}
