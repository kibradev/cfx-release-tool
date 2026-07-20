using System.Diagnostics;
using System.Reflection;

namespace ReleaseTool.Desktop.Services;

public static class AutoUpdateService
{
    public const string CurrentVersion = "1.1.0";

    public static async Task<(bool Available, string? Message)> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            // Basit kontrol: publish klasöründeki version.txt (opsiyonel dağıtım)
            var exeDir = AppContext.BaseDirectory;
            var localFile = Path.Combine(exeDir, "version.txt");
            if (File.Exists(localFile))
            {
                var remote = (await File.ReadAllTextAsync(localFile, ct)).Trim();
                if (Version.Parse(remote) > Version.Parse(CurrentVersion))
                    return (true, $"Yeni sürüm mevcut: v{remote} (şu an v{CurrentVersion})");
            }
        }
        catch
        {
            // ignore
        }

        return (false, null);
    }

    public static void OpenDownloadPage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/",
            UseShellExecute = true
        });
    }
}
