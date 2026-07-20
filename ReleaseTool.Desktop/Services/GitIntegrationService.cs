using System.Diagnostics;
using System.Text;

namespace ReleaseTool.Desktop.Services;

public static class GitIntegrationService
{
    public static bool IsRepo(string path) => Directory.Exists(Path.Combine(path, ".git"));

    public static string? GetRecentCommits(string resourcePath, int count = 10)
    {
        if (!IsRepo(resourcePath))
            return null;

        var output = RunGit(resourcePath, $"log -{count} --pretty=format:- %s");
        return string.IsNullOrWhiteSpace(output) ? null : output;
    }

    public static void CommitAndTag(string resourcePath, string version, bool commit, bool tag)
    {
        if (!IsRepo(resourcePath))
            return;

        if (commit)
        {
            RunGit(resourcePath, "add -A");
            RunGit(resourcePath, $"commit -m \"chore: release v{version}\" --allow-empty");
        }

        if (tag)
            RunGit(resourcePath, $"tag -a v{version} -m \"Release v{version}\" --force");
    }

    private static string RunGit(string cwd, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = cwd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
                return "";

            var stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return stdout;
        }
        catch
        {
            return "";
        }
    }
}
