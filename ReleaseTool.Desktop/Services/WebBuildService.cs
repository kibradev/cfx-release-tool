using System.Diagnostics;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class WebBuildService
{
    public static bool NeedsBuild(string resourcePath, AppConfig config)
    {
        var webDir = Path.Combine(resourcePath, "web");
        if (!Directory.Exists(webDir))
            return false;

        var publish = string.IsNullOrWhiteSpace(config.EscrowWebPublishFolder) ? "dist" : config.EscrowWebPublishFolder;
        var publishDir = Path.Combine(webDir, publish.Replace('/', Path.DirectorySeparatorChar));
        return !Directory.Exists(publishDir) || !Directory.EnumerateFileSystemEntries(publishDir).Any();
    }

    public static async Task RunBuildAsync(string resourcePath, AppConfig config, IProgress<string>? progress, CancellationToken ct = default)
    {
        var webDir = Path.Combine(resourcePath, "web");
        if (!Directory.Exists(webDir))
            return;

        var cmd = string.IsNullOrWhiteSpace(config.WebBuildCommand) ? "npm run build" : config.WebBuildCommand;
        progress?.Report($"Web build: {cmd}");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {cmd}",
            WorkingDirectory = webDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Web build başlatılamadı. Node/npm kurulu mu?");

        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"Web build başarısız (kod {proc.ExitCode}): {err}");
        }
    }
}
