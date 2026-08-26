using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ReleaseTool.Desktop.Services;

public static class AutoUpdateService
{
    private const string Owner = "kibradev";
    private const string Repo = "cfx-release-tool";

    private const string LatestApiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    private const string ReleasesPageUrl = $"https://github.com/{Owner}/{Repo}/releases";

    /// <summary>Son kontrolde bulunan release sayfası (yoksa genel releases sayfası).</summary>
    private static string _downloadUrl = ReleasesPageUrl;

    /// <summary>Çalışan uygulamanın sürümü (csproj &lt;Version&gt; ile senkron).</summary>
    public static Version CurrentVersion { get; } = ResolveCurrentVersion();

    public static async Task<(bool Available, string? Message)> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            // GitHub API User-Agent zorunlu tutar.
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReleaseTool", CurrentVersion.ToString()));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var resp = await http.GetAsync(LatestApiUrl, ct);
            if (!resp.IsSuccessStatusCode)
                return (false, null); // 404 = henüz release yok

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag))
                return (false, null);

            if (root.TryGetProperty("html_url", out var urlEl) && urlEl.GetString() is { Length: > 0 } url)
                _downloadUrl = url;

            var remote = ParseVersion(tag);
            if (remote == null)
                return (false, null);

            if (remote > CurrentVersion)
            {
                var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var title = string.IsNullOrWhiteSpace(name) ? $"v{remote}" : name;
                return (true, $"Yeni sürüm mevcut: {title} (şu an v{Minimal(CurrentVersion)})");
            }
        }
        catch (OperationCanceledException)
        {
            // zaman aşımı / iptal — sessizce geç
        }
        catch
        {
            // ağ/parse hatası — güncelleme kontrolü kritik değil
        }

        return (false, null);
    }

    public static void OpenDownloadPage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _downloadUrl,
            UseShellExecute = true
        });
    }

    private static Version ResolveCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return ParseVersion(info) ?? asm.GetName().Version ?? new Version(1, 0, 0);
    }

    /// <summary>"v1.2.0", "1.2.0-beta", "1.2.0+build" gibi etiketleri Version'a çevirir.</summary>
    private static Version? ParseVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
            s = s[1..];

        // pre-release / build metadata'yı at (1.2.0-beta.1+sha → 1.2.0)
        var cut = s.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0)
            s = s[..cut];

        return Version.TryParse(s, out var v) ? v : null;
    }

    private static string Minimal(Version v) =>
        v.Build >= 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
}
