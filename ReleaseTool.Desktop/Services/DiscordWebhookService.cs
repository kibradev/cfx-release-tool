using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ReleaseTool.Desktop.Services;

public static class DiscordWebhookService
{
    public static async Task NotifyReleaseAsync(
        string webhookUrl,
        string resourceName,
        string version,
        List<(string Mode, long Bytes)> zips,
        string? portalUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return;

        var totalMb = zips.Sum(z => z.Bytes) / (1024.0 * 1024.0);
        var sb = new StringBuilder();
        sb.AppendLine($"**{resourceName}** — v{version}");
        foreach (var z in zips)
            sb.AppendLine($"- {z.Mode}: {z.Bytes / (1024.0 * 1024.0):F2} MB");
        if (!string.IsNullOrEmpty(portalUrl))
            sb.AppendLine(portalUrl);

        var payload = JsonSerializer.Serialize(new
        {
            content = sb.ToString()
        });

        using var client = new HttpClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        await client.PostAsync(webhookUrl, content, ct);
    }
}
