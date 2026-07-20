using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public sealed class PortalUploadService
{
    private const string ApiBase = "https://portal-api.cfx.re/v1/";
    private const int DefaultChunkSize = 2 * 1024 * 1024;
    private readonly ConfigService _configService = new();

    public async Task<bool> TestAuthenticationAsync(string? forumCookieInput = null, CancellationToken ct = default)
    {
        using var client = await CreateAuthenticatedClientAsync(forumCookieInput, ct);
        var response = await client.GetAsync($"{ApiBase}me/assets?search=&sort=asset.name&direction=asc", ct);
        if (response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Unauthorized ({(int)response.StatusCode}): {body}\n\n" +
            "Portal ayarları → 'Tarayıcıdan giriş yap' kullanın (en güvenilir yöntem).");
    }

    public async Task<List<PortalAssetSearchItem>> SearchAssetsAsync(string? forumCookieInput, string query, CancellationToken ct = default)
    {
        using var client = await CreateAuthenticatedClientAsync(forumCookieInput, ct);
        var url = $"{ApiBase}me/assets?search={Uri.EscapeDataString(query)}&sort=asset.name&direction=asc";
        var response = await client.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Asset araması başarısız ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var items = new List<PortalAssetSearchItem>();

        if (!doc.RootElement.TryGetProperty("items", out var arr))
            return items;

        foreach (var item in arr.EnumerateArray())
        {
            items.Add(new PortalAssetSearchItem
            {
                Id = item.GetProperty("id").GetInt32(),
                Name = item.GetProperty("name").GetString() ?? ""
            });
        }

        return items;
    }

    public async Task UploadEscrowZipAsync(
        string? forumCookieInput,
        int assetId,
        string zipPath,
        string version,
        string changelog,
        bool releaseCandidate,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP bulunamadı", zipPath);

        using var client = await CreateAuthenticatedClientAsync(forumCookieInput, ct);
        var fileInfo = new FileInfo(zipPath);
        var chunkSize = DefaultChunkSize;
        var chunkCount = (int)Math.Ceiling(fileInfo.Length / (double)chunkSize);
        var fileName = Path.GetFileName(zipPath);

        progress?.Report("Escrow ZIP yükleniyor…");

        var startPayload = new
        {
            chunk_count = chunkCount,
            chunk_size = chunkSize,
            name = fileName,
            original_file_name = fileName,
            total_size = fileInfo.Length,
            release_candidate = releaseCandidate,
            version,
            changelog
        };

        var startJson = JsonSerializer.Serialize(startPayload);
        using var startContent = new StringContent(startJson, Encoding.UTF8, "application/json");
        var startResponse = await client.PostAsync($"{ApiBase}assets/{assetId}/re-upload", startContent, ct);
        var startBody = await startResponse.Content.ReadAsStringAsync(ct);

        if (!startResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Portal re-upload başarısız: {startBody}");

        using var startDoc = JsonDocument.Parse(startBody);
        if (startDoc.RootElement.TryGetProperty("errors", out var err) && err.ValueKind != JsonValueKind.Null)
            throw new InvalidOperationException($"Portal hatası: {err}");

        var uploadAssetId = startDoc.RootElement.GetProperty("asset_id").GetInt32();
        var versionId = startDoc.RootElement.GetProperty("version_id").GetInt32();

        progress?.Report($"Yükleniyor… 0/{chunkCount}");

        await using var stream = File.OpenRead(zipPath);
        var buffer = new byte[chunkSize];
        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer.AsMemory(0, chunkSize), ct);
            if (read <= 0)
                break;

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(chunkIndex.ToString()), "chunk_id");
            var chunkContent = new ByteArrayContent(buffer.AsMemory(0, read).ToArray());
            chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(chunkContent, "chunk", "blob");

            var chunkResponse = await client.PostAsync(
                $"{ApiBase}assets/{uploadAssetId}/versions/{versionId}/upload-chunk",
                form,
                ct);

            if (!chunkResponse.IsSuccessStatusCode)
            {
                var errBody = await chunkResponse.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Chunk {chunkIndex + 1} yüklenemedi: {errBody}");
            }

            progress?.Report($"Yükleniyor… {chunkIndex + 1}/{chunkCount}");
        }

        var completeResponse = await client.PostAsync(
            $"{ApiBase}assets/{uploadAssetId}/versions/{versionId}/complete-upload",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            ct);

        if (!completeResponse.IsSuccessStatusCode)
        {
            var errBody = await completeResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Upload tamamlanamadı: {errBody}");
        }

        progress?.Report("Portal yükleme tamamlandı");
    }

    public void SavePortalSession(IEnumerable<StoredPortalCookie> cookies)
    {
        var config = _configService.Load();
        config.PortalSessionProtected = PortalSessionStore.Protect(cookies);
        _configService.Save(config);
    }

    public bool HasPortalSession()
    {
        var config = _configService.Load();
        return PortalSessionStore.Unprotect(config.PortalSessionProtected).Count > 0;
    }

    public static string ReadChangelogFromResource(string resourcePath)
    {
        var path = Path.Combine(resourcePath, "CHANGELOG.md");
        if (!File.Exists(path))
            return "Automated Release";

        var text = File.ReadAllText(path);
        var match = Regex.Match(text, @"## v[^\n]+\n([\s\S]*?)(?=\n## |\z)");
        if (match.Success)
        {
            var body = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(body))
                return body.Length > 2000 ? body[..2000] : body;
        }

        return "Automated Release";
    }

    public static bool IsReleaseCandidate(string manifestContent)
    {
        return Regex.IsMatch(manifestContent, @"^\s*beta\s+['""]", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    public static string GetPortalAssetUrl(int assetId) =>
        $"https://portal.cfx.re/assets/created-assets/{assetId}";

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string? forumCookieInput, CancellationToken ct)
    {
        var container = new CookieContainer();
        var config = _configService.Load();

        // 1) WebView2 oturumu (tercih edilen)
        var portalCookies = PortalSessionStore.Unprotect(config.PortalSessionProtected);
        if (portalCookies.Count > 0)
            PortalSessionStore.ApplyToContainer(container, portalCookies);

        var handler = new HttpClientHandler
        {
            CookieContainer = container,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");

        // Portal session varsa doğrudan dene
        if (portalCookies.Count > 0)
        {
            var test = await client.GetAsync($"{ApiBase}me/assets?search=&sort=asset.name&direction=asc", ct);
            if (test.IsSuccessStatusCode)
                return client;
        }

        // 2) Forum cookie SSO (cfx-portal-upload sırası)
        var input = forumCookieInput ?? SecretProtector.Unprotect(config.ForumCookieProtected);
        var parsed = ForumCookieParser.Parse(input);
        if (!parsed.ContainsKey("_t"))
        {
            client.Dispose();
            throw new InvalidOperationException(
                "Portal oturumu yok.\n\n" +
                "Portal ayarları → 'Tarayıcıdan giriş yap' butonunu kullanın.\n" +
                "Veya Network'ten tam cookie satırını yapıştırın.");
        }

        container = new CookieContainer();
        handler = new HttpClientHandler
        {
            CookieContainer = container,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true
        };
        client.Dispose();
        client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");

        // SSO URL al (cookie OLMADAN)
        var ssoResponse = await client.GetAsync($"{ApiBase}auth/discourse?return=https://portal.cfx.re/", ct);
        var ssoBody = await ssoResponse.Content.ReadAsStringAsync(ct);
        if (!ssoResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"SSO başarısız ({(int)ssoResponse.StatusCode}): {ssoBody}");

        using var doc = JsonDocument.Parse(ssoBody);
        var redirectUrl = doc.RootElement.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("SSO URL alınamadı.");

        // Forum cookie ekle
        ForumCookieParser.ApplyToContainer(container, parsed);

        // Forum origin ziyaret (referans akış)
        var forumOrigin = new Uri(redirectUrl).GetLeftPart(UriPartial.Authority);
        await client.GetAsync(forumOrigin, ct);

        // SSO redirect
        await client.GetAsync(redirectUrl, ct);
        await client.GetAsync("https://portal.cfx.re/", ct);

        return client;
    }
}

public sealed class PortalAssetSearchItem
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}
