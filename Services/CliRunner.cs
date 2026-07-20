using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class CliRunner
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var opts = ParseArgs(args);
            var configService = new ConfigService();
            var config = configService.Load();

            var folder = opts.Folder ?? config.ResourcesFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                throw new InvalidOperationException("--folder gerekli");

            var resources = ResourceScanner.ScanResources(folder);
            if (resources.Count == 0)
                throw new InvalidOperationException("Resource bulunamadı");

            var targets = opts.Batch
                ? resources
                : resources.Where(r =>
                    string.Equals(r.Name, opts.Resource, StringComparison.OrdinalIgnoreCase)).ToList();

            if (targets.Count == 0)
                throw new InvalidOperationException($"Resource bulunamadı: {opts.Resource}");

            var orchestrator = new ReleaseOrchestrator();
            var selectionStore = new SelectionStore();

            foreach (var resource in targets)
            {
                Console.WriteLine($"Release: {resource.Name}");
                var escrowPaths = ResolveEscrowPaths(resource, config, selectionStore);
                var bump = opts.Bump ?? VersionBumpKind.Patch;

                var pipeline = new ReleasePipelineOptions
                {
                    ManualVersion = opts.Version,
                    AutoUploadPortal = opts.Upload,
                    SkipPreChecks = opts.SkipChecks,
                    SkipPostSteps = !opts.PostSteps
                };

                var progress = new Progress<(string Message, int Percent)>(p =>
                    Console.WriteLine(p.Percent >= 0 ? $"[{p.Percent}%] {p.Message}" : p.Message));

                var (version, releases) = orchestrator.RunReleaseAsync(
                    resource.Path,
                    resource.Name,
                    escrowPaths,
                    string.IsNullOrEmpty(opts.Version) ? bump : VersionBumpKind.None,
                    pipeline,
                    progress).GetAwaiter().GetResult();

                Console.WriteLine($"OK — v{version} ({releases.Count} zip)");
                foreach (var z in releases)
                    Console.WriteLine($"  {z.ZipName} ({z.Bytes / (1024 * 1024.0):F2} MB)");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"HATA: {ex.Message}");
            return 1;
        }
    }

    private static HashSet<string> ResolveEscrowPaths(ResourceInfo resource, AppConfig config, SelectionStore store)
    {
        var saved = store.Load(resource.Path);
        if (saved is { Count: > 0 })
            return saved.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tree = FileTreeService.BuildTree(resource.Path);
        var filePaths = FileTreeService.CollectAllFilePaths(tree);
        var manifestFile = ManifestService.FindManifestPath(resource.Path);
        var patterns = manifestFile != null
            ? ManifestService.ParseEscrowIgnorePaths(ManifestService.ReadManifest(resource.Path, manifestFile))
            : [];

        return EscrowIgnoreService.BuildInitialEscrowSelection(patterns, filePaths, config);
    }

    private static CliOptions ParseArgs(string[] args)
    {
        var opts = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--folder" when i + 1 < args.Length: opts.Folder = args[++i]; break;
                case "--resource" when i + 1 < args.Length: opts.Resource = args[++i]; break;
                case "--version" when i + 1 < args.Length: opts.Version = args[++i]; break;
                case "--bump" when i + 1 < args.Length:
                    opts.Bump = args[++i].ToLowerInvariant() switch
                    {
                        "none" => VersionBumpKind.None,
                        "minor" => VersionBumpKind.Minor,
                        "major" => VersionBumpKind.Major,
                        _ => VersionBumpKind.Patch
                    };
                    break;
                case "--upload": opts.Upload = true; break;
                case "--batch": opts.Batch = true; break;
                case "--skip-checks": opts.SkipChecks = true; break;
                case "--no-post": opts.PostSteps = false; break;
            }
        }

        return opts;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            ReleaseTool CLI

            ReleaseTool.exe --folder C:\resources --resource my-script [--bump patch|minor|major|none]
            ReleaseTool.exe --folder C:\resources --batch [--upload]
            ReleaseTool.exe --folder C:\resources --resource my-script --version 2.0.0
            """);
    }

    private sealed class CliOptions
    {
        public string? Folder { get; set; }
        public string? Resource { get; set; }
        public string? Version { get; set; }
        public VersionBumpKind? Bump { get; set; }
        public bool Upload { get; set; }
        public bool Batch { get; set; }
        public bool SkipChecks { get; set; }
        public bool PostSteps { get; set; } = true;
    }
}
