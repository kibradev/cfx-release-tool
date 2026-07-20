using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReleaseTool.Desktop.Services;

public static class TebexService
{
    /// <summary>
    /// Tebex Headless API — paket açıklamasını günceller (basit entegrasyon).
    /// </summary>
    public static async Task UpdatePackageVersionNoteAsync(
        string privateKey,
        string packageId,
        string resourceName,
        string version,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(privateKey) || string.IsNullOrWhiteSpace(packageId))
            return;

        using var client = new HttpClient { BaseAddress = new Uri("https://plugin.tebex.io/") };
        client.DefaultRequestHeaders.Add("X-Tebex-Secret", privateKey);

        var payload = JsonSerializer.Serialize(new
        {
            description = $"{resourceName} v{version} — otomatik güncellendi"
        });

        var resp = await client.PutAsync(
            $"package/{packageId}",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Tebex güncellenemedi: {body}");
        }
    }
}
