using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReleaseTool.Desktop.Services;

public static class GitHubReleaseService
{
    public static async Task CreateReleaseAsync(
        string token,
        string repo,
        string tag,
        string title,
        string body,
        IEnumerable<(string Name, string Path)> assets,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(repo))
            return;

        var parts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException("GitHub repo formatı: owner/repo");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ReleaseTool/1.0");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createPayload = JsonSerializer.Serialize(new
        {
            tag_name = tag,
            name = title,
            body,
            draft = false,
            prerelease = tag.Contains("beta", StringComparison.OrdinalIgnoreCase)
        });

        var createUrl = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases";
        var createResp = await client.PostAsync(createUrl, new StringContent(createPayload, Encoding.UTF8, "application/json"), ct);
        var createBody = await createResp.Content.ReadAsStringAsync(ct);
        if (!createResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub release oluşturulamadı: {createBody}");

        using var doc = JsonDocument.Parse(createBody);
        var uploadUrl = doc.RootElement.GetProperty("upload_url").GetString()?.Replace("{?name,label}", "", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(uploadUrl))
            return;

        foreach (var asset in assets)
        {
            if (!File.Exists(asset.Path))
                continue;

            await using var stream = File.OpenRead(asset.Path);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var url = $"{uploadUrl}?name={Uri.EscapeDataString(asset.Name)}";
            var resp = await client.PostAsync(url, content, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"GitHub asset yüklenemedi ({asset.Name}): {err}");
            }
        }
    }
}
