using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public sealed class ReleaseOrchestrator
{
    private readonly ConfigService _configService = new();
    private readonly ReleaseService _releaseService;
    private readonly ReleaseHistoryService _history = new();
    private readonly PortalUploadService _portal = new();

    public ReleaseOrchestrator()
    {
        _releaseService = new ReleaseService(_configService);
    }

    public async Task PreReleaseAsync(
        string resourcePath,
        IEnumerable<string> escrowPaths,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        var config = _configService.Load();

        if (config.RunWebBuildBeforeRelease && WebBuildService.NeedsBuild(resourcePath, config))
            await WebBuildService.RunBuildAsync(resourcePath, config, progress, ct);

        var manifestFile = ManifestService.FindManifestPath(resourcePath);
        if (manifestFile != null)
        {
            var content = ManifestService.ReadManifest(resourcePath, manifestFile);
            var lint = ManifestLintService.Lint(content, manifestFile);
            var preview = ReleasePreviewService.Build(resourcePath, escrowPaths, config);
            var allWarnings = lint.Concat(preview.Warnings).Distinct().ToList();
            if (allWarnings.Count > 0)
                progress?.Report("Uyarılar: " + string.Join("; ", allWarnings.Take(3)));
        }
    }

    public async Task<(string Version, List<ReleaseZipResult> Releases)> RunReleaseAsync(
        string resourcePath,
        string resourceName,
        IEnumerable<string> escrowPaths,
        VersionBumpKind bumpKind,
        ReleasePipelineOptions? options = null,
        IProgress<(string Message, int Percent)>? progress = null,
        CancellationToken ct = default)
    {
        options ??= new ReleasePipelineOptions();
        var msg = new Progress<string>(m => progress?.Report((m, -1)));

        if (!options.SkipPreChecks)
            await PreReleaseAsync(resourcePath, escrowPaths, msg, ct);

        progress?.Report(("ZIP oluşturuluyor…", 40));
        var result = await _releaseService.CreateDualReleaseAsync(
            resourcePath,
            resourceName,
            escrowPaths,
            bumpKind,
            options.ManualVersion,
            msg,
            ct);

        if (!options.SkipPostSteps)
            await PostReleaseAsync(resourcePath, resourceName, result.Version, result.Releases, options, progress, ct);

        progress?.Report(("Tamamlandı", 100));
        return result;
    }

    public async Task PostReleaseAsync(
        string resourcePath,
        string resourceName,
        string version,
        List<ReleaseZipResult> releases,
        ReleasePipelineOptions? options = null,
        IProgress<(string Message, int Percent)>? progress = null,
        CancellationToken ct = default)
    {
        options ??= new ReleasePipelineOptions();
        var config = _configService.Load();

        if (config.VerifyZipAfterRelease)
        {
            foreach (var zip in releases)
            {
                var (ok, message) = ZipVerifyService.Verify(zip.ZipPath);
                if (!ok)
                    throw new InvalidOperationException($"ZIP doğrulama: {zip.ZipName} — {message}");
            }
        }

        if (config.GitCommitOnRelease || config.GitTagOnRelease)
        {
            progress?.Report(("Git işlemleri…", 70));
            GitIntegrationService.CommitAndTag(resourcePath, version, config.GitCommitOnRelease, config.GitTagOnRelease);
        }

        var webhook = SecretProtector.Unprotect(config.DiscordWebhookProtected);
        if (!string.IsNullOrWhiteSpace(webhook))
        {
            progress?.Report(("Discord bildirimi…", 75));
            await DiscordWebhookService.NotifyReleaseAsync(
                webhook,
                resourceName,
                version,
                releases.Select(r => (r.Mode, r.Bytes)).ToList(),
                null,
                ct);
        }

        var ghToken = SecretProtector.Unprotect(config.GitHubTokenProtected);
        if (!string.IsNullOrWhiteSpace(ghToken) && !string.IsNullOrWhiteSpace(config.GitHubRepo))
        {
            progress?.Report(("GitHub release…", 80));
            var assets = releases
                .Where(z => config.UploadOsZipToGitHub || z.Mode == "escrow")
                .Select(z => (z.ZipName, z.ZipPath));
            var changelog = config.UseGitChangelog
                ? GitIntegrationService.GetRecentCommits(resourcePath) ?? PortalUploadService.ReadChangelogFromResource(resourcePath)
                : PortalUploadService.ReadChangelogFromResource(resourcePath);

            await GitHubReleaseService.CreateReleaseAsync(
                ghToken,
                config.GitHubRepo,
                $"v{version}",
                $"{resourceName} v{version}",
                changelog ?? "",
                assets,
                ct);
        }

        var tebexKey = SecretProtector.Unprotect(config.TebexPrivateKeyProtected);
        if (!string.IsNullOrWhiteSpace(tebexKey) && !string.IsNullOrWhiteSpace(config.TebexPackageId))
        {
            progress?.Report(("Tebex güncelleniyor…", 85));
            await TebexService.UpdatePackageVersionNoteAsync(tebexKey, config.TebexPackageId, resourceName, version, ct);
        }

        if (options.AutoUploadPortal || config.AutoUploadPortal)
        {
            progress?.Report(("Portal upload…", 90));
            var escrow = releases.FirstOrDefault(r => r.Mode == "escrow");
            if (escrow != null)
                await UploadPortalAsync(resourceName, resourcePath, escrow, version, ct);
        }
    }

    public async Task UploadPortalAsync(
        string resourceName,
        string resourcePath,
        ReleaseZipResult escrowZip,
        string version,
        CancellationToken ct = default)
    {
        var config = _configService.Load();
        var assetId = config.PortalAssets
            .FirstOrDefault(p => string.Equals(p.ResourceName, resourceName, StringComparison.OrdinalIgnoreCase))
            ?.AssetId ?? 0;

        if (assetId <= 0)
        {
            var search = await _portal.SearchAssetsAsync(null, resourceName, ct);
            assetId = AssetMatcherService.FindBestAssetId(resourceName, search);
        }

        if (assetId <= 0)
            throw new InvalidOperationException("Portal asset bulunamadı");

        var manifestFile = ManifestService.FindManifestPath(resourcePath)!;
        var manifest = ManifestService.ReadManifest(resourcePath, manifestFile);
        var changelog = config.UseGitChangelog
            ? GitIntegrationService.GetRecentCommits(resourcePath) ?? PortalUploadService.ReadChangelogFromResource(resourcePath)
            : PortalUploadService.ReadChangelogFromResource(resourcePath);
        var rc = PortalUploadService.IsReleaseCandidate(manifest);

        await _portal.UploadEscrowZipAsync(null, assetId, escrowZip.ZipPath, version, changelog, rc, null, ct);
    }

    public string? FindPreviousEscrowZip(string resourceName, string currentZipPath)
    {
        var history = _history.Load();
        return history
            .Where(h => string.Equals(h.ResourceName, resourceName, StringComparison.OrdinalIgnoreCase))
            .Where(h => !string.Equals(h.EscrowZipPath, currentZipPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => h.EscrowZipPath)
            .FirstOrDefault(File.Exists);
    }
}
